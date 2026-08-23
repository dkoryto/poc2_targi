using Dspc.Api.Auth;
using Dspc.Api.Middleware;
using Dspc.Application.Modules.Quality;
using Dspc.Application.Modules.Sites;

namespace Dspc.Api.Endpoints;

public static class QualityEndpoints
{
    public static void MapQualityEndpoints(this RouteGroupBuilder api)
    {
        var lots = api.MapGroup("/lots").WithTags("Quality").RequireAuthorization(Policies.SupplyRead);

        lots.MapGet("", async (string? partCode, string? status, string? q, string? siteCode, LotService svc, ISiteContext sites, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(await sites.ResolveAsync(siteCode, ct), partCode, status, q, ct)))
            .WithSummary("Material lots (supplier users see only their own)");

        lots.MapGet("/{lotNumber}", async (string lotNumber, LotService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAsync(lotNumber, ct)));

        lots.MapPost("/{lotNumber}/inspections", async (string lotNumber, AddInspectionRequest req, LotService svc, CancellationToken ct) =>
                Results.Ok(await svc.AddInspectionAsync(lotNumber, req, ct)))
            .AddEndpointFilter<ValidationFilter<AddInspectionRequest>>()
            .RequireAuthorization(Policies.Quality)
            .WithSummary("Record an inspection; a failed result blocks the lot and raises an NCR");

        lots.MapPost("/{lotNumber}/block", async (string lotNumber, BlockLotRequest req, LotService svc, CancellationToken ct) =>
                Results.Ok(await svc.BlockAsync(lotNumber, req, ct)))
            .AddEndpointFilter<ValidationFilter<BlockLotRequest>>()
            .RequireAuthorization(Policies.Quality)
            .WithSummary("Quality block: raises MaterialLotBlocked, invalidates passports built from the lot");

        api.MapGet("/non-conformances", async (LotService svc, CancellationToken ct) => Results.Ok(await svc.NonConformancesAsync(ct)))
            .WithTags("Quality").RequireAuthorization(Policies.Trace);
    }
}
