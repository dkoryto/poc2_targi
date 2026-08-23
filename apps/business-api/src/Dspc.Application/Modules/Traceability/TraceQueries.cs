using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Quality;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Traceability;

/// <summary>
/// Genealogy: <c>Supplier → PurchaseOrder → Line → Shipment → Lot → Inspection → Consumption → Order → Operation →
/// Serial → Passport</c>. Trace-back (serial → lots) and trace-forward (lot → serials) both read
/// <see cref="MaterialConsumption"/>, so the two directions are consistent by construction.
/// </summary>
public sealed class TraceQueries(IAppDbContext db, TraceabilityIndex index)
{
    public async Task<IReadOnlyList<TraceSearchHit>> SearchAsync(string q, CancellationToken ct)
    {
        var term = q.Trim();
        if (term.Length < 2) return [];
        var like = term.ToLower();
        var hits = new List<TraceSearchHit>();

        hits.AddRange(await db.ProductSerials.AsNoTracking().Include(s => s.Product).Where(s => s.SerialNumber.ToLower().Contains(like))
            .OrderBy(s => s.SerialNumber).Take(10)
            .Select(s => new TraceSearchHit("Serial", s.SerialNumber, s.Product!.NamePl)).ToListAsync(ct));

        hits.AddRange(await db.MaterialLots.AsNoTracking().Include(l => l.Part).Where(l => l.LotNumber.ToLower().Contains(like))
            .OrderBy(l => l.LotNumber).Take(10)
            .Select(l => new TraceSearchHit("Lot", l.LotNumber, $"{l.Part!.Code} · {l.Status}")).ToListAsync(ct));

        hits.AddRange(await db.MaterialLots.AsNoTracking().Include(l => l.Part).Where(l => l.HeatNumber != null && l.HeatNumber!.ToLower().Contains(like))
            .OrderBy(l => l.HeatNumber).Take(10)
            .Select(l => new TraceSearchHit("Heat", l.HeatNumber!, $"{l.Part!.Code} · {l.LotNumber}")).ToListAsync(ct));

        hits.AddRange(await db.PurchaseOrders.AsNoTracking().Include(p => p.Supplier).Where(p => p.Code.ToLower().Contains(like))
            .OrderBy(p => p.Code).Take(10)
            .Select(p => new TraceSearchHit("PurchaseOrder", p.Code, p.Supplier!.Name)).ToListAsync(ct));

        hits.AddRange(await db.ProductionOrders.AsNoTracking().Include(o => o.Product).Where(o => o.Code.ToLower().Contains(like))
            .OrderBy(o => o.Code).Take(10)
            .Select(o => new TraceSearchHit("Order", o.Code, o.Product!.NamePl)).ToListAsync(ct));

        hits.AddRange(await db.QualityDocuments.AsNoTracking().Where(d => d.DocumentNumber.ToLower().Contains(like))
            .OrderBy(d => d.DocumentNumber).Take(10)
            .Select(d => new TraceSearchHit("Document", d.DocumentNumber, d.Type.ToString())).ToListAsync(ct));

        return hits.Take(40).ToList();
    }

    public async Task<SerialTraceDto> SerialAsync(string serialNumber, CancellationToken ct)
    {
        var serial = await db.ProductSerials.AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.ProductionOrder).ThenInclude(o => o!.Operations).ThenInclude(op => op.WorkCenter)
            .Include(s => s.Passport)
            .FirstOrDefaultAsync(s => s.SerialNumber == serialNumber, ct)
            ?? throw new NotFoundException("ProductSerial", serialNumber);

