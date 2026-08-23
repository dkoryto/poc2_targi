using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Risk;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Modules.Inbound;

public sealed class PurchaseOrderCommands(IAppDbContext db, ISupplierScope scope, ICurrentUser user, IDemoClock clock, IEventPublisher events, IAuditWriter audit, RiskAssessmentService risk, IPlanImpactEvaluator impact)
{
    private async Task<PurchaseOrderLine> LoadLineAsync(string poCode, Guid lineId, CancellationToken ct)
    {
        var line = await scope.Apply(db.PurchaseOrderLines)
            .Include(l => l.Part).Include(l => l.Documents).Include(l => l.Shipment).Include(l => l.History)
            .Include(l => l.PurchaseOrder).ThenInclude(p => p!.Supplier)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.PurchaseOrder!.Code == poCode, ct);
        return line ?? throw new NotFoundException("PurchaseOrderLine", $"{poCode}/{lineId}");
    }

    private static void CheckRowVersion(PurchaseOrderLine line, string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch == "*") return;
        var v = ifMatch.Trim('"', ' ', 'W', '/');
        if (v != line.RowVersion.ToString()) throw new PreconditionFailedException();
    }

    public async Task<PurchaseOrderLineDto> PatchLineAsync(string poCode, Guid lineId, PatchLineRequest req, string? ifMatch, CancellationToken ct)
    {
        var line = await LoadLineAsync(poCode, lineId, ct);
        CheckRowVersion(line, ifMatch);
        var before = Snapshot(line);
        var now = clock.UtcNow;
        var changes = new List<PurchaseOrderLineChange>();
        void Track(string field, string? oldV, string? newV)
        {
            if (oldV == newV) return;
            changes.Add(new PurchaseOrderLineChange { Id = Guid.NewGuid(), PurchaseOrderLineId = line.Id, Field = field, OldValue = oldV, NewValue = newV, ChangedBy = user.Username, Comment = req.Comment, CreatedAt = now, UpdatedAt = now });
        }

        var oldStatus = line.Status;
        if (req.Status is not null && Enum.TryParse<PurchaseOrderLineStatus>(req.Status, true, out var st) && st != line.Status)
        {
            Track("status", line.Status.ToString(), st.ToString());
            line.Status = st;
            if (st == PurchaseOrderLineStatus.Delivered) { line.DeliveredOn = clock.Today; line.DeliveredQuantity = line.Quantity; line.ProgressPercent = 100; }
            if (st == PurchaseOrderLineStatus.Shipped && line.Shipment is { } sh && sh.Status == ShipmentStatus.Advised) { sh.Status = ShipmentStatus.Departed; sh.ActualDeparture = now; }
        }
        if (req.ProgressPercent is { } p && p != line.ProgressPercent) { Track("progressPercent", line.ProgressPercent.ToString(), p.ToString()); line.ProgressPercent = p; }
        if (req.LotNumber is not null && req.LotNumber != line.LotNumber) { Track("lotNumber", line.LotNumber, req.LotNumber); line.LotNumber = req.LotNumber; }
        if (req.HeatNumber is not null && req.HeatNumber != line.HeatNumber) { Track("heatNumber", line.HeatNumber, req.HeatNumber); line.HeatNumber = req.HeatNumber; }
        if (req.ProducedOn is { } po && po != line.ProducedOn) { Track("producedOn", line.ProducedOn?.ToString("O"), po.ToString("O")); line.ProducedOn = po; }
        if (req.ExpiresOn is { } eo && eo != line.ExpiresOn) { Track("expiresOn", line.ExpiresOn?.ToString("O"), eo.ToString("O")); line.ExpiresOn = eo; }
        if (req.Quantity is { } qty && qty != line.Quantity) { Track("quantity", line.Quantity.ToString(), qty.ToString()); line.Quantity = qty; }
        if (req.Comment is not null) line.LastComment = req.Comment;
        var etaChanged = req.Eta is { } eta && eta != line.Eta;
        var oldEta = line.Eta;
        if (etaChanged) { Track("eta", line.Eta.ToString("O"), req.Eta!.Value.ToString("O")); line.Eta = req.Eta!.Value; if (line.Shipment is { } s) s.Eta = req.Eta.Value; }
        line.SupplierConfirmed = true;
        line.UpdatedAt = now;
        foreach (var c in changes) db.PurchaseOrderLineChanges.Add(c);

        var po2 = line.PurchaseOrder!;
        if (oldStatus != line.Status)
            events.Publish(new SupplierOrderStatusChanged(now, user.CorrelationId, po2.Code, line.Id, line.LineNo, line.Part!.Code, po2.Supplier!.Code, oldStatus.ToString(), line.Status.ToString(), line.ProgressPercent));
        if (etaChanged)
            events.Publish(new ShipmentEtaChanged(now, user.CorrelationId, po2.Code, line.Id, line.LineNo, line.Part!.Code, po2.Supplier!.Code, oldEta, line.Eta, line.RequiredDate, req.Comment));

        RiskSummaryDto riskDto;
        if (etaChanged || changes.Count > 0)
        {
            var overrides = new PlanOverrides();
            overrides.EtaByLineId[line.Id] = line.Eta;
            var planImpact = await impact.EvaluateAsync(po2.SiteId, overrides, ct);
            riskDto = await risk.AssessAndPersistAsync(line, etaChanged ? "EtaChanged" : "LineUpdated", planImpact, ct);
        }
        else riskDto = await CurrentRiskAsync(line, ct);

        audit.Write("PurchaseOrderLine.Patch", "PurchaseOrderLine", $"{po2.Code}/{line.LineNo}", line.Id, before, Snapshot(line));
        await db.SaveChangesAsync(ct);
        return PurchaseOrderQueries.ToLineDto(line, null, riskDto);
    }

    public async Task<EtaChangeResponse> ChangeEtaAsync(string poCode, Guid lineId, EtaChangeRequest req, string? ifMatch, CancellationToken ct)
    {
        var line = await LoadLineAsync(poCode, lineId, ct);
        CheckRowVersion(line, ifMatch);
        var before = Snapshot(line);
        var now = clock.UtcNow;
        var oldEta = line.Eta;
        var comment = string.IsNullOrWhiteSpace(req.Comment) ? req.Reason : $"{req.Reason}: {req.Comment}";
        if (req.Eta != line.Eta)
        {
            db.PurchaseOrderLineChanges.Add(new PurchaseOrderLineChange { Id = Guid.NewGuid(), PurchaseOrderLineId = line.Id, Field = "eta", OldValue = oldEta.ToString("O"), NewValue = req.Eta.ToString("O"), ChangedBy = user.Username, Comment = comment, CreatedAt = now, UpdatedAt = now });
            line.Eta = req.Eta;
            if (line.Shipment is { } s)
            {
                s.Eta = req.Eta;
                s.Events.Add(new ShipmentEvent { Id = Guid.NewGuid(), ShipmentId = s.Id, Type = ShipmentEventType.EtaUpdated, OccurredAt = now, Note = comment, RecordedBy = user.Username, CreatedAt = now, UpdatedAt = now });
            }
        }
        line.LastComment = comment;
        line.SupplierConfirmed = true;
        line.UpdatedAt = now;
        var po = line.PurchaseOrder!;
        events.Publish(new ShipmentEtaChanged(now, user.CorrelationId, po.Code, line.Id, line.LineNo, line.Part!.Code, po.Supplier!.Code, oldEta, line.Eta, line.RequiredDate, req.Reason));

        var overrides = new PlanOverrides();
        overrides.EtaByLineId[line.Id] = line.Eta;
        var planImpact = await impact.EvaluateAsync(po.SiteId, overrides, ct);
        var riskDto = await risk.AssessAndPersistAsync(line, "EtaChanged", planImpact, ct);
        audit.Write("PurchaseOrderLine.EtaChange", "PurchaseOrderLine", $"{po.Code}/{line.LineNo}", line.Id, before, Snapshot(line));
        await db.SaveChangesAsync(ct);

        var endangered = scope.IsRestricted ? riskDto.EndangeredOrders.Select(e => e with { ProductCode = "", Priority = 0 }).ToList() : riskDto.EndangeredOrders;
        return new EtaChangeResponse(PurchaseOrderQueries.ToLineDto(line, null, riskDto with { EndangeredOrders = endangered }), riskDto with { EndangeredOrders = endangered }, endangered, planImpact.Evaluation.Kpi.DowntimeHours);
    }

    public async Task<LineImpactDto> ImpactAsync(string poCode, Guid lineId, CancellationToken ct)
    {
        var line = await LoadLineAsync(poCode, lineId, ct);
        var planImpact = await impact.EvaluateAsync(line.PurchaseOrder!.SiteId, null, ct);
        var (result, endangered) = await risk.ComputeAsync(line, planImpact, ct);
        var dto = RiskAssessmentService.ToDto(result, endangered, clock.UtcNow);
        if (scope.IsRestricted)
        {
            var masked = endangered.Select(e => new EndangeredOrderDto(e.OrderCode, "", 0, e.RequiredOn, e.Shortage, e.AvailableOn, e.LatenessDays)).ToList();
            return new LineImpactDto(dto with { EndangeredOrders = masked }, masked, masked.Count, planImpact.Evaluation.Kpi.DowntimeHours, true);
        }
        return new LineImpactDto(dto, endangered, endangered.Count, planImpact.Evaluation.Kpi.DowntimeHours, false);
    }

    private async Task<RiskSummaryDto> CurrentRiskAsync(PurchaseOrderLine line, CancellationToken ct)
    {
        var a = await db.RiskAssessments.AsNoTracking().Where(r => r.PurchaseOrderLineId == line.Id).OrderByDescending(r => r.AssessedAt).FirstOrDefaultAsync(ct);
        return a is null ? new RiskSummaryDto(line.RiskScore, line.RiskCategory.ToString(), [], [], [], line.UpdatedAt) : RiskAssessmentService.FromAssessment(a);
    }

    private static object Snapshot(PurchaseOrderLine l) => new { l.Status, l.ProgressPercent, l.LotNumber, l.HeatNumber, l.ProducedOn, l.ExpiresOn, l.Quantity, l.Eta, l.RiskScore, RiskCategory = l.RiskCategory.ToString() };
}
