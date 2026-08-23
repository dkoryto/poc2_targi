using Dspc.Application.Abstractions;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Quality;

public sealed record LotForwardImpact(
    string LotNumber,
    IReadOnlyList<string> OrderCodes,
    IReadOnlyList<(string OrderCode, string Relation, string Status)> Orders,
    IReadOnlyList<(string Serial, string OrderCode, string ProductCode)> SerialDetails,
    IReadOnlyList<string> Serials,
    IReadOnlyList<(string Serial, string Status)> Passports,
    IReadOnlyList<string> PassportSerials);

/// <summary>
/// Single source of truth for both trace directions: forward (lot → orders/serials/passports) and back (serial →
/// lots → purchase orders → suppliers) are derived from the same <see cref="MaterialConsumption"/> and
/// <see cref="Reservation"/> rows, so the two views can never disagree. <see cref="TraceabilityLink"/> rows are a
/// denormalised index maintained from the same data (used by search and audit export).
/// </summary>
public sealed class TraceabilityIndex(IAppDbContext db)
{
    public async Task<LotForwardImpact> ForwardAsync(string lotNumber, CancellationToken ct)
    {
        var lot = await db.MaterialLots.AsNoTracking().FirstOrDefaultAsync(l => l.LotNumber == lotNumber, ct);
        if (lot is null) return new LotForwardImpact(lotNumber, [], [], [], [], [], []);

        var consumed = await db.MaterialConsumptions.AsNoTracking()
            .Include(c => c.ProductionOrder).ThenInclude(o => o!.Product)
            .Include(c => c.ProductSerial)
            .Where(c => c.MaterialLotId == lot.Id)
            .ToListAsync(ct);

        var reservedOrders = await db.Reservations.AsNoTracking()
            .Include(r => r.ProductionOrder).ThenInclude(o => o!.Product)
            .Where(r => r.MaterialLotId == lot.Id)
            .Select(r => r.ProductionOrder!)
            .Distinct()
            .ToListAsync(ct);

        // group by business code, not by entity: AsNoTracking does not resolve identities, so the same order arrives as
        // several instances across consumption rows
        var orders = new List<(string OrderCode, string Relation, string Status)>();
        foreach (var g in consumed.Where(c => c.ProductionOrder is not null).GroupBy(c => c.ProductionOrder!.Code).OrderBy(g => g.Key))
            orders.Add((g.Key, "Consumed", g.First().ProductionOrder!.Status.ToString()));
        foreach (var o in reservedOrders.DistinctBy(o => o.Code).Where(o => orders.All(x => x.OrderCode != o.Code)).OrderBy(o => o.Code))
            orders.Add((o.Code, "Reserved", o.Status.ToString()));

        var serialDetails = consumed.Where(c => c.ProductSerial is not null)
            .Select(c => (c.ProductSerial!.SerialNumber, OrderCode: c.ProductionOrder?.Code ?? "", ProductCode: c.ProductionOrder?.Product?.Code ?? ""))
            .DistinctBy(x => x.SerialNumber)
            .OrderBy(x => x.SerialNumber)
            .ToList();

        var serialIds = consumed.Where(c => c.ProductSerialId is not null).Select(c => c.ProductSerialId!.Value).Distinct().ToList();
        var passports = await db.Passports.AsNoTracking().Include(p => p.ProductSerial)
            .Where(p => serialIds.Contains(p.ProductSerialId))
            .Select(p => new { p.ProductSerial!.SerialNumber, p.Status })
            .ToListAsync(ct);
        var passportList = passports.Select(p => (p.SerialNumber, Status: p.Status.ToString())).OrderBy(p => p.SerialNumber).ToList();

        return new LotForwardImpact(
            lot.LotNumber,
            orders.Select(o => o.OrderCode).ToList(),
            orders,
            serialDetails,
            serialDetails.Select(s => s.SerialNumber).ToList(),
            passportList,
            passportList.Select(p => p.SerialNumber).ToList());
    }

    /// <summary>Lots consumed into one serial (trace-back leaf set), ordered by part code.</summary>
    public async Task<IReadOnlyList<MaterialConsumption>> ConsumptionsForSerialAsync(Guid serialId, CancellationToken ct) =>
        await db.MaterialConsumptions.AsNoTracking()
            .Include(c => c.MaterialLot).ThenInclude(l => l!.Part)
            .Include(c => c.MaterialLot).ThenInclude(l => l!.Supplier)
            .Include(c => c.MaterialLot).ThenInclude(l => l!.PurchaseOrderLine).ThenInclude(pl => pl!.PurchaseOrder)
            .Where(c => c.ProductSerialId == serialId)
            .OrderBy(c => c.MaterialLot!.Part!.Code)
            .ToListAsync(ct);

