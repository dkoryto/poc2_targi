using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Domain.Events;
using Dspc.Domain.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dspc.Application.Modules.Risk;

public sealed class RiskOptions
{
    public const string Section = "Risk";
    public RiskWeights Weights { get; set; } = new();
    /// <summary>Score at/above which a planner notification is created.</summary>
    public int NotifyThreshold { get; set; } = 50;
    public int HighRiskThreshold { get; set; } = 50;
}

public sealed class RiskAssessmentService(IAppDbContext db, IOptions<RiskOptions> options, IPlanImpactEvaluator impact, IEventPublisher events, IDemoClock clock, ICurrentUser user)
{
    public RiskWeights Weights => options.Value.Weights;

    /// <summary>Computes the rule-based risk for a line (no persistence).</summary>
    public async Task<(RiskResult Result, IReadOnlyList<EndangeredOrderDto> Endangered)> ComputeAsync(PurchaseOrderLine line, PlanImpact? planImpact, CancellationToken ct)
    {
        var part = line.Part ?? await db.Parts.AsNoTracking().FirstAsync(p => p.Id == line.PartId, ct);
        var supplier = line.PurchaseOrder?.Supplier ?? await db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == line.PurchaseOrder!.SupplierId, ct);

        var requiredTypes = Json.Deserialize<List<DocumentType>>(part.RequiredDocumentTypesJson) ?? new();
        var docs = await db.QualityDocuments.AsNoTracking().Where(d => d.PurchaseOrderLineId == line.Id).ToListAsync(ct);
        var accepted = requiredTypes.Count(t => docs.Any(d => d.Type == t && d.Status == DocumentStatus.Accepted));

        // stock, demand and the plan are all plant-local: a line only competes with its own plant
        var siteId = line.PurchaseOrder?.SiteId ?? await db.PurchaseOrders.AsNoTracking().Where(p => p.Id == line.PurchaseOrderId).Select(p => p.SiteId).FirstAsync(ct);
        var lots = await db.MaterialLots.AsNoTracking().Where(l => l.PartId == part.Id && l.SiteId == siteId).ToListAsync(ct);
        var onHand = lots.Where(l => l.Status is MaterialLotStatus.Accepted or MaterialLotStatus.ConditionallyReleased).Sum(l => l.RemainingQuantity);
        var reserved = await db.Reservations.AsNoTracking().Where(r => r.PartId == part.Id && !r.IsBlocked && r.ProductionOrder!.SiteId == siteId).SumAsync(r => r.Quantity, ct);
        var free = onHand - reserved;

        planImpact ??= await impact.EvaluateAsync(siteId, null, ct);
        var openDemand = planImpact.Model.Request.Orders.SelectMany(o => o.Operations).SelectMany(o => o.MaterialRequirements)
            .Where(r => r.PartCode.Equals(part.Code, StringComparison.OrdinalIgnoreCase)).Sum(r => r.Quantity);

        var now = clock.UtcNow;
        var activeEvents = await db.LogisticsRiskEvents.AsNoTracking()
            .Where(e => e.ResolvedAt == null && (e.SupplierId == line.PurchaseOrder!.SupplierId || (line.ShipmentId != null && e.ShipmentId == line.ShipmentId)))
            .Select(e => e.Severity).ToListAsync(ct);

        var input = new RiskInput(
            DaysLate: line.Eta.DayNumber - line.RequiredDate.DayNumber,
            Criticality: part.Criticality,
            HasAlternativeSupplier: part.HasAlternativeSupplier,
            RequiredDocuments: requiredTypes.Count,
            AcceptedDocuments: accepted,
            SupplierOtifPercent: supplier.OtifPercent,
            OpenDemand: openDemand,
            FreeOnHand: free,
            ActiveEvents: activeEvents);
        var result = RiskScoreCalculator.Calculate(input, Weights);

