using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Domain.Common;
using Dspc.Infrastructure.Planning;
using Dspc.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dspc.Domain.Tests;

/// <summary>Scenario change mapping, compare maths and the engine-unavailable fallback — all without a database.</summary>
public class PlanningScenarioTests
{
    private static readonly Guid LineId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Dictionary<Guid, DateOnly> Etas = new() { [LineId] = new DateOnly(2026, 9, 15) };

    [Fact]
    public void Delay_inbound_shifts_the_line_eta()
    {
        var o = ScenarioCalculations.BuildOverrides(
            [new ScenarioChangeDto(ScenarioChangeType.DELAY_INBOUND, PoLineId: LineId, Days: 10)], Etas);

        o.EtaByLineId.Should().ContainKey(LineId).WhoseValue.Should().Be(new DateOnly(2026, 9, 25));
        o.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Delay_inbound_accepts_a_pull_in()
    {
        var o = ScenarioCalculations.BuildOverrides(
            [new ScenarioChangeDto(ScenarioChangeType.DELAY_INBOUND, PoLineId: LineId, Days: -3)], Etas);
        o.EtaByLineId[LineId].Should().Be(new DateOnly(2026, 9, 12));
    }

    [Fact]
    public void Block_lot_priority_capacity_and_order_delay_map_to_overrides()
    {
        var o = ScenarioCalculations.BuildOverrides(
        [
            new ScenarioChangeDto(ScenarioChangeType.BLOCK_LOT, LotNumber: "HTS-22-2608"),
            new ScenarioChangeDto(ScenarioChangeType.PRIORITY, OrderCode: "WO-2026-014", Priority: 5),
            new ScenarioChangeDto(ScenarioChangeType.CAPACITY, WorkCenterCode: "WC-INT", Factor: 0.5),
            new ScenarioChangeDto(ScenarioChangeType.DELAY_ORDER, OrderCode: "WO-2026-019", Days: 7)
        ], Etas);

        o.BlockedLots.Should().Contain("hts-22-2608");            // case-insensitive set
        o.PriorityByOrder["WO-2026-014"].Should().Be(5);
        o.CapacityFactorByWorkCenter["WC-INT"].Should().Be(0.5);
        o.DelayDaysByOrder["WO-2026-019"].Should().Be(7);
        o.EtaByLineId.Should().BeEmpty();
    }

    [Fact]
    public void Incomplete_changes_are_ignored_rather_than_throwing()
    {
        var o = ScenarioCalculations.BuildOverrides(
        [
            new ScenarioChangeDto(ScenarioChangeType.DELAY_INBOUND, PoLineId: Guid.NewGuid(), Days: 5),  // unknown line
            new ScenarioChangeDto(ScenarioChangeType.PRIORITY, OrderCode: "WO-1"),                        // no priority
            new ScenarioChangeDto(ScenarioChangeType.BLOCK_LOT, LotNumber: "  ")
        ], Etas);
        o.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Moved_operations_and_kpi_delta_are_computed_from_the_two_plans()
    {
        var before = Plan(("WO-1/10", "WC-CUT", 6, 14), ("WO-1/20", "WC-INT", 20, 28));
        var after = Plan(("WO-1/10", "WC-CUT", 6, 14), ("WO-1/20", "WC-INT", 12, 20));

        var moved = ScenarioCalculations.MovedOperations(before, after);
        moved.Should().HaveCount(1);
        moved[0].OperationCode.Should().Be("WO-1/20");
        moved[0].ShiftDays.Should().Be(-0.3);                       // 8 h earlier
        moved[0].Before.Start.Should().Be(Base.AddHours(20));
        moved[0].After.Start.Should().Be(Base.AddHours(12));

        var delta = ScenarioCalculations.KpiDelta(
            new PlanKpi { DowntimeHours = 36, LateOrders = 1, TotalLatenessDays = 4, MovedOperations = 6, OnTimeRate = 0.8 },
            new PlanKpi { DowntimeHours = 8, LateOrders = 1, TotalLatenessDays = 4, MovedOperations = 8, OnTimeRate = 0.9 });
        delta.DowntimeHours.Should().Be(-28);
        delta.LateOrders.Should().Be(0);
        delta.MovedOperations.Should().Be(2);
        delta.OnTimeRate.Should().Be(0.1);
    }

    [Fact]
    public async Task Engine_unreachable_degrades_to_the_deterministic_fallback()
    {
        var request = BaselineImpactEvaluatorTests.Load("act40-delay.json");
        var client = new PlanningEngineClient(
            new HttpClient(new UnreachableHandler()) { BaseAddress = new Uri("http://127.0.0.1:1/") },
            Options.Create(new PlanningEngineOptions { BaseUrl = "http://127.0.0.1:1", TimeoutMs = 500 }),
            new PlanningEngineMetrics(), NullLogger<PlanningEngineClient>.Instance);

        var outcome = await client.SolveAsync(request, CancellationToken.None);

        outcome.UsedFallback.Should().BeTrue();
        outcome.Response.Solver.Should().Be("Heuristic fallback");
        outcome.Response.Status.Should().Be("FALLBACK");
        outcome.Response.Operations.Should().NotBeEmpty("the fallback must still return a usable plan");
        outcome.Response.Orders.Should().Contain(o => o.OrderCode == "WO-2026-014" && o.LatenessDays == 4);
        outcome.Response.Kpi.DowntimeHours.Should().Be(36);
    }

    [Fact]
    public async Task Engine_error_status_also_falls_back()
    {
        var client = new PlanningEngineClient(
            new HttpClient(new StatusHandler(System.Net.HttpStatusCode.InternalServerError)) { BaseAddress = new Uri("http://engine/") },
            Options.Create(new PlanningEngineOptions { TimeoutMs = 1000 }),
            new PlanningEngineMetrics(), NullLogger<PlanningEngineClient>.Instance);

        var outcome = await client.SolveAsync(BaselineImpactEvaluatorTests.Load("baseline.json"), CancellationToken.None);
        outcome.UsedFallback.Should().BeTrue();
        outcome.FallbackReason.Should().Contain("500");
    }

    // ---------------------------------------------------------------- helpers

    private static readonly DateTime Base = new(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

    private static GanttData Plan(params (string Code, string Wc, int StartHour, int EndHour)[] ops) => new(
        new DateOnly(2026, 9, 7), new DateOnly(2026, 11, 30), [],
        [],
        ops.Select(o => new GanttOperation("WO-1", o.Code, 10, o.Wc,
            Base.AddHours(o.StartHour), Base.AddHours(o.EndHour),
            false, "Planned", false, false, 0, null, null)).ToList(),
        [], []);

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }

    private sealed class StatusHandler(System.Net.HttpStatusCode code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent("boom") });
    }
}
