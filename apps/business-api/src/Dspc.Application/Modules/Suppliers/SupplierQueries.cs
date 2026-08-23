using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Suppliers;

public sealed record SupplierDto(string Code, string Name, string Country, string City, double Lat, double Lon, double Otif, double QualityScore, int RiskScore, string RiskCategory, int OpenOrders, int OpenLines, int ActiveShipments, int ActiveEvents, IReadOnlyList<string> Parts);
public sealed record SupplierPerformanceDto(DateOnly PeriodStart, DateOnly PeriodEnd, int DeliveredLines, int OnTimeInFullLines, int QualityRejections, double OtifPercent);
public sealed record SupplierDetailDto(SupplierDto Supplier, IReadOnlyList<SupplierPerformanceDto> Performance);

public sealed class SupplierQueries(IAppDbContext db, ISupplierScope scope)
{
    public async Task<ListResult<SupplierDto>> ListAsync(CancellationToken ct)
    {
        var suppliers = await scope.Apply(db.Suppliers.AsNoTracking()).Where(s => s.IsActive).OrderBy(s => s.Code).ToListAsync(ct);
        var ids = suppliers.Select(s => s.Id).ToList();
        var pos = await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines).Where(p => ids.Contains(p.SupplierId)).ToListAsync(ct);
        var shipments = await db.Shipments.AsNoTracking().Where(s => ids.Contains(s.SupplierId) && s.Status != ShipmentStatus.Received && s.Status != ShipmentStatus.Cancelled).GroupBy(s => s.SupplierId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var events = await db.LogisticsRiskEvents.AsNoTracking().Where(e => e.ResolvedAt == null && e.SupplierId != null).GroupBy(e => e.SupplierId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var parts = await db.Parts.AsNoTracking().Where(p => p.PrimarySupplierId != null).GroupBy(p => p.PrimarySupplierId!.Value).Select(g => new { g.Key, Codes = g.Select(p => p.Code).OrderBy(c => c).ToList() }).ToDictionaryAsync(x => x.Key, x => x.Codes, ct);
        var items = suppliers.Select(s =>
        {
            var myPos = pos.Where(p => p.SupplierId == s.Id).ToList();
            var openLines = myPos.SelectMany(p => p.Lines).Where(l => l.Status != PurchaseOrderLineStatus.Delivered).ToList();
            var risk = openLines.Count == 0 ? 0 : (int)Math.Round(openLines.Average(l => l.RiskScore));
            var maxRisk = openLines.Count == 0 ? 0 : openLines.Max(l => l.RiskScore);
            return new SupplierDto(s.Code, s.Name, s.Country, s.City, s.Latitude, s.Longitude, s.OtifPercent, s.QualityScore, maxRisk, Domain.Risk.RiskScoreCalculator.Categorize(maxRisk).ToString(),
                myPos.Count(p => p.Status is PurchaseOrderStatus.Open or PurchaseOrderStatus.PartiallyDelivered), openLines.Count, shipments.GetValueOrDefault(s.Id), events.GetValueOrDefault(s.Id), parts.GetValueOrDefault(s.Id, new List<string>()));
        }).ToList();
        return ListResult.Of(items);
    }

    public async Task<SupplierDetailDto> GetAsync(string code, CancellationToken ct)
    {
        var list = await ListAsync(ct);
        var s = list.Items.FirstOrDefault(x => x.Code == code) ?? throw new NotFoundException("Supplier", code);
        var id = await db.Suppliers.AsNoTracking().Where(x => x.Code == code).Select(x => x.Id).FirstAsync(ct);
        var perf = await db.SupplierPerformances.AsNoTracking().Where(p => p.SupplierId == id).OrderByDescending(p => p.PeriodStart)
            .Select(p => new SupplierPerformanceDto(p.PeriodStart, p.PeriodEnd, p.DeliveredLines, p.OnTimeInFullLines, p.QualityRejections, p.OtifPercent)).ToListAsync(ct);
        return new SupplierDetailDto(s, perf);
    }
}
