using Dspc.Api.Auth;
using Dspc.Api.Middleware;
using Dspc.Application.Modules.Documents;
using Dspc.Application.Modules.Inbound;
using Dspc.Application.Modules.Sites;
using Microsoft.AspNetCore.Mvc;

namespace Dspc.Api.Endpoints;

public static class InboundEndpoints
{
    public static void MapInboundEndpoints(this RouteGroupBuilder api)
    {
        var po = api.MapGroup("/purchase-orders").WithTags("Inbound").RequireAuthorization(Policies.SupplyRead);
        po.MapGet("", async ([AsParameters] PurchaseOrderFilter filter, PurchaseOrderQueries q, ISiteContext sites, CancellationToken ct) =>
            {
                // Resolve through the site context so an unknown plant is a 404 and an out-of-reach one a 403,
                // rather than silently returning an empty list.
                var site = await sites.ResolveSiteAsync(filter.SiteCode, ct);
                return Results.Ok(await q.ListAsync(filter with { SiteCode = site.Code }, ct));
            })
            .WithSummary("Purchase orders (supplier users: own organisation only)");
        po.MapGet("/{code}", async (string code, PurchaseOrderQueries q, HttpResponse res, CancellationToken ct) =>
        {
            var dto = await q.GetAsync(code, ct);
            res.Headers.ETag = $"\"{dto.RowVersion}\"";
            return Results.Ok(dto);
        });
        po.MapPatch("/{code}/lines/{lineId:guid}", async (string code, Guid lineId, PatchLineRequest req, [FromHeader(Name = "If-Match")] string? ifMatch, PurchaseOrderCommands cmd, HttpResponse res, CancellationToken ct) =>
        {
            var dto = await cmd.PatchLineAsync(code, lineId, req, ifMatch, ct);
            res.Headers.ETag = $"\"{dto.RowVersion}\"";
            return Results.Ok(dto);
        }).AddEndpointFilter<ValidationFilter<PatchLineRequest>>().RequireAuthorization(Policies.SupplyWrite).WithSummary("Supplier status/progress/lot/ETA update (optimistic concurrency via If-Match)");
        po.MapPost("/{code}/lines/{lineId:guid}/eta", async (string code, Guid lineId, EtaChangeRequest req, [FromHeader(Name = "If-Match")] string? ifMatch, PurchaseOrderCommands cmd, HttpResponse res, CancellationToken ct) =>
        {
            var dto = await cmd.ChangeEtaAsync(code, lineId, req, ifMatch, ct);
            res.Headers.ETag = $"\"{dto.Line.RowVersion}\"";
            return Results.Ok(dto);
        }).AddEndpointFilter<ValidationFilter<EtaChangeRequest>>().RequireAuthorization(Policies.SupplyWrite).WithSummary("Change ETA → re-score risk → endangered orders → live event");
        po.MapGet("/{code}/lines/{lineId:guid}/impact", async (string code, Guid lineId, PurchaseOrderCommands cmd, CancellationToken ct) => Results.Ok(await cmd.ImpactAsync(code, lineId, ct)))
            .WithSummary("Production impact of the line (suppliers get order codes/counts only)");

        var sh = api.MapGroup("/shipments").WithTags("Inbound").RequireAuthorization(Policies.SupplyRead);
        sh.MapGet("", async (string? status, string? supplierCode, string? siteCode, ShipmentService svc, ISiteContext sites, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(await sites.ResolveAsync(siteCode, ct), status, supplierCode, ct)));
        sh.MapGet("/{code}", async (string code, ShipmentService svc, CancellationToken ct) => Results.Ok(await svc.GetAsync(code, ct)));
        sh.MapPost("", async (CreateShipmentRequest req, ShipmentService svc, CancellationToken ct) => Results.Created($"/api/v1/shipments", await svc.CreateAsync(req, ct)))
            .AddEndpointFilter<ValidationFilter<CreateShipmentRequest>>().RequireAuthorization(Policies.SupplyWrite).WithSummary("Delivery advice (awizacja)");
        sh.MapPost("/{code}/events", async (string code, AddShipmentEventRequest req, ShipmentService svc, CancellationToken ct) => Results.Ok(await svc.AddEventAsync(code, req, ct)))
            .AddEndpointFilter<ValidationFilter<AddShipmentEventRequest>>().RequireAuthorization(Policies.SupplyWrite);

        var le = api.MapGroup("/logistics-events").WithTags("Inbound").RequireAuthorization(Policies.Inbound);
        le.MapGet("", async (bool? activeOnly, LogisticsEventService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(activeOnly ?? false, ct)));
        le.MapPost("", async (CreateLogisticsEventRequest req, LogisticsEventService svc, CancellationToken ct) => Results.Created("/api/v1/logistics-events", await svc.CreateAsync(req, ct)))
            .AddEndpointFilter<ValidationFilter<CreateLogisticsEventRequest>>().RequireAuthorization(Policies.InboundWrite).WithSummary("Local logistics event simulator (border delay, port disruption, weather, …)");
        le.MapPost("/{id:guid}/resolve", async (Guid id, LogisticsEventService svc, CancellationToken ct) => Results.Ok(await svc.ResolveAsync(id, ct))).RequireAuthorization(Policies.InboundWrite);
    }
}

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/documents").WithTags("Documents").RequireAuthorization(Policies.SupplyRead).DisableAntiforgery();
        g.MapGet("", async (Guid? poLineId, string? lotNumber, DocumentService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(poLineId, lotNumber, ct)));
        g.MapPost("", async (HttpRequest req, DocumentService svc, FluentValidation.IValidator<UploadDocumentRequest> validator, CancellationToken ct) =>
        {
            if (!req.HasFormContentType) throw new Application.Common.ValidationException(new Dictionary<string, string[]> { ["file"] = ["multipart/form-data expected"] });
            var form = await req.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? throw new Application.Common.ValidationException(new Dictionary<string, string[]> { ["file"] = ["File is required."] });
            // A value that is present but unparseable is a different failure from one that is
            // absent: report it against its own field rather than letting it fall through as null,
            // where the caller would be told the document references nothing at all.
            var bindingErrors = new Dictionary<string, string[]>();
            Guid? poLineId = null;
            var rawPoLineId = form["poLineId"].ToString();
            if (!string.IsNullOrWhiteSpace(rawPoLineId))
            {
                if (Guid.TryParse(rawPoLineId, out var parsedPoLineId)) poLineId = parsedPoLineId;
                else bindingErrors["poLineId"] = ["Must be a GUID."];
            }
            DateOnly? issuedOn = null;
            var rawIssuedOn = form["issuedOn"].ToString();
            if (!string.IsNullOrWhiteSpace(rawIssuedOn))
            {
                if (DateOnly.TryParse(rawIssuedOn, out var parsedIssuedOn)) issuedOn = parsedIssuedOn;
                else bindingErrors["issuedOn"] = ["Must be a date (yyyy-MM-dd)."];
            }
            if (bindingErrors.Count > 0) throw new Application.Common.ValidationException(bindingErrors);

            var dto = new UploadDocumentRequest(form["type"].ToString(), poLineId, NullIfEmpty(form["lotNumber"]), NullIfEmpty(form["heatNumber"]),
                form["documentNumber"].ToString(), issuedOn);
            var vr = await validator.ValidateAsync(dto, ct);
            if (!vr.IsValid) throw new Application.Common.ValidationException(Application.Common.ValidationErrors.ToProblemDetails(vr.Errors));
            await using var stream = file.OpenReadStream();
            var result = await svc.UploadAsync(dto, file.FileName, file.Length, stream, ct);
            return Results.Created($"/api/v1/documents/{result.Id}", result);
        }).RequireAuthorization(Policies.SupplyWrite).RequireRateLimiting("upload").WithSummary("Upload certificate/report (pdf/png/jpg ≤ 10 MB; MIME sniffed; scanner adapter)");
        g.MapGet("/{id:guid}/download", async (Guid id, DocumentService svc, CancellationToken ct) =>
        {
            var dl = await svc.DownloadAsync(id, ct);
            return dl is null ? Results.NotFound() : Results.File(dl.Content, dl.ContentType, dl.FileName);
        });
        g.MapPost("/{id:guid}/verify", async (Guid id, VerifyDocumentRequest req, DocumentService svc, CancellationToken ct) => Results.Ok(await svc.VerifyAsync(id, req, ct)))
            .AddEndpointFilter<ValidationFilter<VerifyDocumentRequest>>().RequireAuthorization(Policies.Quality);
        g.MapPost("/{id:guid}/ai-extract", async (Guid id, AiExtractionService svc, CancellationToken ct) =>
                svc.Enabled ? Results.Ok(await svc.ExtractAsync(id, ct)) : Results.NotFound())
            .RequireAuthorization(Policies.Quality).RequireRateLimiting("upload")
            .WithSummary("Optional local-LLM field extraction (LocalAi:Enabled). Proposal only — a human accepts it via /verify.");
    }

    private static string? NullIfEmpty(Microsoft.Extensions.Primitives.StringValues v) => string.IsNullOrWhiteSpace(v) ? null : v.ToString();
}