    /// <summary>
    /// Rebuilds the denormalised <see cref="TraceabilityLink"/> index from consumptions, serials and inbound rows.
    /// Runs after seeding and after any consumption change; cheap enough for the demo dataset.
    /// </summary>
    public async Task<int> RebuildAsync(CancellationToken ct)
    {
        var existing = await db.TraceabilityLinks.ToListAsync(ct);
        var links = new List<TraceabilityLink>();
        void Add(TraceLinkKind kind, string fromType, Guid fromId, string fromCode, string toType, Guid toId, string toCode) =>
            links.Add(new TraceabilityLink { Id = Guid.NewGuid(), Kind = kind, FromType = fromType, FromId = fromId, FromCode = fromCode, ToType = toType, ToId = toId, ToCode = toCode });

        var pos = await db.PurchaseOrders.AsNoTracking().Include(p => p.Supplier).Include(p => p.Lines).ThenInclude(l => l.Part).ToListAsync(ct);
        foreach (var po in pos)
        {
            if (po.Supplier is not null) Add(TraceLinkKind.SupplierToPurchaseOrder, "Supplier", po.SupplierId, po.Supplier.Code, "PurchaseOrder", po.Id, po.Code);
            foreach (var line in po.Lines)
            {
                Add(TraceLinkKind.PurchaseOrderToLine, "PurchaseOrder", po.Id, po.Code, "PurchaseOrderLine", line.Id, $"{po.Code}/{line.LineNo}");
                if (line.ShipmentId is { } sid)
                {
                    var shipmentCode = await db.Shipments.AsNoTracking().Where(s => s.Id == sid).Select(s => s.Code).FirstOrDefaultAsync(ct);
                    if (shipmentCode is not null) Add(TraceLinkKind.LineToShipment, "PurchaseOrderLine", line.Id, $"{po.Code}/{line.LineNo}", "Shipment", sid, shipmentCode);
                }
            }
        }

        var lots = await db.MaterialLots.AsNoTracking().Include(l => l.PurchaseOrderLine).ThenInclude(pl => pl!.PurchaseOrder).ToListAsync(ct);
        foreach (var lot in lots.Where(l => l.PurchaseOrderLine is not null))
            Add(TraceLinkKind.LineToLot, "PurchaseOrderLine", lot.PurchaseOrderLineId!.Value, $"{lot.PurchaseOrderLine!.PurchaseOrder?.Code}/{lot.PurchaseOrderLine.LineNo}", "MaterialLot", lot.Id, lot.LotNumber);

        foreach (var doc in await db.QualityDocuments.AsNoTracking().Where(d => d.MaterialLotId != null).ToListAsync(ct))
            Add(TraceLinkKind.LotToDocument, "MaterialLot", doc.MaterialLotId!.Value, doc.LotNumber ?? "", "QualityDocument", doc.Id, doc.DocumentNumber);

        foreach (var i in await db.QualityInspections.AsNoTracking().Include(i => i.MaterialLot).Where(i => i.MaterialLotId != null).ToListAsync(ct))
            Add(TraceLinkKind.LotToInspection, "MaterialLot", i.MaterialLotId!.Value, i.MaterialLot!.LotNumber, "QualityInspection", i.Id, i.Code);

        var consumptions = await db.MaterialConsumptions.AsNoTracking().Include(c => c.MaterialLot).Include(c => c.ProductionOrder).Include(c => c.ProductSerial).ToListAsync(ct);
        foreach (var c in consumptions)
        {
            Add(TraceLinkKind.LotToConsumption, "MaterialLot", c.MaterialLotId, c.MaterialLot?.LotNumber ?? "", "MaterialConsumption", c.Id, c.Id.ToString("N")[..8]);
            Add(TraceLinkKind.ConsumptionToOrder, "MaterialConsumption", c.Id, c.Id.ToString("N")[..8], "ProductionOrder", c.ProductionOrderId, c.ProductionOrder?.Code ?? "");
            if (c.ProductSerial is not null)
                Add(TraceLinkKind.OrderToSerial, "ProductionOrder", c.ProductionOrderId, c.ProductionOrder?.Code ?? "", "ProductSerial", c.ProductSerial.Id, c.ProductSerial.SerialNumber);
        }

        foreach (var p in await db.Passports.AsNoTracking().Include(p => p.ProductSerial).ToListAsync(ct))
            Add(TraceLinkKind.SerialToPassport, "ProductSerial", p.ProductSerialId, p.ProductSerial?.SerialNumber ?? "", "Passport", p.Id, p.ProductSerial?.SerialNumber ?? "");

        db.TraceabilityLinks.RemoveRange(existing);
        db.TraceabilityLinks.AddRange(links);
        await db.SaveChangesAsync(ct);
        return links.Count;
    }
}
