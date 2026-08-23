using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Dspc.Api.Tests;

/// <summary>
/// The four demo plants (docs/architecture/multi-site.md). The overriding rule is that SITE-01 keeps its
/// golden-path numbers; the rest verify that plants are genuinely isolated and each demonstrates its own scenario.
/// </summary>
[Collection("api")]
public class MultiSiteTests(ApiFixture fx)
{
    private static readonly string[] AllPlants = ["SITE-01", "SITE-02", "SITE-03", "SITE-04"];

    [Fact]
    public async Task Sites_endpoint_lists_the_four_plants_with_their_featured_scenario()
    {
        using var c = await fx.AsAsync("OperationsDirector");
        var sites = await c.GetFromJsonAsync<JsonElement>("/api/v1/sites", ApiFixture.Json);
        var byCode = sites.EnumerateArray().ToDictionary(s => s.GetProperty("code").GetString()!);

        byCode.Keys.Should().BeEquivalentTo(AllPlants);
        byCode["SITE-01"].GetProperty("name").GetString().Should().Be("Zakład Kielce");
        byCode["SITE-01"].GetProperty("city").GetString().Should().Be("Kielce");
        byCode["SITE-01"].GetProperty("lat").GetDouble().Should().BeApproximately(50.87, 0.001);
        byCode["SITE-01"].GetProperty("lon").GetDouble().Should().BeApproximately(20.63, 0.001);
        byCode["SITE-01"].GetProperty("isDefault").GetBoolean().Should().BeTrue();

        byCode["SITE-01"].GetProperty("featuredScenarioKey").GetString().Should().Be("DELAY_ACT40_10D");
        byCode["SITE-02"].GetProperty("featuredScenarioKey").GetString().Should().Be("DELAY_MCUX7_14D");
        byCode["SITE-03"].GetProperty("featuredScenarioKey").GetString().Should().Be("BLOCK_LOT_HTS22");
        byCode["SITE-04"].GetProperty("featuredScenarioKey").GetString().Should().Be("CAPACITY_INT_50");
    }

    [Fact]
    public async Task Each_plant_offers_exactly_one_featured_preset_matching_its_site()
    {
        using var c = await fx.AsAsync("ProductionPlanner");
        foreach (var plant in AllPlants)
        {
            var sites = await c.GetFromJsonAsync<JsonElement>("/api/v1/sites", ApiFixture.Json);
            var featuredKey = sites.EnumerateArray().First(s => s.GetProperty("code").GetString() == plant)
                .GetProperty("featuredScenarioKey").GetString();

            var presets = await c.GetFromJsonAsync<JsonElement>($"/api/v1/planning/scenarios/presets?siteCode={plant}", ApiFixture.Json);
            var featured = presets.EnumerateArray().Where(p => p.GetProperty("featured").GetBoolean()).ToList();
            featured.Should().HaveCount(1, "{0} must demonstrate exactly one headline scenario", plant);
            featured[0].GetProperty("key").GetString().Should().Be(featuredKey);
        }
    }

    [Fact]
    public async Task Block_lot_preset_targets_a_lot_the_plant_actually_consumed()
    {
        using var c = await fx.AsAsync("ProductionPlanner");
        async Task<string?> BlockLotOf(string plant)
        {
            var presets = await c.GetFromJsonAsync<JsonElement>($"/api/v1/planning/scenarios/presets?siteCode={plant}", ApiFixture.Json);
            var tile = presets.EnumerateArray().FirstOrDefault(p => p.GetProperty("key").GetString() == "BLOCK_LOT_HTS22");
            return tile.ValueKind == JsonValueKind.Undefined ? null : tile.GetProperty("changes")[0].GetProperty("lotNumber").GetString();
        }
        // blocking a lot that was never built into a product would invalidate nothing
        (await BlockLotOf("SITE-01")).Should().Be("HTS-22-2608");
        (await BlockLotOf("SITE-03")).Should().Be("HTS-22-3110");
    }

    [Fact]
    public async Task Kpis_are_per_plant_and_never_mix_plants()
    {
        using var c = await fx.AsAsync("OperationsDirector");
        var totals = new Dictionary<string, double>();
        foreach (var plant in AllPlants)
        {
            var kpis = await c.GetFromJsonAsync<JsonElement>($"/api/v1/dashboard/kpis?siteCode={plant}", ApiFixture.Json);
            var high = kpis.GetProperty("items").EnumerateArray().First(i => i.GetProperty("code").GetString() == "HIGH_RISK_DELIVERIES").GetProperty("value").GetDouble();
            totals[plant] = high;
        }
        totals["SITE-01"].Should().Be(3, "Kielce's seeded high-risk count is part of the golden path");
        totals.Values.Should().OnlyContain(v => v < 10, "a plant must not be counting another plant's deliveries");
        totals.Values.Distinct().Should().HaveCountGreaterThan(1, "the plants tell different stories");
    }

