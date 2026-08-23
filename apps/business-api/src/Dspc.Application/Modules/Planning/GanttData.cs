namespace Dspc.Application.Modules.Planning;

public sealed record GanttWorkCenter(string Code, string Name, string NameEn, string LineCode);
public sealed record GanttOrder(string Code, string ProductCode, string ProductName, string ProductNameEn, int Priority, DateOnly DueDate, string Status, bool MaterialComplete, string RiskFlag, int LatenessDays, DateTime PlannedStart, DateTime PlannedEnd);
public sealed record GanttOperation(string OrderCode, string Code, int Sequence, string WorkCenterCode, DateTime Start, DateTime End, bool Frozen, string Status, bool MaterialWait, bool Changed, double ShiftDays, DateTime? BaselineStart, DateTime? BaselineEnd);
public sealed record GanttDependency(string From, string To);
public sealed record GanttConflict(string OperationCode, string OrderCode, string ReasonCode, Dictionary<string, object?> Params);

public sealed record GanttData(
    DateOnly HorizonStart,
    DateOnly HorizonEnd,
    IReadOnlyList<GanttWorkCenter> WorkCenters,
    IReadOnlyList<GanttOrder> Orders,
    IReadOnlyList<GanttOperation> Operations,
    IReadOnlyList<GanttDependency> Dependencies,
    IReadOnlyList<GanttConflict> Conflicts,
    string TimeZone = "Europe/Warsaw");

public static class GanttBuilder
{
    /// <summary>Builds the Gantt view from a model + evaluation. Times are emitted as UTC instants.</summary>
    public static GanttData Build(PlanModel model, PlanningResponse response, Abstractions.IDemoClock clock)
    {
        var opResults = response.Operations.ToDictionary(o => o.OperationCode, StringComparer.OrdinalIgnoreCase);
        var orderResults = response.Orders.ToDictionary(o => o.OrderCode, StringComparer.OrdinalIgnoreCase);
        var orders = new List<GanttOrder>();
        var ops = new List<GanttOperation>();
        var deps = new List<GanttDependency>();
        var conflicts = new List<GanttConflict>();

        foreach (var o in model.Request.Orders.OrderBy(o => o.DueDate).ThenBy(o => o.Code))
        {
            var meta = model.Orders.First(m => m.Code == o.Code);
            var r = orderResults[o.Code];
            var flag = r.LatenessDays > 0 ? "critical" : (!r.MaterialComplete || o.Operations.Any(op => opResults[op.Code].WaitingForMaterial)) ? "warn" : "none";
            orders.Add(new GanttOrder(o.Code, o.ProductCode, meta.ProductNamePl, meta.ProductNameEn, o.Priority, o.DueDate, meta.Status, r.MaterialComplete, flag, r.LatenessDays,
                clock.FromSiteLocal(r.PlannedStart), clock.FromSiteLocal(r.PlannedEnd)));
            string? prev = null;
            foreach (var op in o.Operations.OrderBy(x => x.Sequence))
            {
                var res = opResults[op.Code];
                ops.Add(new GanttOperation(o.Code, op.Code, op.Sequence, op.WorkCenterCode,
                    clock.FromSiteLocal(res.Start), clock.FromSiteLocal(res.End), op.Frozen || o.Frozen,
                    op.Frozen ? "Frozen" : "Planned", res.WaitingForMaterial, res.Changed, res.ShiftDays,
                    op.BaselineStart is { } bs ? clock.FromSiteLocal(bs) : null,
                    op.BaselineEnd is { } be ? clock.FromSiteLocal(be) : null));
                if (prev is not null) deps.Add(new GanttDependency(prev, op.Code));
                prev = op.Code;
            }
        }
        foreach (var e in response.Explanations.Where(e => e.ReasonCode is ReasonCodes.OrderDelayedMaterialShortage or ReasonCodes.OrderLateDue))
        {
            var opCode = e.Params.TryGetValue("operationCode", out var oc) && oc is string s ? s : model.Request.Orders.First(o => o.Code == e.OrderCode).Operations.Last().Code;
            conflicts.Add(new GanttConflict(opCode, e.OrderCode, e.ReasonCode, e.Params));
        }
        return new GanttData(model.Request.HorizonStart, model.Request.HorizonEnd,
            model.WorkCenters.Select(w => new GanttWorkCenter(w.Code, w.NamePl, w.NameEn, w.LineCode)).ToList(),
            orders, ops, deps, conflicts, clock.SiteTimeZone.Id);
    }
}