        var endangered = EndangeredOrders(planImpact, part.Code, line);
        return (result, endangered);
    }

    /// <summary>Orders whose material-bound operations need this part and are shifted/late or would be starved by this line's ETA.</summary>
    public static IReadOnlyList<EndangeredOrderDto> EndangeredOrders(PlanImpact planImpact, string partCode, PurchaseOrderLine line)
    {
        var list = new List<EndangeredOrderDto>();
        foreach (var o in planImpact.Model.Request.Orders)
        {
            var res = planImpact.Evaluation.Orders.First(r => r.OrderCode == o.Code);
            var ops = o.Operations.Where(op => op.MaterialRequirements.Any(r => r.PartCode.Equals(partCode, StringComparison.OrdinalIgnoreCase) && r.Quantity > 0)).ToList();
            if (ops.Count == 0) continue;
            var shortage = res.Shortages.FirstOrDefault(s => s.PartCode.Equals(partCode, StringComparison.OrdinalIgnoreCase));
            var waiting = ops.Any(op => planImpact.Evaluation.Operations.First(x => x.OperationCode == op.Code).WaitingForMaterial);
            var requiredOn = ops.Select(op => op.BaselineStart).Where(d => d.HasValue).Select(d => DateOnly.FromDateTime(d!.Value)).DefaultIfEmpty(o.ReleaseDate).Min();
            var needsLine = requiredOn < line.Eta && shortage is not null;
            if (shortage is null && !waiting && res.LatenessDays == 0) continue;
            if (!needsLine && !waiting && res.LatenessDays == 0) continue;
            list.Add(new EndangeredOrderDto(o.Code, o.ProductCode, o.Priority, requiredOn, shortage?.Quantity ?? 0, shortage?.AvailableOn, res.LatenessDays));
        }
        return list.OrderByDescending(e => e.Priority).ThenBy(e => e.RequiredOn).ToList();
    }

    /// <summary>Re-scores a line, persists history, updates denormalised score, raises DeliveryRiskChanged when material.</summary>
    public async Task<RiskSummaryDto> AssessAndPersistAsync(PurchaseOrderLine line, string trigger, PlanImpact? planImpact, CancellationToken ct, bool raiseEvent = true)
    {
        var (result, endangered) = await ComputeAsync(line, planImpact, ct);
        var old = line.RiskScore;
        var oldCat = line.RiskCategory;
        line.RiskScore = result.Score;
        line.RiskCategory = result.Category;
        var assessment = new RiskAssessment
        {
            Id = Guid.NewGuid(), PurchaseOrderLineId = line.Id, Score = result.Score, Category = result.Category, PreviousScore = old,
            FactorsJson = Json.Serialize(result.Factors), EndangeredOrdersJson = Json.Serialize(endangered), Trigger = trigger,
            AssessedAt = clock.UtcNow, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.RiskAssessments.Add(assessment);

        if (raiseEvent && (oldCat != result.Category || Math.Abs(old - result.Score) >= 5))
        {
            var po = line.PurchaseOrder!;
            var supplierCode = po.Supplier?.Code ?? (await db.Suppliers.AsNoTracking().Where(s => s.Id == po.SupplierId).Select(s => s.Code).FirstAsync(ct));
            var partCode = line.Part?.Code ?? (await db.Parts.AsNoTracking().Where(p => p.Id == line.PartId).Select(p => p.Code).FirstAsync(ct));
            events.Publish(new DeliveryRiskChanged(clock.UtcNow, user.CorrelationId, po.Code, line.Id, line.LineNo, partCode, supplierCode, old, result.Score, oldCat.ToString(), result.Category.ToString(), endangered.Select(e => e.OrderCode).ToArray()));

            if (result.Score >= options.Value.NotifyThreshold && result.Score > old)
            {
                var n = new Notification
                {
                    Id = Guid.NewGuid(), TargetRole = Role.ProductionPlanner, Severity = result.Category == RiskCategory.Critical ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                    TitleKey = "notifications.deliveryRisk.title", MessageKey = "notifications.deliveryRisk.message",
                    ParamsJson = Json.Serialize(new { poCode = po.Code, lineNo = line.LineNo, partCode, score = result.Score, category = result.Category.ToString(), endangered = string.Join(", ", endangered.Select(e => e.OrderCode)), eventName = EventNames.DeliveryRiskChanged }),
                    Route = $"/supply/orders/{po.Code}", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
                };
                db.Notifications.Add(n);
                events.Publish(new NotificationCreated(clock.UtcNow, user.CorrelationId, n.Id, n.TargetRole?.ToString(), n.Severity.ToString(), n.TitleKey));
            }
        }
        return ToDto(result, endangered, assessment.AssessedAt);
    }

    /// <summary>Re-scores every open line of the given part (or all open lines when null). Call after stock/lot/plan changes.</summary>
    public async Task<int> RecalculateAffectedAsync(string? partCode, string trigger, CancellationToken ct, bool raiseEvents = true)
    {
        var q = db.PurchaseOrderLines.Include(l => l.Part).Include(l => l.PurchaseOrder).ThenInclude(p => p!.Supplier)
            .Where(l => l.Status != PurchaseOrderLineStatus.Delivered);
        if (partCode is not null) q = q.Where(l => l.Part!.Code == partCode);
        var lines = await q.OrderBy(l => l.PurchaseOrder!.Code).ThenBy(l => l.LineNo).ToListAsync(ct);
        // one plan evaluation per plant, reused across that plant's lines
        var byPlant = new Dictionary<Guid, PlanImpact>();
        foreach (var line in lines)
        {
            var siteId = line.PurchaseOrder!.SiteId;
            if (!byPlant.TryGetValue(siteId, out var planImpact))
                byPlant[siteId] = planImpact = await impact.EvaluateAsync(siteId, null, ct);
            await AssessAndPersistAsync(line, trigger, planImpact, ct, raiseEvents);
        }
        return lines.Count;
    }

    public static RiskSummaryDto ToDto(RiskResult r, IReadOnlyList<EndangeredOrderDto> endangered, DateTime assessedAt) => new(
        r.Score, r.Category.ToString(),
        r.Factors.Select(f => new RiskFactorDto(f.Code, f.Raw, f.Weight, f.Contribution)).ToList(),
        r.TopFactors.Select(f => new RiskFactorDto(f.Code, f.Raw, f.Weight, f.Contribution)).ToList(),
        endangered, assessedAt);

    public static RiskSummaryDto FromAssessment(RiskAssessment a)
    {
        var factors = Json.Deserialize<List<RiskFactorDto>>(a.FactorsJson) ?? new();
        var endangered = Json.Deserialize<List<EndangeredOrderDto>>(a.EndangeredOrdersJson) ?? new();
        return new RiskSummaryDto(a.Score, a.Category.ToString(), factors, factors.OrderByDescending(f => f.Contribution).ThenBy(f => f.Code).Take(3).ToList(), endangered, a.AssessedAt);
    }
}
