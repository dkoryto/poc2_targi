using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Common;
using Dspc.Domain.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dspc.Application.Modules.Dashboard;

public sealed record KpiDto(string Code, double Value, string Unit, double Trend, string Status, string DefinitionKey, string? Route);
public sealed record KpisDto(DateTime AsOf, IReadOnlyList<KpiDto> Items);

public sealed record MapSiteDto(string Code, string Name, double Lat, double Lon);
public sealed record MapSupplierDto(string Code, string Name, string Country, string City, double Lat, double Lon, int RiskScore, string RiskCategory, int ActiveShipments);
public sealed record MapShipmentDto(string Code, string PoCode, string SupplierCode, string SupplierName, string PartCode, string PartName, decimal Quantity, DateOnly Eta, DateOnly RequiredDate, string Status, int RiskScore, string RiskCategory, double Progress, double Lat, double Lon, IReadOnlyList<double[]> Route, IReadOnlyList<string> EndangeredOrders);
public sealed record MapDto(MapSiteDto Site, IReadOnlyList<MapSupplierDto> Suppliers, IReadOnlyList<MapShipmentDto> Shipments);

public sealed record HeatmapCellDto(string Row, string Col, int Score, int Count);
public sealed record HeatmapDto(IReadOnlyList<string> Rows, IReadOnlyList<string> Cols, IReadOnlyList<HeatmapCellDto> Cells);

public sealed record QualityStatusDto(Dictionary<string, int> Passports, Dictionary<string, int> Documents, int OpenNonConformances, int LotsBlocked, int ReadyForAcceptance, int SerialsInProgress, IReadOnlyList<QualityIssueDto> Issues);
public sealed record QualityIssueDto(string Kind, string Code, string Label, string Status, string? Route);

public sealed class DashboardQueries(IAppDbContext db, IPlanImpactEvaluator impact, IDemoClock clock, IOptions<RiskOptions> risk)
{
    public async Task<KpisDto> KpisAsync(CancellationToken ct)
    {
        var plan = await impact.EvaluateAsync(null, ct);
        var today = clock.Today;

        var openLines = await db.PurchaseOrderLines.AsNoTracking().Where(l => l.Status != PurchaseOrderLineStatus.Delivered).ToListAsync(ct);
        var highRisk = openLines.Count(l => l.RiskScore >= risk.Value.HighRiskThreshold);
        var highRiskPrev = openLines.Count(l => l.OriginalEta == l.Eta ? l.RiskScore >= risk.Value.HighRiskThreshold : false); // seeded state as "previous period"

        var delivered = await db.PurchaseOrderLines.AsNoTracking().Where(l => l.Status == PurchaseOrderLineStatus.Delivered && l.DeliveredOn != null && l.DeliveredOn >= today.AddDays(-90)).ToListAsync(ct);
        var otif = delivered.Count == 0 ? 100 : 100.0 * delivered.Count(l => l.DeliveredOn <= l.RequiredDate && l.DeliveredQuantity >= l.Quantity) / delivered.Count;
        var deliveredPrev = await db.PurchaseOrderLines.AsNoTracking().Where(l => l.Status == PurchaseOrderLineStatus.Delivered && l.DeliveredOn != null && l.DeliveredOn >= today.AddDays(-180) && l.DeliveredOn < today.AddDays(-90)).ToListAsync(ct);
        var otifPrev = deliveredPrev.Count == 0 ? otif : 100.0 * deliveredPrev.Count(l => l.DeliveredOn <= l.RequiredDate && l.DeliveredQuantity >= l.Quantity) / deliveredPrev.Count;

        var orders = plan.Evaluation.Orders;
        var readiness = orders.Count == 0 ? 100 : 100.0 * orders.Count(o => o.MaterialComplete) / orders.Count;
        var onTime = orders.Count == 0 ? 100 : 100.0 * orders.Count(o => o.LatenessDays == 0) / orders.Count;
        var baselineKpi = plan.Model.Baseline.KpiJson is null ? null : Common.Json.Deserialize<PlanKpi>(plan.Model.Baseline.KpiJson);
        var downtime = plan.Evaluation.Kpi.DowntimeHours;

        var passports = await db.Passports.AsNoTracking().ToListAsync(ct);
        var passportCompleteness = passports.Count == 0 ? 100 : 100.0 * passports.Count(p => p.Status is PassportStatus.Approved or PassportStatus.Generated) / passports.Count;

        var items = new List<KpiDto>
        {
            new("MATERIAL_READINESS", Math.Round(readiness, 1), "%", Math.Round(readiness - (baselineKpi is null ? readiness : 100.0 * (orders.Count - baselineKpi.OrdersWithShortage) / Math.Max(1, orders.Count)), 1), readiness >= 85 ? "ok" : readiness >= 70 ? "warn" : "critical", "kpi.materialReadiness", "/inventory"),
            new("OTIF", Math.Round(otif, 1), "%", Math.Round(otif - otifPrev, 1), otif >= 90 ? "ok" : otif >= 80 ? "warn" : "critical", "kpi.otif", "/suppliers"),
            new("HIGH_RISK_DELIVERIES", highRisk, "count", highRisk - highRiskPrev, highRisk == 0 ? "ok" : highRisk <= 3 ? "warn" : "critical", "kpi.highRiskDeliveries", "/supply?riskCategory=High"),
            new("PREDICTED_DOWNTIME_H", downtime, "h", downtime - (baselineKpi?.DowntimeHours ?? 0), downtime == 0 ? "ok" : downtime <= 16 ? "warn" : "critical", "kpi.predictedDowntime", "/planning"),
            new("ORDER_ON_TIME", Math.Round(onTime, 1), "%", Math.Round(onTime - (baselineKpi is null ? onTime : baselineKpi.OnTimeRate * 100), 1), onTime >= 95 ? "ok" : onTime >= 85 ? "warn" : "critical", "kpi.orderOnTime", "/planning"),
            new("PASSPORT_COMPLETENESS", Math.Round(passportCompleteness, 1), "%", 0, passportCompleteness >= 80 ? "ok" : passportCompleteness >= 50 ? "warn" : "critical", "kpi.passportCompleteness", "/passports"),
        };
        return new KpisDto(clock.UtcNow, items);
    }