    [Theory]
    [InlineData("/api/v1/dashboard/kpis")]
    [InlineData("/api/v1/dashboard/map")]
    [InlineData("/api/v1/dashboard/risk-heatmap")]
    [InlineData("/api/v1/dashboard/quality-status")]
    [InlineData("/api/v1/dashboard/plan")]
    [InlineData("/api/v1/planning/baseline")]
    [InlineData("/api/v1/purchase-orders")]
    [InlineData("/api/v1/lots")]
    [InlineData("/api/v1/passports")]
    [InlineData("/api/v1/inventory")]
    public async Task Unknown_plant_is_a_404_on_every_scoped_endpoint(string path)
    {
        using var c = await fx.AsAsync("OperationsDirector");
        var res = await c.GetAsync($"{path}?siteCode=SITE-99");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} must reject an unknown plant", path);
    }

    [Fact]
    public async Task Purchase_orders_and_lots_never_leak_across_plants()
    {
        using var c = await fx.AsAsync("OperationsDirector");
        var kielce = await c.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders?siteCode=SITE-01", ApiFixture.Json);
        var pila = await c.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders?siteCode=SITE-02", ApiFixture.Json);

        var kielceCodes = kielce.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()!).ToList();
        var pilaCodes = pila.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()!).ToList();
        kielceCodes.Should().OnlyContain(x => x.StartsWith("PO-2026-0"));
        pilaCodes.Should().OnlyContain(x => x.StartsWith("PO-2026-1"));
        kielceCodes.Should().NotIntersectWith(pilaCodes);

        var zamoscLots = await c.GetFromJsonAsync<JsonElement>("/api/v1/lots?siteCode=SITE-03", ApiFixture.Json);
        var lotNumbers = zamoscLots.GetProperty("items").EnumerateArray().Select(l => l.GetProperty("lotNumber").GetString()!).ToList();
        lotNumbers.Should().Contain("HTS-22-3110").And.NotContain("HTS-22-2608");
    }

    [Fact]
    public async Task Supplier_may_only_reach_the_plants_it_delivers_to()
    {
        // SUP-01 (Nordstal) ships steel to Kielce and Zamość, but nothing to Piła.
        using var nordstal = await fx.AsAsync("SupplierUser", "SUP-01");
        var sites = await nordstal.GetFromJsonAsync<JsonElement>("/api/v1/sites", ApiFixture.Json);
        var codes = sites.EnumerateArray().Select(s => s.GetProperty("code").GetString()).ToList();
        codes.Should().BeEquivalentTo(["SITE-01", "SITE-03"]);

        (await nordstal.GetAsync("/api/v1/purchase-orders?siteCode=SITE-03")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await nordstal.GetAsync("/api/v1/purchase-orders?siteCode=SITE-02")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var me = await nordstal.GetFromJsonAsync<JsonElement>("/api/v1/auth/me", ApiFixture.Json);
        me.GetProperty("availableSites").EnumerateArray().Select(x => x.GetString()).Should().BeEquivalentTo(["SITE-01", "SITE-03"]);
    }

    [Fact]
    public async Task A_scenario_may_not_span_two_plants()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var res = await planner.PostAsJsonAsync("/api/v1/planning/scenarios", new
        {
            name = "cross-plant",
            changes = new object[]
            {
                new { type = "BLOCK_LOT", lotNumber = "HTS-22-2608" },   // Kielce
                new { type = "BLOCK_LOT", lotNumber = "HTS-22-3110" },   // Zamość
            }
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("one plant");
    }

    [Fact]
    public async Task Each_plant_has_its_own_baseline_and_scenarios_list()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var seen = new HashSet<Guid>();
        foreach (var plant in AllPlants)
        {
            var baseline = await planner.GetFromJsonAsync<JsonElement>($"/api/v1/planning/baseline?siteCode={plant}", ApiFixture.Json);
            seen.Add(baseline.GetProperty("id").GetGuid()).Should().BeTrue("{0} must have its own baseline", plant);
            baseline.GetProperty("gantt").GetProperty("operations").GetArrayLength().Should().BeGreaterThan(0);
        }
    }
}

