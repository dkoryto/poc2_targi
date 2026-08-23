using Dspc.Api.Auth;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Sites;

namespace Dspc.Api.Endpoints;

/// <summary>What-If scenarios: presets, create, run, compare, approve/reject/save. Baseline lives in CoreEndpoints.</summary>
public static class ScenarioEndpoints
{
    public static void MapScenarioEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/planning/scenarios").WithTags("Planning").RequireAuthorization(Policies.Planner);

        g.MapGet("/presets", async (string? siteCode, ScenarioPresetProvider presets, ISiteContext sites, CancellationToken ct) =>
                Results.Ok(await presets.GetAsync(await sites.ResolveSiteAsync(siteCode, ct), ct)))
            .WithSummary("The five demo What-If tiles, with inbound targets resolved to live purchase-order lines");

        g.MapGet("", async (string? siteCode, ScenarioService svc, ISiteContext sites, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(await sites.ResolveAsync(siteCode, ct), ct)))
            .WithSummary("Recent scenarios");

        g.MapGet("/{id:guid}", async (Guid id, ScenarioService svc, CancellationToken ct) => Results.Ok(await svc.GetAsync(id, ct)))
            .WithSummary("Scenario with Before/After plans, KPIs, explanations and consequences");

        g.MapGet("/{id:guid}/compare", async (Guid id, ScenarioService svc, CancellationToken ct) => Results.Ok(await svc.CompareAsync(id, ct)))
            .WithSummary("Moved operations and the KPI delta between Before and After");

        g.MapPost("", async (CreateScenarioRequest body, ScenarioService svc, CancellationToken ct) =>
            {
                var created = await svc.CreateAsync(body, ct);
                return Results.Created($"/api/v1/planning/scenarios/{created.Id}", created);
            })
            .RequireAuthorization(Policies.PlanApprove)
            .WithSummary("Creates a draft scenario (never touches the baseline)");

        g.MapPost("/{id:guid}/run", async (Guid id, ScenarioService svc, ScenarioRunQueue queue, CancellationToken ct) =>
            {
                var accepted = await svc.RequestRunAsync(id, queue, ct);
                return Results.Accepted($"/api/v1/planning/scenarios/{id}", accepted);
            })
            .RequireAuthorization(Policies.PlanApprove)
            .RequireRateLimiting("scenario")
            .WithSummary("Queues the solve; completion arrives over SignalR as PlanningScenarioCompleted");

        g.MapPost("/{id:guid}/approve", async (Guid id, ScenarioService svc, CancellationToken ct) => Results.Ok(await svc.ApproveAsync(id, ct)))
            .RequireAuthorization(Policies.PlanApprove)
            .WithSummary("Promotes the proposal to a new baseline version (previous version is kept)");

        g.MapPost("/{id:guid}/reject", async (Guid id, ScenarioService svc, CancellationToken ct) => Results.Ok(await svc.RejectAsync(id, ct)))
            .RequireAuthorization(Policies.PlanApprove);

        g.MapPost("/{id:guid}/save", async (Guid id, ScenarioService svc, CancellationToken ct) => Results.Ok(await svc.SaveAsync(id, ct)))
            .RequireAuthorization(Policies.PlanApprove);
    }
}
