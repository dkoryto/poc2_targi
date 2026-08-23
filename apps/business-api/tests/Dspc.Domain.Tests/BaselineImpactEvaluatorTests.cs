using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Planning.Scheduling;
using FluentAssertions;

namespace Dspc.Domain.Tests;

/// <summary>Runs the deterministic fallback evaluator on the engine fixtures (packages/contracts/examples, T0 = 2026-09-07).</summary>
public class BaselineImpactEvaluatorTests
{
    public static string FixturesDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && d is not null; i++, d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "packages", "contracts", "examples");
            if (File.Exists(Path.Combine(candidate, "baseline.json"))) return candidate;
        }
        throw new DirectoryNotFoundException("packages/contracts/examples not found");
    }

    public static PlanningRequest Load(string name) => Json.Deserialize<PlanningRequest>(File.ReadAllText(Path.Combine(FixturesDir(), name)))!;

    [Fact]
    public void Baseline_input_reproduces_baseline_exactly()
    {
        var req = Load("baseline.json");
        var res = new BaselineImpactEvaluator().Evaluate(req);
        res.Kpi.MovedOperations.Should().Be(0);
        res.Kpi.DowntimeHours.Should().Be(0);
        res.Kpi.LateOrders.Should().Be(0);
        res.Orders.Should().OnlyContain(o => o.MaterialComplete);
        res.Solver.Should().Be("Heuristic fallback");
    }

    [Fact]
    public void Act40_delay_starves_WO014_by_4_days_with_36h_downtime()
    {
        var req = Load("act40-delay.json");
        var res = new BaselineImpactEvaluator().Evaluate(req);

        var wo014 = res.Orders.Single(o => o.OrderCode == "WO-2026-014");
        wo014.LatenessDays.Should().Be(4);
        wo014.Shortages.Should().ContainSingle(s => s.PartCode == "ACT-40" && s.Quantity == 8 && s.AvailableOn == new DateOnly(2026, 9, 25));

        var int30 = res.Operations.Single(o => o.OperationCode == "WO-2026-014/30");
        int30.WaitingForMaterial.Should().BeTrue();
        int30.Start.Should().Be(new DateTime(2026, 9, 25, 6, 0, 0));
        int30.End.Should().Be(new DateTime(2026, 9, 29, 10, 0, 0));

        res.Kpi.DowntimeHours.Should().Be(36);
        res.Kpi.LateOrders.Should().Be(1);

        // the fallback never pulls orders forward — WO-2026-019 stays at its baseline slot
        res.Operations.Where(o => o.OrderCode == "WO-2026-019").Should().OnlyContain(o => !o.Changed);

        res.Explanations.Should().Contain(e => e.ReasonCode == ReasonCodes.OrderDelayedMaterialShortage && e.OrderCode == "WO-2026-014"
            && (string)e.Params["partCode"]! == "ACT-40" && (decimal)e.Params["missingQty"]! == 8m);
        res.Explanations.Should().Contain(e => e.ReasonCode == ReasonCodes.OrderLateDue && e.OrderCode == "WO-2026-014");
    }

    [Fact]
    public void Hard_constraints_hold_no_overlap_and_sequence()
    {
        var res = new BaselineImpactEvaluator().Evaluate(Load("act40-delay.json"));
        foreach (var wc in res.Operations.GroupBy(o => o.WorkCenterCode))
        {
            var sorted = wc.OrderBy(o => o.Start).ToList();
            for (var i = 1; i < sorted.Count; i++) sorted[i].Start.Should().BeOnOrAfter(sorted[i - 1].End, $"{sorted[i].OperationCode} overlaps {sorted[i - 1].OperationCode}");
        }
        foreach (var order in res.Operations.GroupBy(o => o.OrderCode))
        {
            var seq = order.OrderBy(o => o.OperationCode).ToList();
            for (var i = 1; i < seq.Count; i++) seq[i].Start.Should().BeOnOrAfter(seq[i - 1].End);
        }
    }

    [Fact]
    public void Frozen_operations_never_move()
    {
        var req = Load("act40-delay.json");
        req.Materials.Single(m => m.PartCode == "MCU-X7").OnHand = 0; // would starve frozen WO-2026-012/10 if it were movable
        var res = new BaselineImpactEvaluator().Evaluate(req);
        var frozen = res.Operations.Where(o => o.OrderCode == "WO-2026-012").ToList();
        frozen.Should().OnlyContain(o => !o.Changed);
        res.Explanations.Should().Contain(e => e.ReasonCode == ReasonCodes.OrderFrozenKept && e.OrderCode == "WO-2026-012");
    }

    [Fact]
    public void Evaluation_is_deterministic()
    {
        var a = Json.Serialize(new BaselineImpactEvaluator().Evaluate(Load("act40-delay.json")));
        var b = Json.Serialize(new BaselineImpactEvaluator().Evaluate(Load("act40-delay.json")));
        Strip(a).Should().Be(Strip(b));
        static string Strip(string s) => System.Text.RegularExpressions.Regex.Replace(s, "\"elapsedMs\":\\d+", "");
    }
}

public class WorkCalendarTests
{
    private static WorkCalendar Cal(double factor = 1) => new([new PlanWorkCenter { Code = "WC", LineCode = "L", HoursPerDay = 16, CapacityFactor = factor }]);

    [Fact]
    public void Friday_36h_ends_tuesday_10()
        => Cal().AddWorkingHours("WC", new DateTime(2026, 9, 25, 6, 0, 0), 36).Should().Be(new DateTime(2026, 9, 29, 10, 0, 0));

    [Fact]
    public void Weekend_is_skipped()
        => Cal().NextWorkingTime("WC", new DateTime(2026, 9, 26, 9, 0, 0)).Should().Be(new DateTime(2026, 9, 28, 6, 0, 0));

    [Fact]
    public void Capacity_factor_halves_the_window()
        => Cal(0.5).AddWorkingHours("WC", new DateTime(2026, 9, 7, 6, 0, 0), 16).Should().Be(new DateTime(2026, 9, 8, 14, 0, 0));
}
