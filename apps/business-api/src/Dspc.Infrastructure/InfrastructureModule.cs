using Dspc.Application.Abstractions;
using Dspc.Infrastructure.Identity;
using Dspc.Infrastructure.Outbox;
using Dspc.Infrastructure.Persistence;
using Dspc.Infrastructure.Seeding;
using Dspc.Infrastructure.Services;
using Dspc.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Dspc.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
        services.Configure<PlanningEngineOptions>(config.GetSection(PlanningEngineOptions.Section));
        services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));

        var cs = config.GetConnectionString("Default") ?? "Host=localhost;Port=5432;Database=dspc;Username=dspc;Password=dspc";
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs, n => n.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)).EnableDetailedErrors());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IDemoClock, DemoClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IRecentErrors, RecentErrorsBuffer>();
        services.AddSingleton<SeedState>();
        services.AddHttpClient("probe");
        services.AddSingleton<IExternalServiceProbe, ExternalServiceProbe>();
        services.AddHttpClient<Application.Modules.Planning.IPlanningEngine, Planning.PlanningEngineClient>(Planning.PlanningEngineClient.ClientName, (sp, c) =>
        {
            var o = sp.GetRequiredService<IOptions<PlanningEngineOptions>>().Value;
            c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromMilliseconds(Math.Max(500, o.TimeoutMs) + 500);   // hard ceiling; the per-call CTS is authoritative
        });
        services.AddSingleton<IFileScanner, NoOpFileScanner>();

        services.AddSingleton<IDocumentStorage>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<StorageOptions>>();
            return o.Value.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase) ? new MinioDocumentStorage(o) : new FileSystemDocumentStorage(o);
        });

        services.AddSingleton<Application.Modules.Passports.IPassportPdfGenerator, Documents.PassportPdfGenerator>();
        services.AddScoped<ISeedPostProcessor, Documents.PassportSeedPostProcessor>();
        services.AddHttpClient("local-ai", (sp, c) =>
        {
            var o = sp.GetRequiredService<IOptions<Application.Modules.Admin.LocalAiOptions>>().Value;
            c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddScoped<IDemoSeeder, DemoSeeder>();
        services.AddHostedService<OutboxDispatcherHostedService>();
        services.AddHostedService<MigrateAndSeedHostedService>();
        return services;
    }
}

/// <summary>
/// Applies migrations and seeds demo data on startup (demo profile).
/// Waiting for PostgreSQL to accept connections is retried; a deterministic failure (bad schema, constraint
/// violation, missing seed files) is NOT — it is reported once, loudly, so the operator sees the cause instead of
/// a minute of identical warnings followed by an unexplained 503. Either way startup continues and
/// /health/ready reports the reason.
/// </summary>
public sealed class MigrateAndSeedHostedService(IServiceScopeFactory scopes, IConfiguration config, IHostEnvironment env, ILogger<MigrateAndSeedHostedService> log) : IHostedService
{
    private const int MaxAttempts = 30;

    public static bool Ready { get; private set; }
    public static string? LastError { get; private set; }
    /// <summary>Set when startup failed for a reason retrying cannot fix, so the operator gets one clear message.</summary>
    public static string? FatalError { get; private set; }

    public async Task StartAsync(CancellationToken ct)
    {
        if (config.GetValue<bool>("Seed:Skip")) { Ready = true; return; }
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync(ct);
                if (config.GetValue<bool>("Demo:Enabled") || env.IsDevelopment())
                {
                    var seeder = scope.ServiceProvider.GetRequiredService<IDemoSeeder>();
                    var result = await seeder.SeedIfEmptyAsync(ct);
                    log.LogInformation("Demo seed state: version {Version}, {Ms} ms", result.SeedVersion, result.DurationMs);
                }
                Ready = true; LastError = null; FatalError = null;
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < MaxAttempts && !ct.IsCancellationRequested)
            {
                LastError = ex.Message;
                log.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, MaxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                FatalError = Describe(ex);
                log.LogCritical(ex, "Migration/seed failed and cannot succeed on retry. {Remediation}", FatalError);
                return;
            }
        }
    }

    /// <summary>True only for "the database is not accepting connections yet" — the one condition worth waiting on.</summary>
    private static bool IsTransient(Exception ex) => ex switch
    {
        PostgresException => false,                       // the server answered: schema/constraint/permission problem
        NpgsqlException { InnerException: SocketException } => true,
        NpgsqlException e => e.IsTransient,
        TimeoutException => true,
        _ => ex.InnerException is not null && IsTransient(ex.InnerException),
    };

    private static string Describe(Exception ex) => ex switch
    {
        PostgresException pg => $"PostgreSQL rejected the migration or seed (SQLSTATE {pg.SqlState}). The database schema is incompatible with this build; recreate it with 'docker compose --profile demo down -v' and start again.",
        DirectoryNotFoundException => "Demo seed data was not found. Set Seed:Path to the packages/demo-data folder.",
        _ => "Startup could not complete. See the logged exception; recreating the database with 'docker compose --profile demo down -v' resolves schema mismatches.",
    };

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