/// <summary>
/// Each plant's headline scenario must actually change something on that plant's own data — a demo tile that
/// resolves but produces a flat result is worse than no tile at all. These run through the fallback scheduler
/// (the fixture's engine address is unreachable), which is enough to prove the data supports the story.
/// </summary>
[Collection("api")]
public class FeaturedScenarioTests(ApiFixture fx)
{
    private static async Task<JsonElement> RunFeaturedAsync(HttpClient planner, string plant)
    {
        var presets = await planner.GetFromJsonAsync<JsonElement>($"/api/v1/planning/scenarios/presets?siteCode={plant}", ApiFixture.Json);
        var preset = presets.EnumerateArray().Single(p => p.GetProperty("featured").GetBoolean());
        var key = preset.GetProperty("key").GetString();

        var create = await planner.PostAsJsonAsync("/api/v1/planning/scenarios",
            new { name = $"{plant}:{key}", changes = preset.GetProperty("changes"), presetKey = key });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json)).GetProperty("id").GetString()!;
        (await planner.PostAsync($"/api/v1/planning/scenarios/{id}/run", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            var current = await planner.GetFromJsonAsync<JsonElement>($"/api/v1/planning/scenarios/{id}", ApiFixture.Json);
            if (current.GetProperty("status").GetString() is "Completed") return current;
            if (current.GetProperty("status").GetString() is "Failed") throw new Xunit.Sdk.XunitException($"{plant} featured scenario failed");
            await Task.Delay(200);
        }
        throw new TimeoutException($"{plant} featured scenario did not complete");
    }

    private static IEnumerable<string> ReasonCodes(JsonElement scenario) =>
        scenario.GetProperty("explanations").EnumerateArray().Select(e => e.GetProperty("reasonCode").GetString()!);

    [Fact]
    public async Task Pila_MCU_X7_delay_makes_an_electronics_order_late()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var s = await RunFeaturedAsync(planner, "SITE-02");
        ReasonCodes(s).Should().Contain("ORDER_DELAYED_MATERIAL_SHORTAGE");
        s.GetProperty("kpiAfter").GetProperty("lateOrders").GetInt32().Should().BeGreaterThan(0);
        var shortage = s.GetProperty("explanations").EnumerateArray()
            .First(e => e.GetProperty("reasonCode").GetString() == "ORDER_DELAYED_MATERIAL_SHORTAGE");
        shortage.GetProperty("params").GetProperty("partCode").GetString().Should().Be("MCU-X7");
        shortage.GetProperty("orderCode").GetString().Should().StartWith("WO-2026-1");
    }

    [Fact]
    public async Task Zamosc_lot_block_starves_its_own_orders()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var s = await RunFeaturedAsync(planner, "SITE-03");
        var shortages = s.GetProperty("explanations").EnumerateArray()
            .Where(e => e.GetProperty("reasonCode").GetString() == "ORDER_DELAYED_MATERIAL_SHORTAGE").ToList();
        shortages.Should().NotBeEmpty("blocking the steel lot must starve Zamość's own orders");
        shortages.Should().OnlyContain(e => e.GetProperty("params").GetProperty("partCode").GetString() == "HTS-22");
        shortages.Should().OnlyContain(e => e.GetProperty("orderCode").GetString()!.StartsWith("WO-2026-2"));
    }

    [Fact]
    public async Task Leszno_halved_integration_cell_pushes_orders_past_their_due_dates()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");

        // The plant is on time until the cell is halved.
        var baseline = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/baseline?siteCode=SITE-04", ApiFixture.Json);
        baseline.GetProperty("kpi").GetProperty("lateOrders").GetInt32().Should().Be(0, "Leszno's baseline is feasible");

        var s = await RunFeaturedAsync(planner, "SITE-04");
        // Compared against the baseline, not against "before": with the engine unreachable the fallback returns
        // "after == before" by design (ADR 0005), so the change must be visible on the scenario itself.
        s.GetProperty("kpiBefore").GetProperty("lateOrders").GetInt32().Should().BeGreaterThan(0, "halving the integration cell must cost delivery dates");
        s.GetProperty("kpiAfter").GetProperty("totalLatenessDays").GetInt32().Should().BeGreaterThan(0);
        ReasonCodes(s).Should().Contain("ORDER_LATE_DUE");
    }

    [Fact]
    public async Task Kielce_keeps_the_golden_path_numbers()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var s = await RunFeaturedAsync(planner, "SITE-01");
        s.GetProperty("kpiBefore").GetProperty("downtimeHours").GetDouble().Should().Be(36);
        var late = s.GetProperty("explanations").EnumerateArray()
            .First(e => e.GetProperty("reasonCode").GetString() == "ORDER_DELAYED_MATERIAL_SHORTAGE");
        late.GetProperty("orderCode").GetString().Should().Be("WO-2026-014");
        late.GetProperty("params").GetProperty("partCode").GetString().Should().Be("ACT-40");
        s.GetProperty("kpiAfter").GetProperty("lateOrders").GetInt32().Should().Be(1);
    }
}
