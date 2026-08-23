using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Dspc.Api.Tests;

[Collection("api")]
public class AuthorizationTests(ApiFixture fx)
{
    [Fact]
    public async Task Anonymous_is_401_on_protected_endpoints()
    {
        using var c = fx.Anonymous();
        (await c.GetAsync("/api/v1/dashboard/kpis")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await c.GetAsync("/api/v1/purchase-orders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await c.PostAsync("/api/v1/demo/reset", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Supplier_cannot_read_dashboard_or_audit()
    {
        using var c = await fx.AsAsync("SupplierUser", "SUP-02");
        (await c.GetAsync("/api/v1/dashboard/kpis")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.GetAsync("/api/v1/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.GetAsync("/api/v1/admin/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.GetAsync("/api/v1/planning/baseline")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Director_cannot_reset_demo_or_patch_lines()
    {
        using var c = await fx.AsAsync("OperationsDirector");
        (await c.PostAsync("/api/v1/demo/reset", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var po = await c.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders/PO-2026-0007", ApiFixture.Json);
        var lineId = po.GetProperty("lines")[0].GetProperty("id").GetString();
        (await c.PatchAsJsonAsync($"/api/v1/purchase-orders/PO-2026-0007/lines/{lineId}", new { progressPercent = 50 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Auditor_is_read_only()
    {
        using var c = await fx.AsAsync("Auditor");
        (await c.GetAsync("/api/v1/audit")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await c.PostAsJsonAsync("/api/v1/logistics-events", new { type = "WEATHER", severity = "LOW", description = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.PostAsJsonAsync("/api/v1/shipments", new { poCode = "PO-2026-0007", lineIds = Array.Empty<Guid>(), carrier = "x", plannedDeparture = DateTime.UtcNow, eta = "2026-10-01" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Demo_endpoints_work_for_presenter_and_return_problem_details_on_bad_input()
    {
        using var c = await fx.AsAsync("DemoPresenter");
        (await c.GetAsync("/api/v1/demo/script")).StatusCode.Should().Be(HttpStatusCode.OK);
        var bad = await c.PostAsJsonAsync("/api/v1/auth/login", new { username = "", password = "" });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var pd = await bad.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        pd.GetProperty("errors").TryGetProperty("username", out _).Should().BeTrue();
        pd.TryGetProperty("correlationId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        using var c = fx.Anonymous();
        var res = await c.PostAsJsonAsync("/api/v1/auth/login", new { username = "planner", password = "wrong" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var ok = await c.PostAsJsonAsync("/api/v1/auth/login", new { username = "planner", password = "demo" });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection("api")]
public class SupplierIsolationTests(ApiFixture fx)
{
    [Fact]
    public async Task Supplier_sees_only_own_purchase_orders_shipments_and_documents()
    {
        using var hydromech = await fx.AsAsync("SupplierUser", "SUP-02");
        var pos = await hydromech.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders", ApiFixture.Json);
        pos.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("supplierCode").GetString()).Should().OnlyContain(s => s == "SUP-02").And.NotBeEmpty();
        var shipments = await hydromech.GetFromJsonAsync<JsonElement>("/api/v1/shipments", ApiFixture.Json);
        shipments.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("supplierCode").GetString()).Should().OnlyContain(s => s == "SUP-02");
        var docs = await hydromech.GetFromJsonAsync<JsonElement>("/api/v1/documents", ApiFixture.Json);
        var numbers = docs.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("documentNumber").GetString()!).ToList();
        // Kielce: 5 line documents (PO-2026-0007/0012) + 4 lot documents (ACT-40-0388/0371);
        // SUP-02 also supplies Zamość (PO-2026-2005), which adds its line and lot certificate.
        numbers.Should().HaveCount(11);
        numbers.Should().Contain(["MC-HYD-2026-3114", "MC-HYD-2026-3114L"]);
        numbers.Should().NotContain(n => n.Contains("VIS") || n.Contains("NOR") || n.Contains("BAL") || n.Contains("CAR") || n.Contains("RHC") || n.Contains("SIL") || n.Contains("IBE"));
        var suppliers = await hydromech.GetFromJsonAsync<JsonElement>("/api/v1/suppliers", ApiFixture.Json);
        suppliers.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Supplier_cannot_access_another_suppliers_order_by_direct_call()
    {
        using var nordstal = await fx.AsAsync("SupplierUser", "SUP-01");
        (await nordstal.GetAsync("/api/v1/purchase-orders/PO-2026-0007")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await nordstal.GetAsync("/api/v1/shipments/SHP-2026-0031")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var hydromech = await fx.AsAsync("SupplierUser", "SUP-02");
        var po = await hydromech.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders/PO-2026-0007", ApiFixture.Json);
        var lineId = po.GetProperty("lines")[0].GetProperty("id").GetString();
        var res = await nordstal.PostAsJsonAsync($"/api/v1/purchase-orders/PO-2026-0007/lines/{lineId}/eta", new { eta = "2026-10-01", reason = "LOGISTICS" });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Supplier_impact_view_is_masked_to_codes_and_counts()
    {
        using var hydromech = await fx.AsAsync("SupplierUser", "SUP-02");
        var po = await hydromech.GetFromJsonAsync<JsonElement>("/api/v1/purchase-orders/PO-2026-0007", ApiFixture.Json);
        var lineId = po.GetProperty("lines")[0].GetProperty("id").GetString();
        var impact = await hydromech.GetFromJsonAsync<JsonElement>($"/api/v1/purchase-orders/PO-2026-0007/lines/{lineId}/impact", ApiFixture.Json);
        impact.GetProperty("restricted").GetBoolean().Should().BeTrue();
        foreach (var e in impact.GetProperty("endangeredOrders").EnumerateArray())
        {
            e.GetProperty("productCode").GetString().Should().BeEmpty();
            e.GetProperty("priority").GetInt32().Should().Be(0);
        }
    }
}
