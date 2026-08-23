using Dspc.Api.Auth;
using Dspc.Application.Modules.Passports;
using Dspc.Application.Modules.Sites;

namespace Dspc.Api.Endpoints;

public static class PassportEndpoints
{
    public static void MapPassportEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/passports").WithTags("Passports").RequireAuthorization(Policies.Trace);

        g.MapGet("", async (string? status, string? q, string? siteCode, PassportService svc, ISiteContext sites, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(await sites.ResolveAsync(siteCode, ct), status, q, ct)));

        g.MapGet("/{serial}", async (string serial, PassportService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetAsync(serial, ct)))
            .WithSummary("Passport with DQP-01 completeness, components, inspections, deviations and versions");

        g.MapPost("/{serial}/approve", async (string serial, PassportService svc, CancellationToken ct) =>
                Results.Ok(await svc.ApproveAsync(serial, ct)))
            .RequireAuthorization(Policies.Quality)
            .WithSummary("Quality inspector approval — refused (422) while anything mandatory is missing");

        g.MapPost("/{serial}/generate", async (string serial, PassportService svc, CancellationToken ct) =>
                Results.Ok(await svc.GenerateAsync(serial, ct)))
            .RequireAuthorization(Policies.Quality)
            .WithSummary("Generate a versioned PDF (QR + SHA-256); 422 with the missing list when incomplete");

        g.MapGet("/{serial}/versions/{version:int}/pdf", async (string serial, int version, PassportService svc, HttpContext http, CancellationToken ct) =>
        {
            var file = await svc.DownloadAsync(serial, version, ct);
            http.Response.Headers.ContentDisposition = $"attachment; filename=\"{file.FileName}\"";
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).WithSummary("Download a passport version (streamed from object storage through the API)");

        g.MapGet("/{serial}/qr", async (string serial, PassportService svc, CancellationToken ct) =>
                Results.File(await svc.QrAsync(serial, ct), "image/png"))
            .WithSummary("QR code pointing at the local passport record");
    }
}
