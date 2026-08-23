using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Inbound;

public sealed class ShipmentService(IAppDbContext db, ISupplierScope scope, ICurrentUser user, IDemoClock clock, IEventPublisher events, IAuditWriter audit, RiskAssessmentService risk)
{
    public async Task<ListResult<ShipmentDto>> ListAsync(string? status, string? supplierCode, CancellationToken ct)
    {
        var q = scope.Apply(db.Shipments.AsNoTracking()).Include(s => s.Supplier).Include(s => s.PurchaseOrder).Include(s => s.Events).Include(s => s.Lines).ThenInclude(l => l.Part).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ShipmentStatus>(status, true, out var st)) q = q.Where(s => s.Status == st);
        if (!string.IsNullOrWhiteSpace(supplierCode)) q = q.Where(s => s.Supplier!.Code == supplierCode);
        var list = await q.OrderBy(s => s.Eta).ThenBy(s => s.Code).ToListAsync(ct);
        return ListResult.Of(list.Select(ToDto).ToList());
    }

    public async Task<ShipmentDto> GetAsync(string code, CancellationToken ct)
    {
        var s = await scope.Apply(db.Shipments.AsNoTracking()).Include(s => s.Supplier).Include(s => s.PurchaseOrder).Include(s => s.Events).Include(s => s.Lines).ThenInclude(l => l.Part)
            .FirstOrDefaultAsync(s => s.Code == code, ct) ?? throw new NotFoundException("Shipment", code);
        return ToDto(s);
    }

    public async Task<ShipmentDto> CreateAsync(CreateShipmentRequest req, CancellationToken ct)
    {
        var po = await scope.Apply(db.PurchaseOrders).Include(p => p.Supplier).Include(p => p.Lines).ThenInclude(l => l.Part).FirstOrDefaultAsync(p => p.Code == req.PoCode, ct)
            ?? throw new NotFoundException("PurchaseOrder", req.PoCode);
        var lines = po.Lines.Where(l => req.LineIds.Contains(l.Id)).ToList();
        if (lines.Count != req.LineIds.Count) throw new ValidationException(new Dictionary<string, string[]> { ["lineIds"] = ["One or more lines do not belong to this purchase order."] });
        if (lines.Any(l => l.ShipmentId is not null)) throw new ConflictException("One or more lines are already assigned to a shipment.");
        var now = clock.UtcNow;
        var count = await db.Shipments.CountAsync(ct) + 1;
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(), Code = $"SHP-{now:yyyy}-{count + 1000:0000}", SupplierId = po.SupplierId, PurchaseOrderId = po.Id, Status = ShipmentStatus.Advised,
            Carrier = req.Carrier, Vehicle = req.Vehicle, PlannedDeparture = DateTime.SpecifyKind(req.PlannedDeparture, DateTimeKind.Utc), Eta = req.Eta, Progress = 0, CreatedAt = now, UpdatedAt = now
        };
        shipment.Events.Add(new ShipmentEvent { Id = Guid.NewGuid(), ShipmentId = shipment.Id, Type = ShipmentEventType.Advised, OccurredAt = now, Note = "Awizacja utworzona", RecordedBy = user.Username, CreatedAt = now, UpdatedAt = now });
        db.Shipments.Add(shipment);
        foreach (var l in lines)
        {
            l.ShipmentId = shipment.Id; l.Shipment = shipment;
            if (l.Eta != req.Eta)
            {
                db.PurchaseOrderLineChanges.Add(new PurchaseOrderLineChange { Id = Guid.NewGuid(), PurchaseOrderLineId = l.Id, Field = "eta", OldValue = l.Eta.ToString("O"), NewValue = req.Eta.ToString("O"), ChangedBy = user.Username, Comment = $"Awizacja {shipment.Code}", CreatedAt = now, UpdatedAt = now });
                events.Publish(new ShipmentEtaChanged(now, user.CorrelationId, po.Code, l.Id, l.LineNo, l.Part!.Code, po.Supplier!.Code, l.Eta, req.Eta, l.RequiredDate, "Advice"));
                l.Eta = req.Eta;
            }
            if (l.Status < PurchaseOrderLineStatus.ReadyToShip) l.Status = PurchaseOrderLineStatus.ReadyToShip;
            l.PurchaseOrder = po;
            await risk.AssessAndPersistAsync(l, "ShipmentAdvised", null, ct);
        }
        audit.Write("Shipment.Create", "Shipment", shipment.Code, shipment.Id, null, new { ShipmentCode = shipment.Code, PoCode = po.Code, Lines = lines.Select(l => l.LineNo), req.Eta });
        await db.SaveChangesAsync(ct);
        shipment.Supplier = po.Supplier; shipment.PurchaseOrder = po;
        foreach (var l in lines) shipment.Lines.Add(l);
        return ToDto(shipment);
    }

    public async Task<ShipmentDto> AddEventAsync(string code, AddShipmentEventRequest req, CancellationToken ct)
    {
        var s = await scope.Apply(db.Shipments).Include(s => s.Supplier).Include(s => s.PurchaseOrder).Include(s => s.Events).Include(s => s.Lines).ThenInclude(l => l.Part)
            .FirstOrDefaultAsync(s => s.Code == code, ct) ?? throw new NotFoundException("Shipment", code);
        var now = clock.UtcNow;
        var type = Enum.Parse<ShipmentEventType>(req.Type, true);
        var before = new { s.Status, s.Progress };
        s.Events.Add(new ShipmentEvent { Id = Guid.NewGuid(), ShipmentId = s.Id, Type = type, OccurredAt = req.OccurredAt is { } o ? DateTime.SpecifyKind(o, DateTimeKind.Utc) : now, Note = req.Note, Location = req.Location, RecordedBy = user.Username, CreatedAt = now, UpdatedAt = now });
        switch (type)
        {
            case ShipmentEventType.Departed: s.Status = ShipmentStatus.Departed; s.ActualDeparture ??= now; s.Progress = Math.Max(s.Progress, 0.05); break;
            case ShipmentEventType.BorderCrossed: s.Status = ShipmentStatus.AtBorder; s.Progress = Math.Max(s.Progress, 0.6); break;
            case ShipmentEventType.Delayed: s.Status = ShipmentStatus.InTransit; break;
            case ShipmentEventType.Arrived: s.Status = ShipmentStatus.Arrived; s.ArrivedAt = now; s.Progress = 1; break;
            case ShipmentEventType.Received: s.Status = ShipmentStatus.Received; s.Progress = 1; foreach (var l in s.Lines) { l.Status = PurchaseOrderLineStatus.Delivered; l.DeliveredOn = clock.Today; l.DeliveredQuantity = l.Quantity; l.ProgressPercent = 100; } break;
            default: if (s.Status == ShipmentStatus.Departed) s.Status = ShipmentStatus.InTransit; break;
        }
        if (req.Progress is { } p) s.Progress = p;
        s.UpdatedAt = now;
        events.Publish(new ShipmentEventRecorded(now, user.CorrelationId, s.Code, type.ToString(), s.Status.ToString(), s.Progress));
        audit.Write("Shipment.Event", "Shipment", s.Code, s.Id, before, new { s.Status, s.Progress, Event = type.ToString() });
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public static ShipmentDto ToDto(Shipment s)
    {
        var max = s.Lines.OrderByDescending(l => l.RiskScore).FirstOrDefault();
        return new ShipmentDto(s.Code, s.PurchaseOrder?.Code ?? "", s.Supplier?.Code ?? "", s.Supplier?.Name ?? "", s.Status.ToString(), s.Carrier, s.Vehicle, s.PlannedDeparture, s.ActualDeparture, s.Eta, s.Lines.Count == 0 ? null : s.Lines.Min(l => l.RequiredDate), s.ArrivedAt, s.Progress,
            s.Lines.OrderBy(l => l.LineNo).Select(l => new ShipmentLineDto(l.Id, l.LineNo, l.Part?.Code ?? "", l.Part?.NamePl ?? "", l.Quantity, l.Part?.Unit ?? "", l.RequiredDate)).ToList(),
            s.Events.OrderByDescending(e => e.OccurredAt).Select(e => new ShipmentEventDto(e.Id, e.Type.ToString(), e.OccurredAt, e.Note, e.Location, e.RecordedBy)).ToList(),
            max?.RiskScore ?? 0, (max?.RiskCategory ?? RiskCategory.Low).ToString(), s.RowVersion.ToString());
    }
}

