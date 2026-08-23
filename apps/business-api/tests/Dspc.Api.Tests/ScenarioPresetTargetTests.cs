using System.Net.Http.Json;
using FluentAssertions;

namespace Dspc.Api.Tests;

/// <summary>
/// A tile that cannot change anything is a dead moment in front of an audience. Both defects that
/// produced one were defects of target selection, which is what these tests pin down:
/// the priority tile used to promote the order that was already top priority (so the ranking, and
/// therefore the plan, could not change), and the lot tile used to pick a lot already consumed by a
/// finished product rather than one an open order still depends on.
/// Whether the engine then actually re-sequences is asserted end to end in tests/e2e, which runs
/// against the real solver; the API test suite uses the deterministic fallback, where by design the
/// proposed plan equals the un-resequenced one.
/// </summary>
[Collection("api")]
public class ScenarioPresetTargetTests(ApiFixture fx)
{
    [Theory]
    [InlineData("SITE-01")]
    [InlineData("SITE-02")]
    [InlineData("SITE-03")]
    [InlineData("SITE-04")]
    public async Task Priority_preset_never_targets_an_order_that_is_already_top_priority(string site)
    {
        using var client = await fx.AsAsync("ProductionPlanner");

        var presets = await client.GetFromJsonAsync<List<PresetShape>>(
            $"/api/v1/planning/scenarios/presets?siteCode={site}", ApiFixture.Json);
        var priority = presets!.SingleOrDefault(p => p.Key == "PRIORITY_WO014");
        if (priority is null) return;   // not offered on this plant

        var target = priority.Changes.Single().OrderCode;
        target.Should().NotBeNullOrWhiteSpace();
        priority.TitleParams.Should().ContainKey("orderCode")
            .WhoseValue.Should().Be(target, "the tile names the order it actually promotes");

        var plan = await client.GetFromJsonAsync<BaselineShape>(
            $"/api/v1/planning/baseline?siteCode={site}", ApiFixture.Json);
        var orders = plan!.Gantt.Orders;
        var top = orders.Max(o => o.Priority);
        var targeted = orders.Single(o => o.Code == target);

        targeted.Priority.Should().BeLessThan(top,
            "promoting the order that already outranks every other cannot change the plan");
        targeted.Status.Should().NotBe("InProgress", "an order already running cannot be expedited");
    }

    [Theory]
    [InlineData("SITE-01")]
    [InlineData("SITE-03")]
    public async Task Lot_preset_targets_a_lot_an_open_order_still_depends_on(string site)
    {
        using var client = await fx.AsAsync("ProductionPlanner");

        var presets = await client.GetFromJsonAsync<List<PresetShape>>(
            $"/api/v1/planning/scenarios/presets?siteCode={site}", ApiFixture.Json);
        var block = presets!.SingleOrDefault(p => p.Key == "BLOCK_LOT_HTS22");
        if (block is null) return;   // this plant does not stock the part

        var lotNumber = block.Changes.Single().LotNumber;
        lotNumber.Should().NotBeNullOrWhiteSpace();

        using var quality = await fx.AsAsync("QualityInspector");
        var lot = await quality.GetFromJsonAsync<LotShape>($"/api/v1/lots/{lotNumber}", ApiFixture.Json);
        lot!.SiteCode.Should().Be(site, "the tile must target this plant's own lot");
        lot.ReservedBy.Should().NotBeEmpty(
            "blocking a lot nothing is waiting for starves no operation, so the tile would do nothing");
    }

    private sealed record PresetShape(string Key, string TitleKey, List<ChangeShape> Changes, bool Featured, Dictionary<string, string>? TitleParams);
    private sealed record ChangeShape(string Type, string? OrderCode, string? LotNumber, int? Priority);
    private sealed record BaselineShape(GanttShape Gantt);
    private sealed record GanttShape(List<OrderShape> Orders);
    private sealed record OrderShape(string Code, int Priority, string Status);
    private sealed record LotShape(string LotNumber, string SiteCode, List<string> ReservedBy);
}
