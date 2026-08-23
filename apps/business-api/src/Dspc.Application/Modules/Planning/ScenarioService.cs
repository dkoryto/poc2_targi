using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Domain.Events;
using Dspc.Application.Modules.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dspc.Application.Modules.Planning;

/// <summary>
/// What-If scenarios: create → run (background) → compare → approve/reject/save.
/// A scenario never mutates the active baseline; approval creates the next baseline version instead.
/// </summary>
public sealed class ScenarioService(
    IAppDbContext db,
    PlanModelBuilder builder,
    IPlanImpactEvaluator impact,
    IPlanningEngine engine,
    IDemoClock clock,
    ICurrentUser user,
    IAuditWriter audit,
    IEventPublisher events,
    Sites.ISiteContext siteContext,
    ILogger<ScenarioService> log)
{
    // ---------------------------------------------------------------- queries

    public async Task<ListResult<ScenarioSummaryDto>> ListAsync(Guid siteId, CancellationToken ct)
    {
        var rows = await db.PlanningScenarios.AsNoTracking().Include(s => s.Changes)
            .Where(s => s.SiteId == siteId)
            .OrderByDescending(s => s.CreatedAt).Take(50).ToListAsync(ct);
        return ListResult.Of(rows.Select(s => new ScenarioSummaryDto(
            s.Id, s.Name, s.Status.ToString(), s.CreatedAt, s.CreatedBy, s.Solver, s.Changes.Count,
            s.KpiAfterJson is null ? null : Json.Deserialize<PlanKpi>(s.KpiAfterJson), s.PresetKey)).ToList());
    }

    public async Task<ScenarioDto> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        int? version = s.Status == PlanningScenarioStatus.Approved
            ? await db.PlanningBaselines.AsNoTracking().Where(b => b.SourceScenarioId == s.Id)
                .Select(b => (int?)b.Version).FirstOrDefaultAsync(ct)
            : null;
        return await ToDtoAsync(s, version, ct);
    }

    public async Task<ScenarioCompareDto> CompareAsync(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        var before = s.BeforeJson is null ? null : Json.Deserialize<GanttData>(s.BeforeJson);
        var after = s.AfterJson is null ? null : Json.Deserialize<GanttData>(s.AfterJson);
        if (before is null || after is null) return new ScenarioCompareDto([], new PlanKpi());

        var kb = s.KpiBeforeJson is null ? new PlanKpi() : Json.Deserialize<PlanKpi>(s.KpiBeforeJson)!;
        var ka = s.KpiAfterJson is null ? new PlanKpi() : Json.Deserialize<PlanKpi>(s.KpiAfterJson)!;
        return new ScenarioCompareDto(ScenarioCalculations.MovedOperations(before, after), ScenarioCalculations.KpiDelta(kb, ka));
    }

    // ---------------------------------------------------------------- create

    public async Task<ScenarioDto> CreateAsync(CreateScenarioRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
        if (request.Changes is null || request.Changes.Count == 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["changes"] = ["At least one change is required."] });
        if (request.Changes.Count > 20)
            throw new ValidationException(new Dictionary<string, string[]> { ["changes"] = ["At most 20 changes per scenario."] });

        await ValidateChangesAsync(request.Changes, ct);

        // The plant is implied by what the changes point at; mixing plants in one scenario is rejected.
        var siteId = await ResolveScenarioSiteAsync(request.Changes, request.SiteCode, ct);
        var baseline = await ActiveBaselineAsync(siteId, ct);
        var scenario = new PlanningScenario
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            SiteId = siteId,
            PresetKey = request.PresetKey,
            Status = PlanningScenarioStatus.Draft,
            BaselineId = baseline.Id,
            CreatedBy = user.Username,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
        var seq = 0;
        foreach (var c in request.Changes)
            scenario.Changes.Add(new ScenarioChange
            {
                Id = Guid.NewGuid(),
                PlanningScenarioId = scenario.Id,
                Type = c.Type,
                TargetCode = TargetCodeOf(c),
                TargetId = c.PoLineId,
                ParametersJson = Json.Serialize(c),
                Sequence = seq++,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow
            });

        db.PlanningScenarios.Add(scenario);
        audit.Write("Planning.ScenarioCreated", "PlanningScenario", scenario.Name, scenario.Id, null,
            new { scenario.Name, scenario.PresetKey, Changes = request.Changes });
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(scenario, null, ct);
    }

    private static string? TargetCodeOf(ScenarioChangeDto c) => c.Type switch
    {
        ScenarioChangeType.DELAY_INBOUND => c.PoCode ?? c.PartCode,
        ScenarioChangeType.BLOCK_LOT => c.LotNumber,
        ScenarioChangeType.PRIORITY or ScenarioChangeType.DELAY_ORDER => c.OrderCode,
        ScenarioChangeType.CAPACITY => c.WorkCenterCode,
        _ => null
    };

    private async Task ValidateChangesAsync(IReadOnlyList<ScenarioChangeDto> changes, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        void Err(int i, string field, string msg) => errors[$"changes[{i}].{field}"] = [msg];

        for (var i = 0; i < changes.Count; i++)
        {
            var c = changes[i];
            switch (c.Type)
            {
                case ScenarioChangeType.DELAY_INBOUND:
                    if (c.PoLineId is not { } lineId || lineId == Guid.Empty) { Err(i, "poLineId", "Purchase-order line is required."); break; }
                    if (c.Days is not { } d || d is < -60 or > 180) { Err(i, "days", "Days must be between -60 and 180."); break; }
                    if (!await db.PurchaseOrderLines.AsNoTracking().AnyAsync(l => l.Id == lineId, ct)) Err(i, "poLineId", "Purchase-order line was not found.");
                    break;
                case ScenarioChangeType.BLOCK_LOT:
                    if (string.IsNullOrWhiteSpace(c.LotNumber)) { Err(i, "lotNumber", "Lot number is required."); break; }
                    if (!await db.MaterialLots.AsNoTracking().AnyAsync(l => l.LotNumber == c.LotNumber, ct)) Err(i, "lotNumber", $"Lot '{c.LotNumber}' was not found.");
                    break;
                case ScenarioChangeType.PRIORITY:
                    if (string.IsNullOrWhiteSpace(c.OrderCode)) { Err(i, "orderCode", "Order code is required."); break; }
                    if (c.Priority is not { } p || p is < 1 or > 5) { Err(i, "priority", "Priority must be 1..5."); break; }
                    if (!await db.ProductionOrders.AsNoTracking().AnyAsync(o => o.Code == c.OrderCode, ct)) Err(i, "orderCode", $"Order '{c.OrderCode}' was not found.");
                    break;
                case ScenarioChangeType.CAPACITY:
                    if (string.IsNullOrWhiteSpace(c.WorkCenterCode)) { Err(i, "workCenterCode", "Work centre is required."); break; }
                    if (c.Factor is not { } f || f is <= 0 or > 2) { Err(i, "factor", "Factor must be in (0, 2]."); break; }
                    if (!await db.WorkCenters.AsNoTracking().AnyAsync(w => w.Code == c.WorkCenterCode, ct)) Err(i, "workCenterCode", $"Work centre '{c.WorkCenterCode}' was not found.");
                    break;
                case ScenarioChangeType.DELAY_ORDER:
                    if (string.IsNullOrWhiteSpace(c.OrderCode)) { Err(i, "orderCode", "Order code is required."); break; }
                    if (c.Days is not { } od || od is < -60 or > 180) { Err(i, "days", "Days must be between -60 and 180."); break; }
                    if (!await db.ProductionOrders.AsNoTracking().AnyAsync(o => o.Code == c.OrderCode, ct)) Err(i, "orderCode", $"Order '{c.OrderCode}' was not found.");
                    break;
                default:
                    Err(i, "type", "Unsupported change type.");
                    break;
            }
        }
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    // ---------------------------------------------------------------- run

    /// <summary>Marks the scenario Running and hands it to the background runner. Idempotent while already running.</summary>
    public async Task<ScenarioRunAcceptedDto> RequestRunAsync(Guid id, ScenarioRunQueue queue, CancellationToken ct)
    {
        var s = await Load(id, ct);
        if (s.Status == PlanningScenarioStatus.Running) return new ScenarioRunAcceptedDto(s.Id, "Running");
        if (s.Status is PlanningScenarioStatus.Approved)
            throw new ConflictException("An approved scenario cannot be recalculated.");

        s.Status = PlanningScenarioStatus.Running;
        s.StartedAt = clock.UtcNow;
        s.UpdatedAt = clock.UtcNow;
        s.FailureReason = null;
        await db.SaveChangesAsync(ct);
        queue.Enqueue(s.Id, user.CorrelationId);
        return new ScenarioRunAcceptedDto(s.Id, "Running");
    }

    /// <summary>Executes the scenario (called on the background runner's scope).</summary>
    public async Task ExecuteAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var s = await db.PlanningScenarios.Include(x => x.Changes).Include(x => x.Recommendations)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("PlanningScenario", id.ToString());
        try
        {
            var changes = s.Changes.OrderBy(c => c.Sequence).Select(c => Json.Deserialize<ScenarioChangeDto>(c.ParametersJson)!).ToList();
            var overrides = await ToOverridesAsync(changes, ct);

            // "Before" = baseline + the scenario change, no re-sequencing.
            var before = await impact.EvaluateAsync(s.SiteId, overrides, ct);

            // "After" = the engine proposal for the same problem.
            var model = await builder.BuildAsync(s.SiteId, overrides, ct, s.Id.ToString());
            var outcome = await engine.SolveAsync(model.Request, ct);
            var after = GanttBuilder.Build(model, outcome.Response, clock);

            // The engine measures "changed" against the approved baseline it was handed. The result screen asks a
            // different question — what did re-planning move relative to what would otherwise happen — so the
            // Gantt markers and the headline KPI are re-anchored on "before". The engine's own count is kept in
            // ResponseJson and surfaced separately as "changes vs the approved baseline".
            after = ScenarioCalculations.ReanchorChanges(before.Gantt, after);
            var movedVsBefore = ScenarioCalculations.MovedOperations(before.Gantt, after);

            var explanations = BuildExplanations(outcome, before.Evaluation, changes);

            // Copies, not the engine's own objects: ResponseJson must keep the engine's unmodified numbers.
            // "Before" is the reference plan, so by definition nothing has moved relative to it yet.
            var kpiBefore = Clone(before.Evaluation.Kpi, 0);
            var kpiAfter = Clone(outcome.Response.Kpi, movedVsBefore.Count);

            s.RequestJson = Json.Serialize(model.Request);
            s.ResponseJson = Json.Serialize(outcome.Response);
            s.BeforeJson = Json.Serialize(before.Gantt);
            s.AfterJson = Json.Serialize(after);
            s.KpiBeforeJson = Json.Serialize(kpiBefore);
            s.KpiAfterJson = Json.Serialize(kpiAfter);
            s.ExplanationsJson = Json.Serialize(explanations);
            s.Solver = outcome.Response.Solver;
            s.ElapsedMs = outcome.Response.ElapsedMs;
            s.Status = PlanningScenarioStatus.Completed;
            s.CompletedAt = clock.UtcNow;
            s.UpdatedAt = clock.UtcNow;

            db.PlanningRecommendations.RemoveRange(s.Recommendations);
            var seq = 0;
            foreach (var e in explanations)
                db.PlanningRecommendations.Add(new PlanningRecommendation
                {
                    Id = Guid.NewGuid(), PlanningScenarioId = s.Id, ReasonCode = e.ReasonCode,
                    OrderCode = string.IsNullOrEmpty(e.OrderCode) ? null : e.OrderCode,
                    ParamsJson = Json.Serialize(e.Params), Sequence = seq++,
                    CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
                });

            events.Publish(new PlanningScenarioCompleted(clock.UtcNow, correlationId, s.Id, s.Status.ToString(), s.Solver ?? "", s.ElapsedMs ?? 0));
            await db.SaveChangesAsync(ct);
            log.LogInformation("Scenario {ScenarioId} completed via {Solver} in {ElapsedMs} ms (downtime {Before} h → {After} h)",
                s.Id, s.Solver, s.ElapsedMs, before.Evaluation.Kpi.DowntimeHours, outcome.Response.Kpi.DowntimeHours);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Scenario {ScenarioId} failed", id);
            s.Status = PlanningScenarioStatus.Failed;
            s.FailureReason = ex is AppException ae ? ae.Message : ex.GetType().Name;
            s.CompletedAt = clock.UtcNow;
            s.UpdatedAt = clock.UtcNow;
            events.Publish(new PlanningScenarioCompleted(clock.UtcNow, correlationId, s.Id, s.Status.ToString(), s.Solver ?? "", 0));
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>Copy of a KPI set with the presentation-level moved-operations count substituted.</summary>
    private static PlanKpi Clone(PlanKpi k, int movedOperations) => new()
    {
        DowntimeHours = k.DowntimeHours,
        LateOrders = k.LateOrders,
        TotalLatenessDays = k.TotalLatenessDays,
        MovedOperations = movedOperations,
        OrdersWithShortage = k.OrdersWithShortage,
        OnTimeRate = k.OnTimeRate
    };

    private async Task<PlanOverrides> ToOverridesAsync(IReadOnlyList<ScenarioChangeDto> changes, CancellationToken ct)
    {
        var lineIds = changes.Where(c => c.Type == ScenarioChangeType.DELAY_INBOUND && c.PoLineId is not null)
            .Select(c => c.PoLineId!.Value).Distinct().ToList();
        var etaByLine = lineIds.Count == 0
            ? new Dictionary<Guid, DateOnly>()
            : await db.PurchaseOrderLines.AsNoTracking().Where(l => lineIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Eta, ct);
        foreach (var id in lineIds)
            if (!etaByLine.ContainsKey(id)) throw new NotFoundException("PurchaseOrderLine", id.ToString());
        return ScenarioCalculations.BuildOverrides(changes, etaByLine);
    }

    /// <summary>Engine explanations, plus the ones only the caller can know (fallback, downtime delta, no-op scenario).</summary>
    private static List<Explanation> BuildExplanations(EngineOutcome outcome, PlanningResponse before, IReadOnlyList<ScenarioChangeDto> changes)
    {
        var list = new List<Explanation>(outcome.Response.Explanations);

        if (outcome.UsedFallback)
            list.Insert(0, new Explanation
            {
                ReasonCode = ReasonCodes.FallbackUsed, OrderCode = "",
                Params = new Dictionary<string, object?> { ["reason"] = outcome.FallbackReason ?? "engine unavailable" }
            });

        // The engine reports DOWNTIME_REDUCED for its own improvement pass; add the caller-visible before → after delta
        // when the engine did not (e.g. fallback) and it actually changed.
        var beforeH = before.Kpi.DowntimeHours;
        var afterH = outcome.Response.Kpi.DowntimeHours;
        if (afterH < beforeH && !list.Any(e => e.ReasonCode == ReasonCodes.DowntimeReduced))
            list.Add(new Explanation
            {
                ReasonCode = ReasonCodes.DowntimeReduced, OrderCode = "",
                Params = new Dictionary<string, object?> { ["fromHours"] = beforeH, ["toHours"] = afterH }
            });

        // The engine only explains what its own improvement pass did. A re-sequencing that falls out of
        // the initial placement (expediting a promoted order, for instance) left the list empty, so a
        // scenario that visibly moved operations still reported "no change" — the screen contradicted
        // its own Gantt. Say what actually happened before falling back to the no-op message.
        if (list.Count == 0)
        {
            var beforeStart = before.Operations
                .GroupBy(o => o.OrderCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Min(o => o.Start), StringComparer.OrdinalIgnoreCase);
            var pulled = outcome.Response.Operations
                .GroupBy(o => o.OrderCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Order = g.Key, Start = g.Min(o => o.Start), WorkCenters = g.Select(o => o.WorkCenterCode).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList() })
                .Where(x => beforeStart.TryGetValue(x.Order, out var b) && x.Start < b)
                .OrderBy(x => x.Start).ThenBy(x => x.Order, StringComparer.Ordinal)
                .FirstOrDefault();
            if (pulled is not null)
                list.Add(new Explanation
                {
                    ReasonCode = ReasonCodes.OrderPulledForward, OrderCode = pulled.Order,
                    Params = new Dictionary<string, object?>
                    {
                        ["orderCode"] = pulled.Order,
                        ["lineCode"] = outcome.Response.Orders.FirstOrDefault(o => string.Equals(o.OrderCode, pulled.Order, StringComparison.OrdinalIgnoreCase))?.LineCode ?? "",
                        ["days"] = (int)Math.Round((beforeStart[pulled.Order] - pulled.Start).TotalDays),
                        ["materialCompleteness"] = 1.0,
                        ["workCenters"] = pulled.WorkCenters
                    }
                });
        }

        if (list.Count == 0)
            list.Add(new Explanation
            {
                ReasonCode = "NO_CHANGE",
                OrderCode = changes.FirstOrDefault(c => c.OrderCode is not null)?.OrderCode ?? "",
                Params = new Dictionary<string, object?>()
            });
        return list;
    }

    private static List<ConsequenceDto> BuildConsequences(PlanningResponse after, PlanKpi before)
    {
        var list = new List<ConsequenceDto>();
        foreach (var o in after.Orders.Where(o => o.LatenessDays > 0).OrderByDescending(o => o.LatenessDays).ThenBy(o => o.OrderCode, StringComparer.Ordinal))
            list.Add(new ConsequenceDto("critical", "explain.ORDER_LATE_DUE",
                new Dictionary<string, object?> { ["orderCode"] = o.OrderCode, ["days"] = o.LatenessDays }));

        foreach (var o in after.Orders.Where(o => o.Shortages.Count > 0).OrderBy(o => o.OrderCode, StringComparer.Ordinal))
        {
            var s = o.Shortages[0];
            // An order can be short of material without ending up late (slack absorbed the wait).
            // Saying "moved by 0 days" there reads as noise, so use a shortage-only wording.
            var delayed = o.LatenessDays > 0;
            list.Add(new ConsequenceDto("warn", delayed ? "explain.ORDER_DELAYED_MATERIAL_SHORTAGE" : "explain.ORDER_MATERIAL_SHORTAGE",
                new Dictionary<string, object?>
                {
                    ["orderCode"] = o.OrderCode, ["partCode"] = s.PartCode, ["missingQty"] = s.Quantity,
                    ["days"] = o.LatenessDays, ["availableOn"] = s.AvailableOn?.ToString("yyyy-MM-dd")
                }));
        }

        if (after.Kpi.DowntimeHours < before.DowntimeHours)
            list.Add(new ConsequenceDto("info", "explain.DOWNTIME_REDUCED",
                new Dictionary<string, object?> { ["fromHours"] = before.DowntimeHours, ["toHours"] = after.Kpi.DowntimeHours }));
        return list;
    }

    // ---------------------------------------------------------------- decisions

    public async Task<ScenarioDto> ApproveAsync(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        if (s.Status != PlanningScenarioStatus.Completed)
            throw new ConflictException($"Only a completed scenario can be approved (current status: {s.Status}).");
        if (s.ResponseJson is null) throw new ConflictException("Scenario has no plan to approve.");

        var response = Json.Deserialize<PlanningResponse>(s.ResponseJson)!;
        var current = await ActiveBaselineAsync(s.SiteId, ct);
        var currentOps = await db.ScheduledOperations.Where(o => o.PlanningBaselineId == current.Id).AsNoTracking().ToListAsync(ct);
        var codeByOperationId = await db.OperationDefinitions.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Code, ct);

        var next = new PlanningBaseline
        {
            Id = Guid.NewGuid(),
            SiteId = current.SiteId,
            Version = current.Version + 1,
            Status = PlanningBaselineStatus.Active,
            HorizonStart = current.HorizonStart,
            HorizonEnd = current.HorizonEnd,
            ApprovedBy = user.Username,
            ApprovedAt = clock.UtcNow,
            SourceScenarioId = s.Id,
            KpiJson = s.KpiAfterJson,
            Notes = s.Name,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
        db.PlanningBaselines.Add(next);

        var resultByOp = response.Operations.ToDictionary(o => o.OperationCode, StringComparer.OrdinalIgnoreCase);
        foreach (var old in currentOps)
        {
            var r = codeByOperationId.TryGetValue(old.OperationDefinitionId, out var code) && resultByOp.TryGetValue(code, out var rr) ? rr : null;
            db.ScheduledOperations.Add(new ScheduledOperation
            {
                Id = Guid.NewGuid(),
                PlanningBaselineId = next.Id,
                OperationDefinitionId = old.OperationDefinitionId,
                WorkCenterId = old.WorkCenterId,
                AssemblyLineId = old.AssemblyLineId,
                Start = r is null ? old.Start : clock.FromSiteLocal(r.Start),
                End = r is null ? old.End : clock.FromSiteLocal(r.End),
                Frozen = old.Frozen,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow
            });
        }

        var tracked = await db.PlanningBaselines.FirstAsync(b => b.Id == current.Id, ct);
        tracked.Status = PlanningBaselineStatus.Superseded;
        tracked.UpdatedAt = clock.UtcNow;

        s.Status = PlanningScenarioStatus.Approved;
        s.DecidedBy = user.Username;
        s.DecidedAt = clock.UtcNow;
        s.UpdatedAt = clock.UtcNow;

        audit.Write("Planning.PlanApproved", "PlanningBaseline", $"v{next.Version}", next.Id,
            new { Version = current.Version, current.ApprovedBy, current.ApprovedAt },
            new { next.Version, next.ApprovedBy, next.ApprovedAt, ScenarioId = s.Id, s.Name });
        events.Publish(new ProductionPlanApproved(clock.UtcNow, user.CorrelationId, s.Id, next.Version, user.Username));
        await db.SaveChangesAsync(ct);

        return await ToDtoAsync(s, next.Version, ct);
    }

    public Task<ScenarioDto> RejectAsync(Guid id, CancellationToken ct) => DecideAsync(id, PlanningScenarioStatus.Rejected, "Planning.ScenarioRejected", ct);
    public Task<ScenarioDto> SaveAsync(Guid id, CancellationToken ct) => DecideAsync(id, PlanningScenarioStatus.Saved, "Planning.ScenarioSaved", ct);

    private async Task<ScenarioDto> DecideAsync(Guid id, PlanningScenarioStatus status, string action, CancellationToken ct)
    {
        var s = await Load(id, ct);
        if (s.Status == PlanningScenarioStatus.Approved) throw new ConflictException("An approved scenario cannot be changed.");
        var before = s.Status;
        s.Status = status;
        s.DecidedBy = user.Username;
        s.DecidedAt = clock.UtcNow;
        s.UpdatedAt = clock.UtcNow;
        audit.Write(action, "PlanningScenario", s.Name, s.Id, new { Status = before.ToString() }, new { Status = status.ToString() });
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(s, null, ct);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<PlanningScenario> Load(Guid id, CancellationToken ct) =>
        await db.PlanningScenarios.Include(s => s.Changes).FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException("PlanningScenario", id.ToString());

    private async Task<PlanningBaseline> ActiveBaselineAsync(Guid siteId, CancellationToken ct) =>
        await db.PlanningBaselines.AsNoTracking().Where(b => b.SiteId == siteId && b.Status == PlanningBaselineStatus.Active)
            .OrderByDescending(b => b.Version).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("PlanningBaseline", "active");

    /// <summary>
    /// Derives the plant from the scenario's targets. Every target must belong to the same plant — a scenario that
    /// mixes plants is a modelling error, not something to silently pick a winner for.
    /// </summary>
    private async Task<Guid> ResolveScenarioSiteAsync(IReadOnlyList<ScenarioChangeDto> changes, string? siteCode, CancellationToken ct)
    {
        var found = new Dictionary<Guid, string>();
        foreach (var c in changes)
        {
            Guid? id = c.Type switch
            {
                ScenarioChangeType.DELAY_INBOUND when c.PoLineId is { } lineId =>
                    await db.PurchaseOrderLines.AsNoTracking().Where(l => l.Id == lineId).Select(l => (Guid?)l.PurchaseOrder!.SiteId).FirstOrDefaultAsync(ct),
                ScenarioChangeType.BLOCK_LOT when c.LotNumber is { } lot =>
                    await db.MaterialLots.AsNoTracking().Where(l => l.LotNumber == lot).Select(l => (Guid?)l.SiteId).FirstOrDefaultAsync(ct),
                ScenarioChangeType.PRIORITY or ScenarioChangeType.DELAY_ORDER when c.OrderCode is { } oc =>
                    await db.ProductionOrders.AsNoTracking().Where(o => o.Code == oc).Select(o => (Guid?)o.SiteId).FirstOrDefaultAsync(ct),
                ScenarioChangeType.CAPACITY when c.WorkCenterCode is { } wc =>
                    await db.WorkCenters.AsNoTracking().Where(w => w.Code == wc).Select(w => (Guid?)w.SiteId).FirstOrDefaultAsync(ct),
                _ => null
            };
            if (id is { } sid && sid != Guid.Empty) found[sid] = TargetCodeOf(c) ?? c.Type.ToString();
        }

        if (found.Count > 1)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["changes"] = [$"A scenario must stay within one plant, but its targets span {found.Count}: {string.Join(", ", found.Values)}."]
            });
        if (found.Count == 1)
        {
            var sid = found.Keys.First();
            // still enforce the caller's reach
            var site = await db.Sites.AsNoTracking().FirstAsync(x => x.Id == sid, ct);
            return (await siteContext.ResolveSiteAsync(site.Code, ct)).Id;
        }
        return await siteContext.ResolveAsync(siteCode, ct);
    }

    /// <summary>Resolves the scenario's plant so the response can name it, then builds the DTO.</summary>
    private async Task<ScenarioDto> ToDtoAsync(PlanningScenario s, int? approvedBaselineVersion, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().Where(x => x.Id == s.SiteId)
            .Select(x => new { x.Code, x.Name }).FirstOrDefaultAsync(ct);
        return ToDto(s, approvedBaselineVersion, site?.Code ?? "", site?.Name ?? "");
    }

    private static ScenarioDto ToDto(PlanningScenario s, int? approvedBaselineVersion = null, string siteCode = "", string siteName = "")
    {
        var kpiBefore = s.KpiBeforeJson is null ? null : Json.Deserialize<PlanKpi>(s.KpiBeforeJson);
        var response = s.ResponseJson is null ? null : Json.Deserialize<PlanningResponse>(s.ResponseJson);
        var approved = s.Status == PlanningScenarioStatus.Approved;
        return new ScenarioDto(
            s.Id, s.Name, s.Status.ToString(), s.CreatedAt, s.CreatedBy,
            s.Changes.OrderBy(c => c.Sequence).Select(c => Json.Deserialize<ScenarioChangeDto>(c.ParametersJson)!).ToList(),
            s.PresetKey, s.Solver, s.ElapsedMs,
            s.BeforeJson is null ? null : Json.Deserialize<GanttData>(s.BeforeJson),
            s.AfterJson is null ? null : Json.Deserialize<GanttData>(s.AfterJson),
            kpiBefore,
            s.KpiAfterJson is null ? null : Json.Deserialize<PlanKpi>(s.KpiAfterJson),
            s.ExplanationsJson is null ? null : Json.Deserialize<List<Explanation>>(s.ExplanationsJson),
            response is null ? null : BuildConsequences(response, kpiBefore ?? new PlanKpi()),
            approved ? s.DecidedAt : null,
            approved ? s.DecidedBy : null,
            approvedBaselineVersion,
            s.FailureReason,
            response?.Kpi.MovedOperations,
            siteCode,
            siteName);
    }
}
