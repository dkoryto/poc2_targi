using System.Text;
using Dspc.Api.Auth;
using Dspc.Application.Modules.Audit;
using Dspc.Application.Modules.Traceability;

namespace Dspc.Api.Endpoints;

public static class TraceEndpoints
{
    public static void MapTraceEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/trace").WithTags("Traceability").RequireAuthorization(Policies.Trace);

        g.MapGet("/search", async (string? q, TraceQueries svc, CancellationToken ct) =>
                Results.Ok(await svc.SearchAsync(q ?? "", ct)))
            .WithSummary("Search serials, lots, heats, purchase orders, production orders and documents");

        g.MapGet("/serials/{serial}", async (string serial, TraceQueries svc, CancellationToken ct) =>
                Results.Ok(await svc.SerialAsync(serial, ct)))
            .WithSummary("Trace-back: serial → order → operations → lots → purchase order → supplier → certificates");

        g.MapGet("/lots/{lotNumber}/forward", async (string lotNumber, TraceQueries svc, CancellationToken ct) =>
                Results.Ok(await svc.LotForwardAsync(lotNumber, ct)))
            .WithSummary("Trace-forward: lot → production orders → serials → passports");

        g.MapGet("/audit", async (string? entity, string? code, string? user, DateTime? from, DateTime? to, int? page, int? pageSize, string? format,
            AuditQueries q, CancellationToken ct) =>
        {
            var filter = new AuditFilter(entity, code, user, from, to, page ?? 1, pageSize ?? 50);
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
                return Results.File(Encoding.UTF8.GetBytes(await q.ExportCsvAsync(filter, ct)), "text/csv", $"trace-audit-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
            return Results.Ok(await q.ListAsync(filter, ct));
        }).WithSummary("Audit history for a traced entity (?format=csv to export)");
    }
}