    public async Task<MapDto> MapAsync(CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().OrderBy(s => s.Code).FirstAsync(ct);
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Code).ToListAsync(ct);
        var shipments = await db.Shipments.AsNoTracking().Include(s => s.Supplier).Include(s => s.PurchaseOrder).Include(s => s.Lines).ThenInclude(l => l.Part)
            .Where(s => s.Status != ShipmentStatus.Received && s.Status != ShipmentStatus.Cancelled).OrderBy(s => s.Eta).ToListAsync(ct);
        var openLines = await db.PurchaseOrderLines.AsNoTracking().Include(l => l.PurchaseOrder).Where(l => l.Status != PurchaseOrderLineStatus.Delivered).ToListAsync(ct);
        var lineIds = shipments.SelectMany(s => s.Lines.Select(l => l.Id)).ToList();
        var latest = await db.RiskAssessments.AsNoTracking().Where(r => lineIds.Contains(r.PurchaseOrderLineId)).GroupBy(r => r.PurchaseOrderLineId).Select(g => g.OrderByDescending(r => r.AssessedAt).First()).ToListAsync(ct);
        var endangeredByLine = latest.ToDictionary(r => r.PurchaseOrderLineId, r => (Common.Json.Deserialize<List<Common.EndangeredOrderDto>>(r.EndangeredOrdersJson) ?? new()).Select(e => e.OrderCode).ToList());

        var supplierDtos = suppliers.Select(s =>
        {
            var lines = openLines.Where(l => l.PurchaseOrder!.SupplierId == s.Id).ToList();
            var max = lines.Count == 0 ? 0 : lines.Max(l => l.RiskScore);
            return new MapSupplierDto(s.Code, s.Name, s.Country, s.City, s.Latitude, s.Longitude, max, RiskScoreCalculator.Categorize(max).ToString(), shipments.Count(sh => sh.SupplierId == s.Id));
        }).ToList();

