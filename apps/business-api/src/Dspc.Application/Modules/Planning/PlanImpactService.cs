using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Planning.Scheduling;

namespace Dspc.Application.Modules.Planning;

public sealed record PlanImpact(PlanModel Model, PlanningResponse Evaluation, GanttData Gantt);

public interface IPlanImpactEvaluator
{
    /// <summary>Baseline + current data (ETAs, lots, stock) without re-sequencing.</summary>
    Task<PlanImpact> EvaluateAsync(PlanOverrides? overrides, CancellationToken ct);
    /// <summary>Baseline exactly as stored (should evaluate to zero changes).</summary>
    Task<PlanImpact> BaselineAsync(CancellationToken ct);
}

public sealed class PlanImpactService(PlanModelBuilder builder, IDemoClock clock) : IPlanImpactEvaluator
{
    private readonly BaselineImpactEvaluator _evaluator = new();
    private PlanImpact? _cached;

    public async Task<PlanImpact> EvaluateAsync(PlanOverrides? overrides, CancellationToken ct)
    {
        if ((overrides is null || overrides.IsEmpty) && _cached is not null) return _cached;
        var model = await builder.BuildAsync(overrides, ct);
        var evaluation = _evaluator.Evaluate(model.Request);
        var impact = new PlanImpact(model, evaluation, GanttBuilder.Build(model, evaluation, clock));
        if (overrides is null || overrides.IsEmpty) _cached = impact;
        return impact;
    }

    public Task<PlanImpact> BaselineAsync(CancellationToken ct) => EvaluateAsync(null, ct);
}

public sealed record BaselineDto(Guid Id, int Version, DateTime? ApprovedAt, string? ApprovedBy, GanttData Gantt, PlanKpi Kpi, PlanningResponse Evaluation);

public sealed class PlanningQueries(IPlanImpactEvaluator impact)
{
    public async Task<BaselineDto> GetBaselineAsync(CancellationToken ct)
    {
        var i = await impact.EvaluateAsync(null, ct);
        return new BaselineDto(i.Model.Baseline.Id, i.Model.Baseline.Version, i.Model.Baseline.ApprovedAt, i.Model.Baseline.ApprovedBy, i.Gantt, i.Evaluation.Kpi, i.Evaluation);
    }
}
