using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dspc.Domain.Entities;
using Dspc.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dspc.Api.Tests;

[Collection("api")]
public class EtaChangeRaisesRiskTests(ApiFixture fx)
{
    [Fact]
    public async Task Act40_eta_plus_10_days_raises_risk_to_critical_and_endangers_WO014()
    {
        await fx.ResetAsync();
        using var supplier = await fx.AsAsync("SupplierUser", "SUP-02");
        var po = await supplier.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders/PO-2026-0007", ApiFixture.Json);
        var line = po.GetProperty("lines")[0];
        line.GetProperty("partCode").GetString().Should().Be("ACT-40");
        line.GetProperty("risk").GetProperty("score").GetInt32().Should().Be(44);
        line.GetProperty("risk").GetProperty("category").GetString().Should().Be("Medium");
        var lineId = line.GetProperty("id").GetString();
        var eta = DateOnly.Parse(line.GetProperty("eta").GetString()!);
        eta.Should().Be(new DateOnly(2026, 9, 15)); // T0+8 with ClockAnchor 2026-09-07

        var res = await supplier.PostAsJsonAsync($"/api/v1/purchase-orders/PO-2026-0007/lines/{lineId}/eta", new { eta = eta.AddDays(10).ToString("yyyy-MM-dd"), reason = "PRODUCTION_DELAY", comment = "test" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Headers.ETag.Should().NotBeNull();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        body.GetProperty("risk").GetProperty("score").GetInt32().Should().Be(79);
        body.GetProperty("risk").GetProperty("category").GetString().Should().Be("Critical");
        var endangered = body.GetProperty("endangeredOrders").EnumerateArray().ToList();
        endangered.Should().ContainSingle(e => e.GetProperty("orderCode").GetString() == "WO-2026-014");
        endangered[0].GetProperty("shortage").GetDecimal().Should().Be(8);
        endangered[0].GetProperty("latenessDays").GetInt32().Should().Be(4);
        body.GetProperty("predictedDowntimeHours").GetDouble().Should().Be(36);
        body.GetProperty("risk").GetProperty("factors").EnumerateArray().First().GetProperty("code").GetString().Should().Be("ETA_DEVIATION");

        using var scope = fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.Parse(lineId!);
        var assessments = await db.RiskAssessments.Where(r => r.PurchaseOrderLineId == id).OrderByDescending(r => r.AssessedAt).ToListAsync();
        assessments.First().Trigger.Should().Be("EtaChanged");
        assessments.First().Score.Should().Be(79);
        assessments.First().PreviousScore.Should().Be(44);

        var outbox = (await db.OutboxMessages.Where(m => m.EventName == "ShipmentEtaChanged" || m.EventName == "DeliveryRiskChanged").ToListAsync())
            .Where(m => m.PayloadJson.Contains(lineId!, StringComparison.OrdinalIgnoreCase)).Select(m => m.EventName).ToList();
        outbox.Should().Contain("ShipmentEtaChanged").And.Contain("DeliveryRiskChanged");

        var notifications = await db.Notifications.Where(n => n.TargetRole == Domain.Common.Role.ProductionPlanner && n.TitleKey == "notifications.deliveryRisk.title").ToListAsync();
        notifications.Should().Contain(n => n.ParamsJson.Contains("PO-2026-0007"));

        var audit = await db.AuditEvents.Where(a => a.Action == "PurchaseOrderLine.EtaChange" && a.EntityCode == "PO-2026-0007/1").OrderByDescending(a => a.Id).FirstAsync();
        audit.UserName.Should().Be("supplier.hydromech");
        audit.BeforeJson.Should().Contain("2026-09-15");
        audit.AfterJson.Should().Contain("2026-09-25");
        audit.CorrelationId.Should().NotBeNullOrEmpty();

        // dashboard reflects it
        using var director = await fx.AsAsync("OperationsDirector");
        var kpis = await director.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/kpis", ApiFixture.Json);
        Kpi(kpis, "HIGH_RISK_DELIVERIES").Should().Be(4);
        Kpi(kpis, "PREDICTED_DOWNTIME_H").Should().Be(36);
        var plan = await director.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/plan", ApiFixture.Json);
        plan.GetProperty("orders").EnumerateArray().Single(o => o.GetProperty("code").GetString() == "WO-2026-014").GetProperty("riskFlag").GetString().Should().Be("critical");

        await fx.ResetAsync();
    }

    [Fact]
    public async Task Stale_if_match_is_rejected_with_412_and_idempotent_retry_is_replayed()
    {
        await fx.ResetAsync();
        using var supplier = await fx.AsAsync("SupplierUser", "SUP-02");
        var po = await supplier.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders/PO-2026-0007", ApiFixture.Json);
        var lineId = po.GetProperty("lines")[0].GetProperty("id").GetString();
        var stale = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/purchase-orders/PO-2026-0007/lines/{lineId}") { Content = JsonContent.Create(new { progressPercent = 90 }) };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        (await supplier.SendAsync(stale)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var key = Guid.NewGuid().ToString();
        HttpRequestMessage Req() { var r = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/purchase-orders/PO-2026-0007/lines/{lineId}") { Content = JsonContent.Create(new { progressPercent = 90, comment = "idem" }) }; r.Headers.Add("Idempotency-Key", key); return r; }
        var first = await supplier.SendAsync(Req());
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await supplier.SendAsync(Req());
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Contains("Idempotent-Replayed").Should().BeTrue();
        await fx.ResetAsync();
    }

    internal static double Kpi(JsonElement kpis, string code) => kpis.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("code").GetString() == code).GetProperty("value").GetDouble();
}

[Collection("api")]
public class DashboardKpiTests(ApiFixture fx)
{
    [Fact]
    public async Task Seeded_kpis_match_the_demo_scenario()
    {
        await fx.ResetAsync();
        using var director = await fx.AsAsync("OperationsDirector");
        var kpis = await director.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/kpis", ApiFixture.Json);
        EtaChangeRaisesRiskTests.Kpi(kpis, "HIGH_RISK_DELIVERIES").Should().Be(3);
        EtaChangeRaisesRiskTests.Kpi(kpis, "PREDICTED_DOWNTIME_H").Should().Be(0);
        EtaChangeRaisesRiskTests.Kpi(kpis, "MATERIAL_READINESS").Should().Be(100);
        EtaChangeRaisesRiskTests.Kpi(kpis, "ORDER_ON_TIME").Should().Be(100);
        EtaChangeRaisesRiskTests.Kpi(kpis, "OTIF").Should().BeInRange(80, 92);
        EtaChangeRaisesRiskTests.Kpi(kpis, "PASSPORT_COMPLETENESS").Should().Be(50);
        kpis.GetProperty("items").GetArrayLength().Should().Be(6);

        var map = await director.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/map", ApiFixture.Json);
        map.GetProperty("suppliers").GetArrayLength().Should().Be(8);
        map.GetProperty("shipments").GetArrayLength().Should().Be(12);
        map.GetProperty("shipments").EnumerateArray().Should().Contain(s => s.GetProperty("code").GetString() == "SHP-2026-0031" && s.GetProperty("partCode").GetString() == "ACT-40");

        var heat = await director.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/risk-heatmap", ApiFixture.Json);
        heat.GetProperty("cols").GetArrayLength().Should().Be(5);
        heat.GetProperty("rows").EnumerateArray().Select(r => r.GetString()).Should().Contain("PL").And.Contain("DE");

        var quality = await director.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/quality-status", ApiFixture.Json);
        quality.GetProperty("lotsBlocked").GetInt32().Should().Be(1);
        // the two issued passports (PMV-2026-0007/0008) are rendered to PDF by the seed post-processor, so they are
        // seeded as Generated rather than Approved; the "ready for acceptance" aggregate counts both statuses
        quality.GetProperty("passports").GetProperty("generated").GetInt32().Should().Be(2);
        quality.GetProperty("readyForAcceptance").GetInt32().Should().Be(2);
        quality.GetProperty("documents").GetProperty("rejected").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Seeded_baseline_equals_the_engine_fixture()
    {
        await fx.ResetAsync();
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiFixture.RepoRoot(), "packages", "contracts", "examples", "baseline.json"))).RootElement;
        using var planner = await fx.AsAsync("ProductionPlanner");
        var baseline = await planner.GetFromJsonAsync<JsonElement>("/api/v1/planning/baseline", ApiFixture.Json);
        baseline.GetProperty("kpi").GetProperty("movedOperations").GetInt32().Should().Be(0);
        baseline.GetProperty("kpi").GetProperty("downtimeHours").GetDouble().Should().Be(0);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        var ops = baseline.GetProperty("gantt").GetProperty("operations").EnumerateArray().ToDictionary(o => o.GetProperty("code").GetString()!, o => o);
        var checkedOps = 0;
        foreach (var order in fixture.GetProperty("orders").EnumerateArray())
            foreach (var op in order.GetProperty("operations").EnumerateArray())
            {
                var code = op.GetProperty("code").GetString()!;
                ops.Should().ContainKey(code);
                var start = TimeZoneInfo.ConvertTimeFromUtc(ops[code].GetProperty("start").GetDateTime().ToUniversalTime(), tz);
                var end = TimeZoneInfo.ConvertTimeFromUtc(ops[code].GetProperty("end").GetDateTime().ToUniversalTime(), tz);
                start.Should().Be(DateTime.Parse(op.GetProperty("baselineStart").GetString()!), code);
                end.Should().Be(DateTime.Parse(op.GetProperty("baselineEnd").GetString()!), code);
                ops[code].GetProperty("workCenterCode").GetString().Should().Be(op.GetProperty("workCenterCode").GetString());
                checkedOps++;
            }
        checkedOps.Should().Be(29);
    }
}

