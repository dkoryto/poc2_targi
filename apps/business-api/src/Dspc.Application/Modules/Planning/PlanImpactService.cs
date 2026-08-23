using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Planning.Scheduling;

namespace Dspc.Application.Modules.Planning;

public sealed record PlanImpact(PlanModel Model, PlanningResponse Evaluation, GanttData Gantt);

public interface IPlanImpactEvaluator
{
    /// <summary>Baseline + current data (ETAs, lots, stock) for one plant, without re-sequencing.</summary>
    Task<PlanImpact> EvaluateAsync(Guid siteId, PlanOverrides? overrides, CancellationToken ct);
    /// <summary>Baseline exactly as stored (should evaluate to zero changes).</summary>
    Task<PlanImpact> BaselineAsync(Guid siteId, CancellationToken ct);
}

public sealed class PlanImpactService(PlanModelBuilder builder, IDemoClock clock) : IPlanImpactEvaluator
{
    private readonly BaselineImpactEvaluator _evaluator = new();
    private readonly Dictionary<Guid, PlanImpact> _cached = new();

    public async Task<PlanImpact> EvaluateAsync(Guid siteId, PlanOverrides? overrides, CancellationToken ct)
    {
        var plain = overrides is null || overrides.IsEmpty;
        if (plain && _cached.TryGetValue(siteId, out var hit)) return hit;
        var model = await builder.BuildAsync(siteId, overrides, ct);
        var evaluation = _evaluator.Evaluate(model.Request);
        var impact = new PlanImpact(model, evaluation, GanttBuilder.Build(model, evaluation, clock));
        if (plain) _cached[siteId] = impact;
        return impact;
    }

    public Task<PlanImpact> BaselineAsync(Guid siteId, CancellationToken ct) => EvaluateAsync(siteId, null, ct);
}

public sealed record BaselineDto(Guid Id, int Version, DateTime? ApprovedAt, string? ApprovedBy, GanttData Gantt, PlanKpi Kpi, PlanningResponse Evaluation);

public sealed class PlanningQueries(IPlanImpactEvaluator impact)
{
    public async Task<BaselineDto> GetBaselineAsync(Guid siteId, CancellationToken ct)
    {
        var i = await impact.EvaluateAsync(siteId, null, ct);
        return new BaselineDto(i.Model.Baseline.Id, i.Model.Baseline.Version, i.Model.Baseline.ApprovedAt, i.Model.Baseline.ApprovedBy, i.Gantt, i.Evaluation.Kpi, i.Evaluation);
    }
}
