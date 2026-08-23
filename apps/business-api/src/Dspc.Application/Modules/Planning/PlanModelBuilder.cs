using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dspc.Application.Modules.Planning;

public sealed class PlanningOptions
{
    public const string Section = "Planning";
    public ObjectiveWeights Weights { get; set; } = new();
    public int TimeLimitMs { get; set; } = 2500;
    public int HorizonWeeks { get; set; } = 12;
}

/// <summary>Overrides applied to the assembled model before evaluation / solving (scenario changes, previews).</summary>
public sealed class PlanOverrides
{
    public Dictionary<Guid, DateOnly> EtaByLineId { get; } = new();
    public HashSet<string> BlockedLots { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> PriorityByOrder { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> CapacityFactorByWorkCenter { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> DelayDaysByOrder { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsEmpty => EtaByLineId.Count == 0 && BlockedLots.Count == 0 && PriorityByOrder.Count == 0 && CapacityFactorByWorkCenter.Count == 0 && DelayDaysByOrder.Count == 0;
}

public sealed record PlanOrderMeta(string Code, Guid Id, string ProductCode, string ProductNamePl, string ProductNameEn, int Priority, DateOnly DueDate, string Status, string? LineCode);
public sealed record PlanWorkCenterMeta(string Code, string NamePl, string NameEn, string LineCode);

public sealed class PlanModel
{
    public required PlanningBaseline Baseline { get; init; }
    public required PlanningRequest Request { get; init; }
    public required IReadOnlyList<PlanOrderMeta> Orders { get; init; }
    public required IReadOnlyList<PlanWorkCenterMeta> WorkCenters { get; init; }
    public required IReadOnlyDictionary<string, Guid> OperationIdsByCode { get; init; }
}

/// <summary>Assembles the planning problem (engine contract shape) from the database for the active baseline.</summary>
public sealed class PlanModelBuilder(IAppDbContext db, IDemoClock clock, IOptions<PlanningOptions> options)
{
    /// <summary>Assembles the problem for one plant. Every plant has its own baseline, work centres, orders and stock.</summary>
    public async Task<PlanModel> BuildAsync(Guid siteId, PlanOverrides? overrides, CancellationToken ct, string scenarioId = "baseline")
    {
        overrides ??= new PlanOverrides();
        var baseline = await db.PlanningBaselines.AsNoTracking()
            .Where(b => b.SiteId == siteId && b.Status == PlanningBaselineStatus.Active)
            .OrderByDescending(b => b.Version).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("PlanningBaseline", "active");

        var lines = await db.AssemblyLines.AsNoTracking().Where(l => l.SiteId == siteId).ToDictionaryAsync(l => l.Id, ct);
        var workCenters = await db.WorkCenters.AsNoTracking().Include(w => w.Calendar).Where(w => w.SiteId == siteId).OrderBy(w => w.Sequence).ToListAsync(ct);
        var wcById = workCenters.ToDictionary(w => w.Id);

        var orders = await db.ProductionOrders.AsNoTracking()
            .Include(o => o.Product).Include(o => o.Operations)
            .Where(o => o.SiteId == siteId && o.Status != ProductionOrderStatus.Completed && o.Status != ProductionOrderStatus.Cancelled)
            .OrderBy(o => o.Code).ToListAsync(ct);
        var orderIds = orders.Select(o => o.Id).ToList();

        var scheduled = await db.ScheduledOperations.AsNoTracking()
            .Where(s => s.PlanningBaselineId == baseline.Id).ToListAsync(ct);
        var schedByOp = scheduled.ToDictionary(s => s.OperationDefinitionId);

        var reservations = await db.Reservations.AsNoTracking().Include(r => r.Part).Include(r => r.MaterialLot)
            .Where(r => orderIds.Contains(r.ProductionOrderId)).ToListAsync(ct);

        var parts = await db.Parts.AsNoTracking().ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, ct);
        var lots = await db.MaterialLots.AsNoTracking().Include(l => l.Part).Where(l => l.SiteId == siteId).ToListAsync(ct);
        var inboundLines = await db.PurchaseOrderLines.AsNoTracking().Include(l => l.Part).Include(l => l.PurchaseOrder)
            .Where(l => l.PurchaseOrder!.SiteId == siteId && l.Status != PurchaseOrderLineStatus.Delivered && l.Quantity > l.DeliveredQuantity)
            .ToListAsync(ct);

        var planWcs = workCenters.Select(w => new PlanWorkCenter
        {
            Code = w.Code,
            LineCode = lines[w.AssemblyLineId].Code,
            HoursPerDay = w.HoursPerDay,
            CapacityFactor = overrides.CapacityFactorByWorkCenter.GetValueOrDefault(w.Code, 1.0),
            Calendar = w.Calendar.OrderBy(c => c.Date).Select(c => new CalendarOverride { Date = c.Date, AvailableHours = c.AvailableHours }).ToList()
        }).ToList();

        var planOrders = new List<PlanOrder>();
        var meta = new List<PlanOrderMeta>();
        var opIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var usedParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var o in orders)
        {
            var resByPart = reservations.Where(r => r.ProductionOrderId == o.Id && !r.IsBlocked && !(r.MaterialLot is not null && overrides.BlockedLots.Contains(r.MaterialLot.LotNumber)))
                .GroupBy(r => r.Part!.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity), StringComparer.OrdinalIgnoreCase);
            var delay = overrides.DelayDaysByOrder.GetValueOrDefault(o.Code, 0);
            var po = new PlanOrder
            {
                Code = o.Code,
                ProductCode = o.Product!.Code,
                Priority = overrides.PriorityByOrder.GetValueOrDefault(o.Code, o.Priority),
                Quantity = o.Quantity,
                DueDate = o.DueDate,
                ReleaseDate = o.ReleaseDate.AddDays(delay),
                Frozen = o.Frozen,
                LineCode = o.AssemblyLineId is { } lid ? lines[lid].Code : null
            };
            foreach (var op in o.Operations.OrderBy(x => x.Sequence))
            {
                opIds[op.Code] = op.Id;
                var reqs = (Json.Deserialize<List<MaterialRequirement>>(op.MaterialRequirementsJson) ?? new())
                    .Select(r =>
                    {
                        var reserved = resByPart.GetValueOrDefault(r.PartCode, 0m);
                        var net = Math.Max(0, r.Quantity - reserved);
                        resByPart[r.PartCode] = Math.Max(0, reserved - r.Quantity);
                        usedParts.Add(r.PartCode);
                        return new MaterialRequirement { PartCode = r.PartCode, Quantity = net };
                    })
                    .Where(r => r.Quantity > 0).ToList();
                schedByOp.TryGetValue(op.Id, out var s);
                po.Operations.Add(new PlanOperation
                {
                    Code = op.Code,
                    Sequence = op.Sequence,
                    WorkCenterCode = wcById[op.WorkCenterId].Code,
                    DurationHours = op.DurationHours,
                    Frozen = op.Frozen || op.Status == OperationStatus.Completed || op.Status == OperationStatus.InProgress,
                    BaselineStart = s is null ? null : clock.ToSiteLocal(s.Start),
                    BaselineEnd = s is null ? null : clock.ToSiteLocal(s.End),
                    MaterialRequirements = reqs
                });
            }
            planOrders.Add(po);
            meta.Add(new PlanOrderMeta(o.Code, o.Id, o.Product.Code, o.Product.NamePl, o.Product.NameEn, po.Priority, o.DueDate, o.Status.ToString(), po.LineCode));
        }

        var materials = new List<MaterialAvailability>();
        foreach (var partCode in usedParts.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!parts.TryGetValue(partCode, out var part)) continue;
            var partLots = lots.Where(l => l.PartId == part.Id).ToList();
            var onHand = partLots.Where(l => (l.Status is MaterialLotStatus.Accepted or MaterialLotStatus.ConditionallyReleased) && !overrides.BlockedLots.Contains(l.LotNumber))
                .Sum(l => l.RemainingQuantity);
            var reserved = reservations.Where(r => r.PartId == part.Id && !r.IsBlocked && !(r.MaterialLot is not null && overrides.BlockedLots.Contains(r.MaterialLot.LotNumber))).Sum(r => r.Quantity);
            var inbound = inboundLines.Where(l => l.PartId == part.Id)
                .OrderBy(l => overrides.EtaByLineId.GetValueOrDefault(l.Id, l.Eta)).ThenBy(l => l.PurchaseOrder!.Code).ThenBy(l => l.LineNo)
                .Select(l => new InboundSupply
                {
                    Quantity = l.Quantity - l.DeliveredQuantity,
                    Eta = overrides.EtaByLineId.GetValueOrDefault(l.Id, l.Eta),
                    Reference = $"{l.PurchaseOrder!.Code}/{l.LineNo}",
                    RiskScore = l.RiskScore
                }).ToList();
            materials.Add(new MaterialAvailability { PartCode = partCode, OnHand = onHand, Reserved = Math.Min(reserved, onHand), Inbound = inbound });
        }

        var request = new PlanningRequest
        {
            ScenarioId = scenarioId,
            BaselineId = baseline.Id.ToString(),
            HorizonStart = baseline.HorizonStart,
            HorizonEnd = baseline.HorizonEnd,
            TimeLimitMs = options.Value.TimeLimitMs,
            WorkCenters = planWcs,
            Orders = planOrders,
            Materials = materials,
            Weights = options.Value.Weights
        };
        return new PlanModel
        {
            Baseline = baseline, Request = request, Orders = meta,
            WorkCenters = workCenters.Select(w => new PlanWorkCenterMeta(w.Code, w.NamePl, w.NameEn, lines[w.AssemblyLineId].Code)).ToList(),
            OperationIdsByCode = opIds
        };
    }
}
