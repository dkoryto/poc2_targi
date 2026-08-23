using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Infrastructure.Persistence;
using Dspc.Infrastructure.Planning;
using Dspc.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dspc.Api.Tests;

/// <summary>
/// What-If end to end against the seeded database. The fixture points the engine at an unreachable address, so these
/// runs exercise the deterministic fallback; <see cref="PlanningEngineIntegrationTests"/> covers the real Java engine.
/// </summary>
[Collection("api")]
public class PlanningScenarioApiTests(ApiFixture fx)
{
    private static async Task<JsonElement> RunPresetAsync(HttpClient planner, string presetKey)
    {
        var presets = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/scenarios/presets", ApiFixture.Json);
        var preset = presets.EnumerateArray().Single(p => p.GetProperty("key").GetString() == presetKey);

        var create = await planner.PostAsJsonAsync("/api/v1/planning/scenarios",
            new { name = presetKey, changes = preset.GetProperty("changes"), presetKey });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var scenario = await create.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        var id = scenario.GetProperty("id").GetString()!;

        var run = await planner.PostAsync($"/api/v1/planning/scenarios/{id}/run", null);
        run.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            var current = await planner.GetFromJsonAsync<JsonElement>($"/api/v1/planning/scenarios/{id}", ApiFixture.Json);
            var status = current.GetProperty("status").GetString();
            if (status is "Completed") return current;
            if (status is "Failed") throw new Xunit.Sdk.XunitException("scenario failed: " + current.GetProperty("errorMessage"));
            await Task.Delay(200);
        }
        throw new TimeoutException($"scenario {id} did not complete");
    }

    [Fact]
    public async Task Presets_expose_the_five_demo_tiles_with_resolved_targets()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var presets = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/scenarios/presets", ApiFixture.Json);
        var keys = presets.EnumerateArray().Select(p => p.GetProperty("key").GetString()).ToList();
        keys.Should().BeEquivalentTo(["DELAY_ACT40_10D", "DELAY_MCUX7_14D", "BLOCK_LOT_HTS22", "PRIORITY_WO014", "CAPACITY_INT_50"]);

        var act40 = presets.EnumerateArray().First(p => p.GetProperty("key").GetString() == "DELAY_ACT40_10D");
        act40.GetProperty("titleKey").GetString().Should().Be("planning.presets.ACT40_DELAY");
        var change = act40.GetProperty("changes")[0];
        change.GetProperty("type").GetString().Should().Be("DELAY_INBOUND");
        change.GetProperty("days").GetInt32().Should().Be(10);
        change.GetProperty("partCode").GetString().Should().Be("ACT-40");
        Guid.Parse(change.GetProperty("poLineId").GetString()!).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Act40_scenario_reports_36h_downtime_before_replanning_and_flags_WO014()
    {
        await fx.ResetAsync();
        using var planner = await fx.AsAsync("ProductionPlanner");
        var scenario = await RunPresetAsync(planner, "DELAY_ACT40_10D");

        scenario.GetProperty("kpiBefore").GetProperty("downtimeHours").GetDouble().Should().Be(36,
            "the ACT-40 delay idles the WC-INT window reserved for WO-2026-014");
        scenario.GetProperty("before").GetProperty("operations").GetArrayLength().Should().BeGreaterThan(0);
        scenario.GetProperty("after").GetProperty("operations").GetArrayLength().Should().BeGreaterThan(0);
        scenario.GetProperty("elapsedMs").GetInt32().Should().BeLessThan(3000);

        var lateOrder = scenario.GetProperty("after").GetProperty("orders").EnumerateArray()
            .Single(o => o.GetProperty("code").GetString() == "WO-2026-014");
        lateOrder.GetProperty("latenessDays").GetInt32().Should().Be(4);

        var reasons = scenario.GetProperty("explanations").EnumerateArray()
            .Select(e => e.GetProperty("reasonCode").GetString()).ToList();
        reasons.Should().Contain("ORDER_DELAYED_MATERIAL_SHORTAGE");

        var shortage = scenario.GetProperty("explanations").EnumerateArray()
            .First(e => e.GetProperty("reasonCode").GetString() == "ORDER_DELAYED_MATERIAL_SHORTAGE");
        shortage.GetProperty("orderCode").GetString().Should().Be("WO-2026-014");
        shortage.GetProperty("params").GetProperty("partCode").GetString().Should().Be("ACT-40");

        scenario.GetProperty("consequences").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Unreachable_engine_is_reported_as_heuristic_fallback_but_still_returns_a_plan()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var scenario = await RunPresetAsync(planner, "CAPACITY_INT_50");

        scenario.GetProperty("solver").GetString().Should().Be("Heuristic fallback");
        scenario.GetProperty("explanations").EnumerateArray()
            .Select(e => e.GetProperty("reasonCode").GetString()).Should().Contain("FALLBACK_USED");
        scenario.GetProperty("after").GetProperty("operations").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Compare_lists_moved_operations_and_the_kpi_delta()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var scenario = await RunPresetAsync(planner, "DELAY_ACT40_10D");
        var id = scenario.GetProperty("id").GetString();

        var compare = await planner.GetFromJsonAsync<JsonElement>($"/api/v1/planning/scenarios/{id}/compare", ApiFixture.Json);
        compare.GetProperty("movedOperations").ValueKind.Should().Be(JsonValueKind.Array);
        compare.GetProperty("kpiDelta").GetProperty("downtimeHours").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public async Task Running_a_scenario_never_touches_the_baseline_until_it_is_approved()
    {
        await fx.ResetAsync();
        using var planner = await fx.AsAsync("ProductionPlanner");
        var before = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/baseline", ApiFixture.Json);
        var version = before.GetProperty("version").GetInt32();

        await RunPresetAsync(planner, "DELAY_ACT40_10D");

        var after = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/baseline", ApiFixture.Json);
        after.GetProperty("version").GetInt32().Should().Be(version);
    }

    [Fact]
    public async Task Approval_creates_the_next_baseline_version_keeps_the_previous_and_is_audited()
    {
        await fx.ResetAsync();
        using var planner = await fx.AsAsync("ProductionPlanner");
        var baselineBefore = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/baseline", ApiFixture.Json);
        var version = baselineBefore.GetProperty("version").GetInt32();

        var scenario = await RunPresetAsync(planner, "DELAY_ACT40_10D");
        var id = scenario.GetProperty("id").GetString()!;

        var approve = await planner.PostAsync($"/api/v1/planning/scenarios/{id}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await approve.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        approved.GetProperty("status").GetString().Should().Be("Approved");
        approved.GetProperty("baselineVersion").GetInt32().Should().Be(version + 1);
        approved.GetProperty("approvedBy").GetString().Should().NotBeNullOrEmpty();

        var baselineAfter = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/baseline", ApiFixture.Json);
        baselineAfter.GetProperty("version").GetInt32().Should().Be(version + 1);

        using var scope = fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PlanningBaselines.CountAsync()).Should().BeGreaterThan(1, "the previous baseline version is retained");
        (await db.AuditEvents.AnyAsync(a => a.Action == "Planning.PlanApproved")).Should().BeTrue();
        (await db.OutboxMessages.AnyAsync(m => m.EventName == "ProductionPlanApproved")).Should().BeTrue();
        (await db.OutboxMessages.AnyAsync(m => m.EventName == "PlanningScenarioCompleted")).Should().BeTrue();

        // an approved scenario is terminal
        var again = await planner.PostAsync($"/api/v1/planning/scenarios/{id}/run", null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await fx.ResetAsync();
    }

    [Fact]
    public async Task Reject_and_save_record_a_decision_without_a_new_baseline()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var scenario = await RunPresetAsync(planner, "PRIORITY_WO014");
        var id = scenario.GetProperty("id").GetString();

        var rejected = await (await planner.PostAsync($"/api/v1/planning/scenarios/{id}/reject", null))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        rejected.GetProperty("status").GetString().Should().Be("Rejected");

        var saved = await (await planner.PostAsync($"/api/v1/planning/scenarios/{id}/save", null))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        saved.GetProperty("status").GetString().Should().Be("Saved");
    }

    [Fact]
    public async Task Invalid_changes_are_rejected_with_problem_details()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var res = await planner.PostAsJsonAsync("/api/v1/planning/scenarios", new
        {
            name = "bad",
            changes = new object[] { new { type = "CAPACITY", workCenterCode = "WC-NOPE", factor = 0.5 } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        problem.GetProperty("errors").GetProperty("changes[0].workCenterCode").GetArrayLength().Should().Be(1);

        var empty = await planner.PostAsJsonAsync("/api/v1/planning/scenarios", new { name = "x", changes = Array.Empty<object>() });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Auditor_may_read_but_not_create_run_or_approve_and_suppliers_are_shut_out()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        var scenario = await RunPresetAsync(planner, "BLOCK_LOT_HTS22");
        var id = scenario.GetProperty("id").GetString();

        using var auditor = await fx.AsAsync("Auditor");
        (await auditor.GetAsync($"/api/v1/planning/scenarios/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await auditor.PostAsync($"/api/v1/planning/scenarios/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await auditor.PostAsync($"/api/v1/planning/scenarios/{id}/run", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await auditor.PostAsJsonAsync("/api/v1/planning/scenarios", new { name = "x", changes = new object[] { new { type = "BLOCK_LOT", lotNumber = "HTS-22-2608" } } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var supplier = await fx.AsAsync("SupplierUser", "SUP-02");
        (await supplier.GetAsync("/api/v1/planning/scenarios")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await supplier.GetAsync($"/api/v1/planning/scenarios/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var anon = fx.Anonymous();
        (await anon.GetAsync("/api/v1/planning/scenarios")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Scenario_list_summarises_recent_runs()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        await RunPresetAsync(planner, "DELAY_MCUX7_14D");
        var list = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/scenarios", ApiFixture.Json);
        list.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        var first = list.GetProperty("items")[0];
        first.GetProperty("changeCount").GetInt32().Should().BeGreaterThan(0);
        first.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
    }
}

/// <summary>
/// Exercises the real Java engine with the exact request the API builds. Skipped (with a warning in the test name's
/// message) when the engine is not running — start it with <c>docker compose --profile demo up -d planning-engine</c>.
/// </summary>
[Collection("api")]
public class PlanningEngineIntegrationTests(ApiFixture fx)
{
    private static readonly string EngineUrl = Environment.GetEnvironmentVariable("PLANNING_ENGINE_URL") ?? "http://localhost:8081";

    private static async Task<bool> EngineReachableAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var r = await http.GetAsync($"{EngineUrl}/actuator/health");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    [Fact]
    public async Task Real_engine_pulls_WO_2026_019_forward_and_cuts_downtime_from_36_to_8()
    {
        if (!await EngineReachableAsync())
        {
            Console.WriteLine($"SKIPPED: planning engine not reachable at {EngineUrl} — fallback behaviour is covered by PlanningScenarioApiTests.");
            return;
        }

        await fx.ResetAsync();
        using var planner = await fx.AsAsync("ProductionPlanner");
        var presets = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/scenarios/presets", ApiFixture.Json);
        var preset = presets.EnumerateArray().Single(p => p.GetProperty("key").GetString() == "DELAY_ACT40_10D");
        var create = await planner.PostAsJsonAsync("/api/v1/planning/scenarios",
            new { name = "engine", changes = preset.GetProperty("changes"), presetKey = "DELAY_ACT40_10D" });
        var id = Guid.Parse((await create.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json)).GetProperty("id").GetString()!);
        await planner.PostAsync($"/api/v1/planning/scenarios/{id}/run", null);

        // wait for the API's own (fallback) run to persist the request it assembled
        string? requestJson = null;
        for (var i = 0; i < 60 && requestJson is null; i++)
        {
            using var scope = fx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            requestJson = await db.PlanningScenarios.Where(s => s.Id == id).Select(s => s.RequestJson).FirstOrDefaultAsync();
            if (requestJson is null) await Task.Delay(250);
        }
        requestJson.Should().NotBeNull("the scenario run must persist its planning request for audit");

        var request = Json.Deserialize<PlanningRequest>(requestJson!)!;
        var client = new PlanningEngineClient(
            new HttpClient { BaseAddress = new Uri(EngineUrl.TrimEnd('/') + "/") },
            Options.Create(new PlanningEngineOptions { BaseUrl = EngineUrl, TimeoutMs = 3000 }),
            new PlanningEngineMetrics(), NullLogger<PlanningEngineClient>.Instance);

        var outcome = await client.SolveAsync(request, CancellationToken.None);

        outcome.UsedFallback.Should().BeFalse("the engine is reachable");
        outcome.Response.Status.Should().Be("FEASIBLE");
        outcome.Response.ElapsedMs.Should().BeLessThan(3000);
        outcome.Response.Orders.Single(o => o.OrderCode == "WO-2026-014").LatenessDays.Should().Be(4);
        outcome.Response.Kpi.DowntimeHours.Should().Be(8);
        outcome.Response.Explanations.Should().Contain(e =>
            e.ReasonCode == "ORDER_PULLED_FORWARD" && e.OrderCode == "WO-2026-019");
        outcome.Response.Explanations.Should().Contain(e => e.ReasonCode == "DOWNTIME_REDUCED");

        await fx.ResetAsync();
    }
}