        var shipmentDtos = shipments.Select(s =>
        {
            var sup = s.Supplier!;
            var route = Route(sup.Latitude, sup.Longitude, site.Latitude, site.Longitude);
            var idx = (int)Math.Round(Math.Clamp(s.Progress, 0, 1) * (route.Count - 1));
            var main = s.Lines.OrderByDescending(l => l.RiskScore).First();
            var endangered = s.Lines.SelectMany(l => endangeredByLine.GetValueOrDefault(l.Id, new())).Distinct().ToList();
            return new MapShipmentDto(s.Code, s.PurchaseOrder!.Code, sup.Code, sup.Name, main.Part!.Code, main.Part.NamePl, s.Lines.Sum(l => l.Quantity), s.Eta, s.Lines.Min(l => l.RequiredDate), s.Status.ToString(),
                main.RiskScore, main.RiskCategory.ToString(), s.Progress, route[idx][1], route[idx][0], route, endangered);
        }).ToList();
        return new MapDto(new MapSiteDto(site.Code, site.Name, site.Latitude, site.Longitude), supplierDtos, shipmentDtos);
    }

    /// <summary>Gently curved polyline [lon,lat] from supplier to site (great-circle-ish feel without external data).</summary>
    private static List<double[]> Route(double lat1, double lon1, double lat2, double lon2)
    {
        var pts = new List<double[]>();
        const int n = 24;
        var dx = lon2 - lon1; var dy = lat2 - lat1;
        var len = Math.Sqrt(dx * dx + dy * dy);
        var curve = Math.Min(1.5, len * 0.12);
        for (var i = 0; i <= n; i++)
        {
            var t = (double)i / n;
            var lon = lon1 + dx * t; var lat = lat1 + dy * t;
            var bulge = Math.Sin(Math.PI * t) * curve;
            // perpendicular offset
            if (len > 0) { lon += -dy / len * bulge; lat += dx / len * bulge; }
            pts.Add(new[] { Math.Round(lon, 4), Math.Round(lat, 4) });
        }
        return pts;
    }

    public async Task<HeatmapDto> HeatmapAsync(CancellationToken ct)
    {
        var lines = await db.PurchaseOrderLines.AsNoTracking().Include(l => l.Part).Include(l => l.PurchaseOrder).ThenInclude(p => p!.Supplier)
            .Where(l => l.Status != PurchaseOrderLineStatus.Delivered).ToListAsync(ct);
        var rows = lines.Select(l => l.PurchaseOrder!.Supplier!.Country).Distinct().OrderBy(c => c == "PL" ? "" : c).ToList();
        var cols = Enum.GetNames<PartCategory>().ToList();
        var cells = new List<HeatmapCellDto>();
        foreach (var r in rows)
            foreach (var c in cols)
            {
                var group = lines.Where(l => l.PurchaseOrder!.Supplier!.Country == r && l.Part!.Category.ToString() == c).ToList();
                cells.Add(new HeatmapCellDto(r, c, group.Count == 0 ? 0 : group.Max(l => l.RiskScore), group.Count));
            }
        return new HeatmapDto(rows, cols, cells);
    }

    public async Task<QualityStatusDto> QualityStatusAsync(CancellationToken ct)
    {
        var passports = await db.Passports.AsNoTracking().Include(p => p.ProductSerial).ToListAsync(ct);
        var docs = await db.QualityDocuments.AsNoTracking().Include(d => d.PurchaseOrderLine).ThenInclude(l => l!.PurchaseOrder).ToListAsync(ct);
        var ncr = await db.NonConformances.AsNoTracking().Where(n => n.Status != NonConformanceStatus.Closed).ToListAsync(ct);
        var lots = await db.MaterialLots.AsNoTracking().Where(l => l.Status == MaterialLotStatus.Blocked || l.Status == MaterialLotStatus.Recalled).ToListAsync(ct);
        var serials = await db.ProductSerials.AsNoTracking().CountAsync(s => s.Status == SerialStatus.InProduction, ct);
        var issues = new List<QualityIssueDto>();
        issues.AddRange(docs.Where(d => d.Status is DocumentStatus.Rejected or DocumentStatus.RequiresCompletion or DocumentStatus.Missing or DocumentStatus.Pending)
            .OrderBy(d => d.Status).Select(d => new QualityIssueDto("Document", d.DocumentNumber, $"{d.Type} · {(d.PurchaseOrderLine?.PurchaseOrder?.Code ?? d.LotNumber ?? "")}", d.Status.ToString(), d.PurchaseOrderLine?.PurchaseOrder is { } po ? $"/supply/orders/{po.Code}" : null)));
        issues.AddRange(lots.Select(l => new QualityIssueDto("Lot", l.LotNumber, l.BlockReason ?? "", l.Status.ToString(), $"/trace/lots/{l.LotNumber}")));
        issues.AddRange(ncr.Select(n => new QualityIssueDto("NonConformance", n.Code, n.Title, n.Status.ToString(), "/quality/non-conformances")));
        return new QualityStatusDto(
            Enum.GetNames<PassportStatus>().ToDictionary(s => char.ToLowerInvariant(s[0]) + s[1..], s => passports.Count(p => p.Status.ToString() == s)),
            Enum.GetNames<DocumentStatus>().ToDictionary(s => char.ToLowerInvariant(s[0]) + s[1..], s => docs.Count(d => d.Status.ToString() == s)),
            ncr.Count, lots.Count, passports.Count(p => p.Status is PassportStatus.Approved or PassportStatus.Generated), serials, issues.Take(12).ToList());
    }

    public async Task<GanttData> PlanAsync(CancellationToken ct) => (await impact.EvaluateAsync(null, ct)).Gantt;
}