        var bom = await db.BomVersions.AsNoTracking().Where(b => b.Id == serial.BomVersionId).Select(b => b.Version).FirstOrDefaultAsync(ct) ?? "";
        var consumptions = await index.ConsumptionsForSerialAsync(serial.Id, ct);
        var lotIds = consumptions.Select(c => c.MaterialLotId).Distinct().ToList();
        var documents = await db.QualityDocuments.AsNoTracking().Where(d => d.MaterialLotId != null && lotIds.Contains(d.MaterialLotId!.Value)).ToListAsync(ct);
        var inspections = await db.QualityInspections.AsNoTracking().Where(i => i.MaterialLotId != null && lotIds.Contains(i.MaterialLotId!.Value)).ToListAsync(ct);
        var serialInspections = await db.QualityInspections.AsNoTracking().Where(i => i.ProductSerialId == serial.Id).OrderBy(i => i.InspectedAt).ToListAsync(ct);
        var shipments = await db.Shipments.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s, ct);

        var components = consumptions.Select(c =>
        {
            var lot = c.MaterialLot!;
            var cert = documents.Where(d => d.MaterialLotId == lot.Id && d.Type is Domain.Common.DocumentType.MATERIAL_CERT or Domain.Common.DocumentType.DECLARATION_OF_CONFORMITY)
                .OrderByDescending(d => d.Status == Domain.Common.DocumentStatus.Accepted).FirstOrDefault();
            return new TraceComponentDto(lot.Part?.Code ?? "", lot.Part?.NamePl, lot.LotNumber, lot.HeatNumber, lot.Supplier?.Code ?? "", lot.Supplier?.Name,
                lot.CountryOfOrigin, cert?.Sha256, cert?.Id, lot.Status.ToString(), c.Quantity);
        }).GroupBy(c => c.LotNumber).Select(g => g.First() with { Quantity = g.Sum(x => x.Quantity) }).OrderBy(c => c.PartCode).ToList();

        // trace-back tree, rooted at the serial. Group by lot number, not by entity — AsNoTracking does not resolve
        // identities, so one lot can arrive as several instances across consumption rows.
        var lotNodes = consumptions.Where(c => c.MaterialLot is not null).GroupBy(c => c.MaterialLot!.LotNumber).Select(g =>
        {
            var lot = g.First().MaterialLot!;
            var children = new List<TraceNode>();
            foreach (var d in documents.Where(d => d.MaterialLotId == lot.Id).OrderBy(d => d.Type))
                children.Add(new TraceNode("Document", d.DocumentNumber, $"{d.Type}", d.Status.ToString(), [],
                    Meta(("sha256", d.Sha256), ("documentId", d.Id), ("issuedOn", d.IssuedOn?.ToString("yyyy-MM-dd")))));
            foreach (var i in inspections.Where(i => i.MaterialLotId == lot.Id).OrderBy(i => i.InspectedAt))
                children.Add(new TraceNode("Inspection", i.Code, i.Result.ToString(), i.Result.ToString(), [], Meta(("inspectedAt", i.InspectedAt), ("by", i.InspectedBy))));

            if (lot.PurchaseOrderLine is { } line)
            {
                var lineChildren = new List<TraceNode>();
                if (line.ShipmentId is { } sid && shipments.TryGetValue(sid, out var shipment))
                    lineChildren.Add(new TraceNode("Shipment", shipment.Code, shipment.Carrier ?? shipment.Code, shipment.Status.ToString(), [], Meta(("eta", shipment.Eta.ToString("yyyy-MM-dd")))));
                var supplierNode = new TraceNode("Supplier", lot.Supplier?.Code ?? "", lot.Supplier?.Name ?? "", null, [], Meta(("country", lot.CountryOfOrigin)));
                var poNode = new TraceNode("PurchaseOrder", line.PurchaseOrder?.Code ?? "", $"{line.PurchaseOrder?.Code}/{line.LineNo}", line.Status.ToString(),
                    [supplierNode, .. lineChildren], Meta(("lineNo", line.LineNo), ("eta", line.Eta.ToString("yyyy-MM-dd")), ("requiredDate", line.RequiredDate.ToString("yyyy-MM-dd"))));
                children.Add(poNode);
            }

            return new TraceNode("Lot", lot.LotNumber, $"{lot.Part?.Code} · {g.Sum(x => x.Quantity):0.##} {lot.Unit}", lot.Status.ToString(), children,
                Meta(("heatNumber", lot.HeatNumber), ("supplierCode", lot.Supplier?.Code), ("country", lot.CountryOfOrigin), ("quantity", g.Sum(x => x.Quantity))));
        }).OrderBy(n => n.Code).ToList();

        var operationNodes = (serial.ProductionOrder?.Operations ?? []).OrderBy(o => o.Sequence).Select(op =>
        {
            var used = consumptions.Where(c => c.OperationDefinitionId == op.Id).Select(c => c.MaterialLot!.LotNumber).Distinct().ToList();
            return new TraceNode("Operation", op.Code, $"{op.Sequence} · {op.NamePl}", op.Status.ToString(),
                lotNodes.Where(n => used.Contains(n.Code)).ToList(), Meta(("workCenter", op.WorkCenter?.Code), ("durationHours", op.DurationHours)));
        }).ToList();

        var unassigned = lotNodes.Where(n => !operationNodes.SelectMany(o => o.Children).Any(c => c.Code == n.Code)).ToList();

        var orderNode = new TraceNode("Order", serial.ProductionOrder?.Code ?? "", serial.Product?.NamePl ?? "", serial.ProductionOrder?.Status.ToString(),
            [.. operationNodes, .. unassigned], Meta(("dueDate", serial.ProductionOrder?.DueDate.ToString("yyyy-MM-dd")), ("quantity", serial.ProductionOrder?.Quantity)));

        var rootChildren = new List<TraceNode> { orderNode };
        foreach (var i in serialInspections)
            rootChildren.Add(new TraceNode("Inspection", i.Code, i.Result.ToString(), i.Result.ToString(), [], Meta(("inspectedAt", i.InspectedAt), ("by", i.InspectedBy))));
        if (serial.Passport is { } pp)
            rootChildren.Add(new TraceNode("Passport", serial.SerialNumber, $"DQP-01 v{pp.CurrentVersion}", pp.Status.ToString(), [], Meta(("version", pp.CurrentVersion))));

        var root = new TraceNode("Serial", serial.SerialNumber, serial.Product?.NamePl ?? "", serial.Status.ToString(), rootChildren,
            Meta(("productCode", serial.Product?.Code), ("bomVersion", bom), ("completedAt", serial.CompletedAt)));

        return new SerialTraceDto(serial.SerialNumber, serial.Product?.Code ?? "", serial.Product?.NamePl ?? "", serial.ProductionOrder?.Code ?? "",
            bom, serial.Status.ToString(), root, components, serial.Passport?.Status.ToString());
    }

    public async Task<LotForwardDto> LotForwardAsync(string lotNumber, CancellationToken ct)
    {
        var lot = await db.MaterialLots.AsNoTracking().Include(l => l.Part).Include(l => l.Supplier).FirstOrDefaultAsync(l => l.LotNumber == lotNumber, ct)
            ?? throw new NotFoundException("MaterialLot", lotNumber);
        var impact = await index.ForwardAsync(lotNumber, ct);
        return new LotForwardDto(
            LotService.ToSummary(lot),
            impact.Orders.Select(o => new LotForwardOrderDto(o.OrderCode, o.Status, o.Relation)).ToList(),
            impact.SerialDetails.Select(s => new LotForwardSerialDto(s.Serial, s.OrderCode, s.ProductCode)).ToList(),
            impact.Passports.Select(p => new LotForwardPassportDto(p.Serial, p.Status)).ToList());
    }

    private static IReadOnlyDictionary<string, object?> Meta(params (string Key, object? Value)[] pairs) =>
        pairs.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value);
}
