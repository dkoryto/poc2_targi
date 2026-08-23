using Dspc.Api.Auth;
using Dspc.Api.Middleware;
using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Admin;
using Dspc.Application.Modules.Audit;
using Dspc.Application.Modules.Dashboard;
using Dspc.Application.Modules.Demo;
using Dspc.Application.Modules.Identity;
using Dspc.Application.Modules.Inventory;
using Dspc.Application.Modules.Notifications;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Sites;
using Dspc.Application.Modules.Suppliers;
using Dspc.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dspc.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResultStatusCodes = { [HealthStatus.Healthy] = 200, [HealthStatus.Degraded] = 200, [HealthStatus.Unhealthy] = 503 },
            ResponseWriter = async (ctx, report) =>
            {
                var ready = report.Status != HealthStatus.Unhealthy && MigrateAndSeedHostedService.Ready;
                ctx.Response.StatusCode = ready ? 200 : 503;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsJsonAsync(new { status = ready ? "Healthy" : "Unhealthy", seeded = MigrateAndSeedHostedService.Ready, seedError = MigrateAndSeedHostedService.LastError, checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), durationMs = (int)e.Value.Duration.TotalMilliseconds }) });
            }
        }).AllowAnonymous();
    }
}

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/auth").WithTags("Identity");
        g.MapPost("/login", async (LoginRequest req, IdentityService svc, IAppDbContext db, CancellationToken ct) => { var r = await svc.LoginAsync(req, ct); await db.SaveChangesAsync(ct); return Results.Ok(r); })
            .AddEndpointFilter<ValidationFilter<LoginRequest>>().RequireRateLimiting("login").AllowAnonymous().WithSummary("Username/password login → JWT");
        g.MapGet("/me", async (ICurrentUser user, IdentityService svc, CancellationToken ct) => Results.Ok(await svc.MeAsync(user.Id ?? Guid.Empty, ct))).RequireAuthorization(Policies.Authenticated);
        g.MapGet("/demo-login", async (string? role, string? supplierCode, IdentityService svc, IAppDbContext db, CancellationToken ct) => { var r = await svc.DemoLoginAsync(role, supplierCode, ct); await db.SaveChangesAsync(ct); return Results.Ok(r); })
            .RequireRateLimiting("login").AllowAnonymous().WithSummary("Demo profile only: issue a token for any role");
        g.MapGet("/demo-accounts", async (IdentityService svc, CancellationToken ct) => Results.Ok(await svc.DemoAccountsAsync(ct))).AllowAnonymous();
    }
}

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/dashboard").WithTags("Dashboard").RequireAuthorization(Policies.Dashboard);
        g.MapGet("/kpis", async (string? siteCode, DashboardQueries q, ISiteContext sites, CancellationToken ct) => Results.Ok(await q.KpisAsync(await sites.ResolveAsync(siteCode, ct), ct)));
        g.MapGet("/map", async (string? siteCode, DashboardQueries q, ISiteContext sites, CancellationToken ct) => Results.Ok(await q.MapAsync(await sites.ResolveAsync(siteCode, ct), ct)));
        g.MapGet("/risk-heatmap", async (string? siteCode, DashboardQueries q, ISiteContext sites, CancellationToken ct) => Results.Ok(await q.HeatmapAsync(await sites.ResolveAsync(siteCode, ct), ct)));
        g.MapGet("/quality-status", async (string? siteCode, DashboardQueries q, ISiteContext sites, CancellationToken ct) => Results.Ok(await q.QualityStatusAsync(await sites.ResolveAsync(siteCode, ct), ct)));
        g.MapGet("/plan", async (string? siteCode, DashboardQueries q, ISiteContext sites, CancellationToken ct) => Results.Ok(await q.PlanAsync(await sites.ResolveAsync(siteCode, ct), ct)));
    }
}

public static class SiteEndpoints
{
    public static void MapSiteEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/sites", async (SiteQueries q, CancellationToken ct) => Results.Ok(await q.ListAsync(ct)))
            .WithTags("Sites").RequireAuthorization().WithSummary("Plants this user may work with, in display order");
    }
}

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/suppliers").WithTags("Suppliers").RequireAuthorization(Policies.SupplyRead);
        g.MapGet("", async (SupplierQueries q, CancellationToken ct) => Results.Ok(await q.ListAsync(ct)));
        g.MapGet("/{code}", async (string code, SupplierQueries q, CancellationToken ct) => Results.Ok(await q.GetAsync(code, ct)));
    }
}

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/inventory", async (string? partCode, string? siteCode, InventoryQueries q, ISiteContext sites, CancellationToken ct) =>
            Results.Ok(await q.ListAsync(await sites.ResolveAsync(siteCode, ct), partCode, ct))).WithTags("Inventory").RequireAuthorization(Policies.Dashboard);
    }
}

public static class PlanningEndpoints
{
    public static void MapPlanningEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/planning").WithTags("Planning").RequireAuthorization(Policies.Planner);
        g.MapGet("/baseline", async (string? siteCode, PlanningQueries q, ISiteContext sites, CancellationToken ct) =>
            Results.Ok(await q.GetBaselineAsync(await sites.ResolveAsync(siteCode, ct), ct))).WithSummary("Active baseline of the selected plant, evaluated against current ETAs/stock (no re-sequencing)");
    }
}

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/notifications").WithTags("Notifications").RequireAuthorization(Policies.Authenticated);
        g.MapGet("", async (bool? unreadOnly, NotificationService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(unreadOnly ?? false, ct)));
        g.MapPost("/{id:guid}/read", async (Guid id, NotificationService svc, CancellationToken ct) => { await svc.MarkReadAsync(id, ct); return Results.NoContent(); });
        g.MapPost("/read-all", async (NotificationService svc, CancellationToken ct) => { await svc.MarkAllReadAsync(ct); return Results.NoContent(); });
    }
}

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/audit").WithTags("Audit").RequireAuthorization(Policies.Audit);
        g.MapGet("", async (string? entity, string? code, string? user, DateTime? from, DateTime? to, int? page, int? pageSize, string? format, AuditQueries q, CancellationToken ct) =>
        {
            var filter = new AuditFilter(entity, code, user, from, to, page ?? 1, pageSize ?? 50);
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
                return Results.File(System.Text.Encoding.UTF8.GetBytes(await q.ExportCsvAsync(filter, ct)), "text/csv", $"audit-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
            return Results.Ok(await q.ListAsync(filter, ct));
        });
    }
}

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/demo").WithTags("Demo");
        g.MapGet("/status", (DemoService svc) => Results.Ok(svc.Status())).AllowAnonymous();
        g.MapGet("/script", (DemoService svc) => Results.Ok(svc.Script())).RequireAuthorization(Policies.Authenticated);
        g.MapPost("/reset", async (DemoService svc, CancellationToken ct) => Results.Ok(await svc.ResetAsync(ct))).RequireAuthorization(Policies.DemoControl).RequireRateLimiting("reset").WithSummary("Truncate + reseed (< 10 s). Demo profile only.");
    }
}

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/admin").WithTags("Admin").RequireAuthorization(Policies.Admin);
        g.MapGet("/settings", (AdminService svc) => Results.Ok(svc.Settings()));
        g.MapGet("/status", async (AdminService svc, CancellationToken ct) => Results.Ok(await svc.StatusAsync(ct)));
    }
}