[Collection("api")]
public class DemoResetTests(ApiFixture fx)
{
    [Fact]
    public async Task Reset_restores_identical_state_in_under_10_seconds()
    {
        using var presenter = await fx.AsAsync("DemoPresenter");
        using var director = await fx.AsAsync("OperationsDirector");

        async Task<string> Snapshot()
        {
            var kpis = await director.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/kpis", ApiFixture.Json);
            var pos = await director.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders", ApiFixture.Json);
            var plan = await director.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/plan", ApiFixture.Json);
            return JsonSerializer.Serialize(kpis.GetProperty("items")) + JsonSerializer.Serialize(pos) + JsonSerializer.Serialize(plan);
        }

        var sw = Stopwatch.StartNew();
        var r1 = await presenter.PostAsync("/api/v1/demo/reset", null);
        sw.Stop();
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        var body = await r1.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        body.GetProperty("durationMs").GetInt64().Should().BeLessThan(10_000);
        // 18 at Kielce plus the three demo plants; the golden path itself is asserted against SITE-01 below
        body.GetProperty("counts").GetProperty("purchaseOrders").GetInt32().Should().Be(42);
        var kielcePos = await director.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders?siteCode=SITE-01", ApiFixture.Json);
        kielcePos.GetProperty("total").GetInt32().Should().Be(18);
        var s1 = await Snapshot();

        // mutate, then reset again
        using var supplier = await fx.AsAsync("SupplierUser", "SUP-02");
        var po = await supplier.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders/PO-2026-0007", ApiFixture.Json);
        var lineId = po.GetProperty("lines")[0].GetProperty("id").GetString();
        (await supplier.PostAsJsonAsync($"/api/v1/purchase-orders/PO-2026-0007/lines/{lineId}/eta", new { eta = "2026-09-25", reason = "LOGISTICS" })).EnsureSuccessStatusCode();
        (await Snapshot()).Should().NotBe(s1);

        (await presenter.PostAsync("/api/v1/demo/reset", null)).EnsureSuccessStatusCode();
        (await Snapshot()).Should().Be(s1);

        var status = await director.GetFromJsonAsync<JsonElement>("/api/v1/demo/status", ApiFixture.Json);
        status.GetProperty("demoMode").GetBoolean().Should().BeTrue();
        status.GetProperty("seedVersion").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Audit_table_is_append_only()
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var act = async () => await db.Database.ExecuteSqlRawAsync("DELETE FROM audit_events WHERE id = (SELECT MIN(id) FROM audit_events)");
        await act.Should().ThrowAsync<Exception>().Where(e => e.Message.Contains("append-only"));
    }
}
