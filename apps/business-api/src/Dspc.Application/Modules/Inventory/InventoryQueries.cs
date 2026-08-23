using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Inventory;

public sealed record InboundRefDto(string PoLine, decimal Qty, DateOnly Eta, int RiskScore);
public sealed record LotRefDto(string LotNumber, string? HeatNumber, decimal Remaining, string Status, string SupplierCode);
public sealed record InventoryItemDto(string PartCode, string PartName, string PartNameEn, string Unit, string Category, int Criticality, decimal OnHand, decimal Reserved, decimal Blocked, decimal Free, decimal OpenDemand, IReadOnlyList<InboundRefDto> Inbound, IReadOnlyList<LotRefDto> Lots, int? CoverageDays, string Status);

public sealed class InventoryQueries(IAppDbContext db, IPlanImpactEvaluator impact, IDemoClock clock)
{
    public async Task<ListResult<InventoryItemDto>> ListAsync(string? partCode, CancellationToken ct)
    {
        var parts = await db.Parts.AsNoTracking().Where(p => partCode == null || p.Code == partCode).OrderBy(p => p.Code).ToListAsync(ct);
        var lots = await db.MaterialLots.AsNoTracking().Include(l => l.Supplier).ToListAsync(ct);
        var reservations = await db.Reservations.AsNoTracking().Where(r => !r.IsBlocked).GroupBy(r => r.PartId).Select(g => new { g.Key, Qty = g.Sum(r => r.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var inbound = await db.PurchaseOrderLines.AsNoTracking().Include(l => l.PurchaseOrder).Where(l => l.Status != PurchaseOrderLineStatus.Delivered).ToListAsync(ct);
        var plan = await impact.EvaluateAsync(null, ct);
        var demand = plan.Model.Request.Orders.SelectMany(o => o.Operations).SelectMany(o => o.MaterialRequirements).GroupBy(r => r.PartCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity), StringComparer.OrdinalIgnoreCase);
        var items = parts.Select(p =>
        {
            var pl = lots.Where(l => l.PartId == p.Id).ToList();
            var onHand = pl.Where(l => l.Status is MaterialLotStatus.Accepted or MaterialLotStatus.ConditionallyReleased).Sum(l => l.RemainingQuantity);
            var blocked = pl.Where(l => l.Status is MaterialLotStatus.Blocked or MaterialLotStatus.Recalled).Sum(l => l.RemainingQuantity);
            var reserved = reservations.GetValueOrDefault(p.Id, 0);
            var free = onHand - reserved;
            var open = demand.GetValueOrDefault(p.Code, 0);
            var inb = inbound.Where(l => l.PartId == p.Id).OrderBy(l => l.Eta).Select(l => new InboundRefDto($"{l.PurchaseOrder!.Code}/{l.LineNo}", l.Quantity - l.DeliveredQuantity, l.Eta, l.RiskScore)).ToList();
            int? coverage = null;
            if (open > 0 && free < open)
            {
                decimal acc = free; foreach (var i in inb) { acc += i.Qty; if (acc >= open) { coverage = i.Eta.DayNumber - clock.Today.DayNumber; break; } }
            }
            else if (open > 0) coverage = 999;
            var status = open == 0 ? "ok" : free >= open ? "ok" : (free + inb.Sum(i => i.Qty)) >= open ? "warn" : "critical";
            return new InventoryItemDto(p.Code, p.NamePl, p.NameEn, p.Unit, p.Category.ToString(), p.Criticality, onHand, reserved, blocked, free, open, inb,
                pl.OrderBy(l => l.LotNumber).Select(l => new LotRefDto(l.LotNumber, l.HeatNumber, l.RemainingQuantity, l.Status.ToString(), l.Supplier?.Code ?? "")).ToList(), coverage, status);
        }).ToList();
        return ListResult.Of(items);
    }
}