public sealed class LogisticsEventService(IAppDbContext db, ICurrentUser user, IDemoClock clock, IEventPublisher events, IAuditWriter audit, RiskAssessmentService risk)
{
    public async Task<ListResult<LogisticsEventDto>> ListAsync(bool activeOnly, CancellationToken ct)
    {
        var q = db.LogisticsRiskEvents.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(e => e.ResolvedAt == null);
        var list = await q.OrderByDescending(e => e.StartedAt).ToListAsync(ct);
        var suppliers = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, ct);
        var shipments = await db.Shipments.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, ct);
        return ListResult.Of(list.Select(e => ToDto(e, e.SupplierId is { } sid ? suppliers.GetValueOrDefault(sid) : null, e.ShipmentId is { } shid ? shipments.GetValueOrDefault(shid) : null)).ToList());
    }

    public async Task<LogisticsEventDto> CreateAsync(CreateLogisticsEventRequest req, CancellationToken ct)
    {
        var now = clock.UtcNow;
        Supplier? supplier = req.SupplierCode is null ? null : await db.Suppliers.FirstOrDefaultAsync(s => s.Code == req.SupplierCode, ct) ?? throw new NotFoundException("Supplier", req.SupplierCode);
        Shipment? shipment = req.ShipmentCode is null ? null : await db.Shipments.Include(s => s.Supplier).FirstOrDefaultAsync(s => s.Code == req.ShipmentCode, ct) ?? throw new NotFoundException("Shipment", req.ShipmentCode);
        supplier ??= shipment?.Supplier;
        var count = await db.LogisticsRiskEvents.CountAsync(ct) + 1;
        var e = new LogisticsRiskEvent
        {
            Id = Guid.NewGuid(), Code = $"LRE-{now:yyyy}-{count + 100:000}", Type = Enum.Parse<LogisticsEventType>(req.Type, true), Severity = Enum.Parse<EventSeverity>(req.Severity, true),
            SupplierId = supplier?.Id, ShipmentId = shipment?.Id, Region = req.Region ?? supplier?.Country, Description = req.Description, StartedAt = now, CreatedAt = now, UpdatedAt = now
        };
        db.LogisticsRiskEvents.Add(e);
        if (shipment is not null && e.Type is LogisticsEventType.BORDER_DELAY or LogisticsEventType.PORT_DISRUPTION or LogisticsEventType.WEATHER)
            shipment.Events.Add(new ShipmentEvent { Id = Guid.NewGuid(), ShipmentId = shipment.Id, Type = ShipmentEventType.Delayed, OccurredAt = now, Note = req.Description, RecordedBy = user.Username, CreatedAt = now, UpdatedAt = now });
        events.Publish(new LogisticsRiskEventRaised(now, user.CorrelationId, e.Code, e.Type.ToString(), e.Severity.ToString(), supplier?.Code, shipment?.Code, e.Description));
        audit.Write("LogisticsEvent.Create", "LogisticsRiskEvent", e.Code, e.Id, null, new { e.Type, e.Severity, supplier?.Code, ShipmentCode = shipment?.Code });

        // re-score affected lines
        var lines = await db.PurchaseOrderLines.Include(l => l.Part).Include(l => l.PurchaseOrder).ThenInclude(p => p!.Supplier)
            .Where(l => l.Status != PurchaseOrderLineStatus.Delivered && ((supplier != null && l.PurchaseOrder!.SupplierId == supplier.Id) || (shipment != null && l.ShipmentId == shipment.Id)))
            .ToListAsync(ct);
        await db.SaveChangesAsync(ct); // event persisted first so risk computation sees it
        foreach (var l in lines) await risk.AssessAndPersistAsync(l, "LogisticsEvent", null, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(e, supplier?.Code, shipment?.Code);
    }

    public async Task<LogisticsEventDto> ResolveAsync(Guid id, CancellationToken ct)
    {
        var e = await db.LogisticsRiskEvents.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("LogisticsRiskEvent", id.ToString());
        e.ResolvedAt = clock.UtcNow; e.UpdatedAt = e.ResolvedAt.Value;
        audit.Write("LogisticsEvent.Resolve", "LogisticsRiskEvent", e.Code, e.Id, new { Resolved = false }, new { Resolved = true });
        await db.SaveChangesAsync(ct);
        var lines = await db.PurchaseOrderLines.Include(l => l.Part).Include(l => l.PurchaseOrder).ThenInclude(p => p!.Supplier)
            .Where(l => l.Status != PurchaseOrderLineStatus.Delivered && ((e.SupplierId != null && l.PurchaseOrder!.SupplierId == e.SupplierId) || (e.ShipmentId != null && l.ShipmentId == e.ShipmentId))).ToListAsync(ct);
        foreach (var l in lines) await risk.AssessAndPersistAsync(l, "LogisticsEventResolved", null, ct);
        await db.SaveChangesAsync(ct);
        var supplierCode = e.SupplierId is { } sid ? await db.Suppliers.Where(s => s.Id == sid).Select(s => s.Code).FirstOrDefaultAsync(ct) : null;
        var shipmentCode = e.ShipmentId is { } shid ? await db.Shipments.Where(s => s.Id == shid).Select(s => s.Code).FirstOrDefaultAsync(ct) : null;
        return ToDto(e, supplierCode, shipmentCode);
    }

    private static LogisticsEventDto ToDto(LogisticsRiskEvent e, string? supplierCode, string? shipmentCode)
        => new(e.Id, e.Code, e.Type.ToString(), e.Severity.ToString(), supplierCode, shipmentCode, e.Region, e.Description, e.StartedAt, e.ResolvedAt, e.ResolvedAt is null);
}
