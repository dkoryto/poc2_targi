using System.Text;
using System.Text.Json.Serialization;
using System.Net;
using System.Threading.RateLimiting;
using Dspc.Api.Auth;
using Dspc.Api.Endpoints;
using Dspc.Api.Middleware;
using Dspc.Api.Realtime;
using Dspc.Application;
using Dspc.Application.Abstractions;
using Dspc.Infrastructure;
using Dspc.Infrastructure.Identity;
using Dspc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ---------- logging (structured, correlation id enriched, secrets never logged)
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .MinimumLevel.Information()
       .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
       .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
       .Enrich.FromLogContext()
       .Enrich.WithMachineName()
       .Enrich.WithProperty("service", "business-api");
    if (ctx.HostingEnvironment.IsDevelopment()) cfg.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
    else cfg.WriteTo.Console(new CompactJsonFormatter());
});

var config = builder.Configuration;
var jwtKey = config["Identity:Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    if (builder.Environment.IsDevelopment()) { jwtKey = "dspc-development-only-jwt-key-0123456789abcdef"; config["Identity:Jwt:Key"] = jwtKey; }
    else throw new InvalidOperationException("Identity__Jwt__Key must be set (>= 32 characters) outside Development.");
}

builder.Services.AddApplication(config);
builder.Services.AddInfrastructure(config);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ISupplierScope, SupplierScope>();
builder.Services.AddSingleton<ILiveBroadcaster, SignalRLiveBroadcaster>();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = config["Identity:Jwt:Issuer"] ?? "dspc",
        ValidateAudience = true, ValidAudience = config["Identity:Jwt:Audience"] ?? "dspc",
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "unique_name", RoleClaimType = DspcClaims.Role
    };
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            // SignalR websockets cannot set headers — token arrives as ?access_token=
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")) ctx.Token = token;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization(Policies.Configure);

builder.Services.AddSignalR(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    ctx.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(ctx.HttpContext);
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var origins = (config["Cors:Origins"] ?? "http://localhost:5173").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("ETag", "X-Correlation-Id")));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    string Key(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "anon";
    int Limit(string name, int fallback) => config.GetValue<int?>($"RateLimits:{name}PerMinute") ?? fallback;
    void Policy(string name, int fallback) => o.AddPolicy(name, c => RateLimitPartition.GetFixedWindowLimiter(Key(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit(name, fallback), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    Policy("login", 30);      // demo role switching + auto-login share this budget per IP
    Policy("upload", 30);
    Policy("scenario", 20);
    Policy("reset", 20);  // a presenter may reset repeatedly while rehearsing
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "DSPC business-api", Version = "v1", Description = "Defense Supply & Production Control — demonstrator. Fictional data only." });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header, Name = "Authorization" });
    o.SupportNonNullableReferenceTypes();
    var xml = Path.Combine(AppContext.BaseDirectory, "Dspc.Api.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

// Behind a reverse proxy (Caddy, then the web container's nginx) the socket address is the
// proxy, not the client. Without honouring X-Forwarded-* the per-IP rate limits collapse into a
// single shared bucket and the audit trail records the proxy address for every user.
if (config.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        o.ForwardLimit = 2;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
        foreach (var cidr in (config["ForwardedHeaders:TrustedNetworks"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = cidr.Split('/');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var length))
                o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, length));
            else if (IPAddress.TryParse(cidr, out var proxy))
                o.KnownProxies.Add(proxy);
        }
    });
}

var app = builder.Build();

if (config.GetValue<bool>("ForwardedHeaders:Enabled")) app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(o =>
{
    o.GetLevel = (ctx, _, ex) => ex is not null || ctx.Response.StatusCode >= 500 ? LogEventLevel.Error : ctx.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Debug : LogEventLevel.Information;
    o.EnrichDiagnosticContext = (d, ctx) => { d.Set("CorrelationId", CorrelationIdMiddleware.Get(ctx)); d.Set("User", ctx.User.Identity?.Name ?? "anonymous"); };
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<IdempotencyMiddleware>();

// Swagger is developer documentation; in Production it must not be served at all
// (the Caddy proxy additionally answers /swagger with 404).
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => { o.SwaggerEndpoint("/swagger/v1/swagger.json", "DSPC v1"); o.DocumentTitle = "DSPC API"; });
}

app.MapHealthEndpoints();
var api = app.MapGroup("/api/v1");
api.MapIdentityEndpoints();
api.MapSiteEndpoints();
api.MapDashboardEndpoints();
api.MapSupplierEndpoints();
api.MapInboundEndpoints();
api.MapDocumentEndpoints();
api.MapInventoryEndpoints();
api.MapQualityEndpoints();
api.MapTraceEndpoints();
api.MapPassportEndpoints();
api.MapPlanningEndpoints();
api.MapScenarioEndpoints();
api.MapNotificationEndpoints();
api.MapAuditEndpoints();
api.MapDemoEndpoints();
api.MapAdminEndpoints();
app.MapHub<LiveHub>("/hubs/live").RequireAuthorization();

app.Run();

public partial class Program { }
