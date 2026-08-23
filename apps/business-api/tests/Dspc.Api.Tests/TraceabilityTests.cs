using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Dspc.Api.Tests;

/// <summary>Trace-back and trace-forward must describe the same links from opposite ends.</summary>
[Collection("api")]
public class TraceabilityTests(ApiFixture fx)
{
    private const string Serial = "PMV-2026-0007";
    private const string Lot = "HTS-22-2608";

    [Fact]
    public async Task Trace_back_from_serial_reaches_lots_certificates_and_supplier()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var trace = await c.GetFromJsonAsync<JsonElement>($"/api/v1/trace/serials/{Serial}", ApiFixture.Json);

        trace.GetProperty("orderCode").GetString().Should().Be("WO-2026-011");
        trace.GetProperty("productCode").GetString().Should().Be("P-MOB-03");
        trace.GetProperty("bomVersion").GetString().Should().NotBeNullOrWhiteSpace();

        var json = trace.ToString();
        json.Should().Contain(Lot);
        json.Should().Contain("SUP-01", "the genealogy must reach the supplier of the steel lot");

        var components = trace.GetProperty("components").EnumerateArray().ToList();
        components.Should().NotBeEmpty();
        components.Select(x => x.GetProperty("lotNumber").GetString()).Should().Contain(Lot);
        components.Where(x => x.GetProperty("lotNumber").GetString() == Lot)
            .Should().OnlyContain(x => x.GetProperty("certSha256").GetString()!.Length == 64);
    }

    [Fact]
    public async Task Genealogy_tree_is_rooted_at_the_serial_and_nests_order_operations_and_lots()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var trace = await c.GetFromJsonAsync<JsonElement>($"/api/v1/trace/serials/{Serial}", ApiFixture.Json);
        var root = trace.GetProperty("genealogy");

        root.GetProperty("kind").GetString().Should().Be("Serial");
        root.GetProperty("code").GetString().Should().Be(Serial);

        var kinds = new List<string>();
        Walk(root, kinds);
        kinds.Should().Contain(["Serial", "Order", "Operation", "Lot", "Document", "PurchaseOrder", "Supplier"]);

        static void Walk(JsonElement node, List<string> kinds)
        {
            kinds.Add(node.GetProperty("kind").GetString()!);
            foreach (var child in node.GetProperty("children").EnumerateArray()) Walk(child, kinds);
        }
    }

    [Fact]
    public async Task Trace_forward_and_trace_back_agree_on_the_same_links()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var forward = await c.GetFromJsonAsync<JsonElement>($"/api/v1/trace/lots/{Lot}/forward", ApiFixture.Json);
        var serials = forward.GetProperty("serials").EnumerateArray().Select(s => s.GetProperty("serial").GetString()).ToList();
        serials.Should().Contain([Serial, "PMV-2026-0008"]);
        forward.GetProperty("orders").EnumerateArray().Select(o => o.GetProperty("orderCode").GetString()).Should().Contain("WO-2026-011");

        // every serial the lot points to must point back at the lot
        foreach (var serial in serials)
        {
            var back = await c.GetFromJsonAsync<JsonElement>($"/api/v1/trace/serials/{serial}", ApiFixture.Json);
            back.GetProperty("components").EnumerateArray().Select(x => x.GetProperty("lotNumber").GetString())
                .Should().Contain(Lot, $"trace-back from {serial} must reach {Lot}");
        }
    }

    [Fact]
    public async Task Reserved_orders_appear_in_trace_forward_with_their_relation()
    {
        using var c = await fx.AsAsync("ProductionPlanner");

        var forward = await c.GetFromJsonAsync<JsonElement>($"/api/v1/trace/lots/{Lot}/forward", ApiFixture.Json);
        var orders = forward.GetProperty("orders").EnumerateArray()
            .ToDictionary(o => o.GetProperty("orderCode").GetString()!, o => o.GetProperty("relation").GetString());

        orders["WO-2026-011"].Should().Be("Consumed");
        orders.Should().ContainKey("WO-2026-018").WhoseValue.Should().Be("Reserved");
    }

    [Fact]
    public async Task Search_finds_serials_lots_heats_and_orders()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var bySerial = await c.GetFromJsonAsync<JsonElement>("/api/v1/trace/search?q=PMV-2026", ApiFixture.Json);
        bySerial.EnumerateArray().Select(h => h.GetProperty("kind").GetString()).Should().Contain("Serial");

        var byLot = await c.GetFromJsonAsync<JsonElement>("/api/v1/trace/search?q=HTS-22", ApiFixture.Json);
        byLot.EnumerateArray().Select(h => h.GetProperty("code").GetString()).Should().Contain(Lot);

        var byOrder = await c.GetFromJsonAsync<JsonElement>("/api/v1/trace/search?q=WO-2026-014", ApiFixture.Json);
        byOrder.EnumerateArray().Select(h => h.GetProperty("kind").GetString()).Should().Contain("Order");

        var tooShort = await c.GetFromJsonAsync<JsonElement>("/api/v1/trace/search?q=P", ApiFixture.Json);
        tooShort.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Audit_history_is_exportable_as_csv()
    {
        using var c = await fx.AsAsync("Auditor");

        var res = await c.GetAsync("/api/v1/trace/audit?format=csv");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        (await res.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Supplier_users_cannot_read_the_trace()
    {
        using var supplier = await fx.AsAsync("SupplierUser", "SUP-02");

        (await supplier.GetAsync($"/api/v1/trace/serials/{Serial}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await supplier.GetAsync($"/api/v1/trace/lots/{Lot}/forward")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unknown_serial_is_a_problem_details_404()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var res = await c.GetAsync("/api/v1/trace/serials/NO-SUCH-SERIAL");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await res.Content.ReadAsStringAsync()).Should().Contain("NO-SUCH-SERIAL");
    }
}
