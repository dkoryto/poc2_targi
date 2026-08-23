using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Dspc.Api.Tests;

/// <summary>Passport completeness gate, versioned PDF generation and the invalidation triggered by a lot block.</summary>
[Collection("api")]
public class PassportTests(ApiFixture fx) : IAsyncLifetime
{
    private const string CompleteSerial = "PMV-2026-0007";
    private const string IncompleteSerial = "SCM-2026-0103";
    private const string Lot = "HTS-22-2608";

    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seeded_passport_is_complete_and_already_has_a_generated_pdf()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var passport = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{CompleteSerial}", ApiFixture.Json);

        passport.GetProperty("completeness").GetProperty("complete").GetBoolean().Should().BeTrue();
        passport.GetProperty("completeness").GetProperty("missing").GetArrayLength().Should().Be(0);
        passport.GetProperty("templateCode").GetString().Should().Be("DQP-01");
        passport.GetProperty("status").GetString().Should().Be("Generated", "the seed post-processor renders the two issued passports");
        passport.GetProperty("versions").GetArrayLength().Should().BeGreaterThan(0);
        passport.GetProperty("components").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Complete_passport_generates_a_new_versioned_pdf_with_sha256_and_qr()
    {
        using var c = await fx.AsAsync("QualityInspector");
        var before = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{CompleteSerial}", ApiFixture.Json);
        var previousVersions = before.GetProperty("versions").GetArrayLength();

        var res = await c.PostAsync($"/api/v1/passports/{CompleteSerial}/generate", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var generated = await res.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);

        var version = generated.GetProperty("version").GetInt32();
        generated.GetProperty("sha256").GetString().Should().MatchRegex("^[0-9a-f]{64}$");
        generated.GetProperty("fileSize").GetInt64().Should().BeGreaterThan(1000);

        var pdf = await c.GetAsync($"/api/v1/passports/{CompleteSerial}/versions/{version}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var qr = await c.GetAsync($"/api/v1/passports/{CompleteSerial}/qr");
        qr.StatusCode.Should().Be(HttpStatusCode.OK);
        (await qr.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal([0x89, 0x50, 0x4E, 0x47]);

        // previous versions are kept, the new one is current
        var after = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{CompleteSerial}", ApiFixture.Json);
        after.GetProperty("versions").GetArrayLength().Should().Be(previousVersions + 1);
        var versions = after.GetProperty("versions").EnumerateArray().ToList();
        versions.Count(v => v.GetProperty("status").GetString() == "Current").Should().Be(1);
        versions.Should().Contain(v => v.GetProperty("status").GetString() == "Superseded");
    }

    [Fact]
    public async Task Incomplete_passport_is_refused_with_the_missing_list()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var res = await c.PostAsync($"/api/v1/passports/{IncompleteSerial}/generate", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await res.Content.ReadAsStringAsync();
        problem.Should().Contain("missing");
        problem.Should().Contain("APPROVAL");

        var passport = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{IncompleteSerial}", ApiFixture.Json);
        passport.GetProperty("completeness").GetProperty("complete").GetBoolean().Should().BeFalse();
        var missing = passport.GetProperty("completeness").GetProperty("missing").EnumerateArray()
            .Select(m => m.GetProperty("code").GetString()).ToList();
        missing.Should().NotBeEmpty();
        missing.Should().OnlyContain(code => code!.Length > 0);
        passport.GetProperty("completeness").GetProperty("missing").EnumerateArray()
            .Should().OnlyContain(m => m.GetProperty("labelKey").GetString()!.StartsWith("passports.missing."));
    }

    [Fact]
    public async Task Approving_an_incomplete_passport_is_refused()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var res = await c.PostAsync($"/api/v1/passports/{IncompleteSerial}/approve", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Blocking_the_steel_lot_invalidates_every_passport_built_from_it()
    {
        using var quality = await fx.AsAsync("QualityInspector");

        var res = await quality.PostAsJsonAsync($"/api/v1/lots/{Lot}/block",
            new { reason = "Odchyłka składu chemicznego", ncrTitle = "NCR — partia HTS-22-2608" }, ApiFixture.Json);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);

        var affected = body.GetProperty("affected");
        affected.GetProperty("orders").EnumerateArray().Select(o => o.GetString()).Should().Contain(["WO-2026-011", "WO-2026-018"]);
        affected.GetProperty("serials").EnumerateArray().Select(s => s.GetString()).Should().Contain([CompleteSerial, "PMV-2026-0008"]);
        affected.GetProperty("passports").EnumerateArray().Select(p => p.GetString()).Should().Contain([CompleteSerial, "PMV-2026-0008"]);
        body.GetProperty("lot").GetProperty("status").GetString().Should().Be("Blocked");
        body.GetProperty("nonConformance").GetProperty("code").GetString().Should().StartWith("NCR-");

        // immediately visible — invalidation happens inside the blocking transaction
        foreach (var serial in new[] { CompleteSerial, "PMV-2026-0008" })
        {
            var passport = await quality.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{serial}", ApiFixture.Json);
            passport.GetProperty("status").GetString().Should().Be("Invalidated");
            passport.GetProperty("invalidationReason").GetString().Should().Contain(Lot);
            passport.GetProperty("versions").EnumerateArray().Should().NotBeEmpty("previous PDF versions are retained");
        }

        // the blocked lot no longer counts as available material and the passport can no longer be generated
        var regenerate = await quality.PostAsync($"/api/v1/passports/{CompleteSerial}/generate", null);
        regenerate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var lot = await quality.GetFromJsonAsync<JsonElement>($"/api/v1/lots/{Lot}", ApiFixture.Json);
        lot.GetProperty("status").GetString().Should().Be("Blocked");
        lot.GetProperty("nonConformances").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Blocking_twice_is_a_conflict()
    {
        using var quality = await fx.AsAsync("QualityInspector");
        var body = new { reason = "duplikat", ncrTitle = "NCR duplikat" };

        (await quality.PostAsJsonAsync($"/api/v1/lots/CON-5-1142/block", body, ApiFixture.Json)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "CON-5-1142 is already blocked in the seed");
    }

    [Fact]
    public async Task Failed_inspection_blocks_the_lot_and_raises_a_non_conformance()
    {
        using var quality = await fx.AsAsync("QualityInspector");

        var res = await quality.PostAsJsonAsync("/api/v1/lots/ARM-2-0077/inspections",
            new { result = "Failed", notes = "Pęknięcie powłoki", inspectedAt = DateTime.UtcNow }, ApiFixture.Json);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var lot = await quality.GetFromJsonAsync<JsonElement>("/api/v1/lots/ARM-2-0077", ApiFixture.Json);
        lot.GetProperty("status").GetString().Should().Be("Blocked");
        lot.GetProperty("nonConformances").GetArrayLength().Should().BeGreaterThan(0);

        var ncrs = await quality.GetFromJsonAsync<JsonElement>("/api/v1/non-conformances", ApiFixture.Json);
        ncrs.GetProperty("items").EnumerateArray().Select(n => n.GetProperty("lotNumber").GetString()).Should().Contain("ARM-2-0077");
    }

    [Fact]
    public async Task Only_quality_roles_may_block_or_approve()
    {
        using var planner = await fx.AsAsync("ProductionPlanner");
        using var auditor = await fx.AsAsync("Auditor");
        var body = new { reason = "x", ncrTitle = "y" };

        (await planner.PostAsJsonAsync($"/api/v1/lots/{Lot}/block", body, ApiFixture.Json)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await auditor.PostAsync($"/api/v1/passports/{CompleteSerial}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await auditor.PostAsync($"/api/v1/passports/{CompleteSerial}/generate", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await auditor.GetAsync($"/api/v1/passports/{CompleteSerial}")).StatusCode.Should().Be(HttpStatusCode.OK, "auditors read everything");
    }

    [Fact]
    public async Task Supplier_users_cannot_read_passports_or_other_suppliers_lots()
    {
        using var supplier = await fx.AsAsync("SupplierUser", "SUP-02");

        (await supplier.GetAsync($"/api/v1/passports/{CompleteSerial}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await supplier.GetAsync($"/api/v1/lots/{Lot}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "HTS-22-2608 belongs to SUP-01");

        var own = await supplier.GetFromJsonAsync<JsonElement>("/api/v1/lots", ApiFixture.Json);
        own.GetProperty("items").EnumerateArray().Should().OnlyContain(l => l.GetProperty("supplierCode").GetString() == "SUP-02");
    }

    [Fact]
    public async Task Demo_reset_restores_the_generated_passports_within_the_budget()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await fx.ResetAsync();
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(10_000, "the stand must be able to reset between visitors");

        using var c = await fx.AsAsync("QualityInspector");
        var passport = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{CompleteSerial}", ApiFixture.Json);
        passport.GetProperty("status").GetString().Should().Be("Generated");
        passport.GetProperty("versions").GetArrayLength().Should().Be(1, "reset rebuilds exactly one seeded version");
    }

    /// <summary>
    /// Every plant must tell the same passport story. This is the regression for the defect where the passports seeded
    /// for Piła, Zamość and Leszno were marked Generated but had no version, no PDF and no checksum, so their detail
    /// screen was empty while the list showed status "Wygenerowany" next to a blank version column.
    /// </summary>
    [Theory]
    [InlineData("SITE-01")]
    [InlineData("SITE-02")]
    [InlineData("SITE-03")]
    [InlineData("SITE-04")]
    public async Task Every_generated_passport_has_a_downloadable_pdf_on_every_plant(string siteCode)
    {
        using var c = await fx.AsAsync("QualityInspector");

        var list = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports?siteCode={siteCode}", ApiFixture.Json);
        var items = list.TryGetProperty("items", out var arr) ? arr : list;
        items.GetArrayLength().Should().BeGreaterThan(0, "each plant seeds passports");

        var generatedSeen = 0;
        foreach (var row in items.EnumerateArray())
        {
            var serial = row.GetProperty("serial").GetString()!;
            var detail = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{serial}", ApiFixture.Json);
            if (detail.GetProperty("status").GetString() != "Generated") continue;

            generatedSeen++;
            var versions = detail.GetProperty("versions");
            versions.GetArrayLength().Should().BeGreaterThan(0,
                $"{serial} on {siteCode} is Generated, which asserts a rendered document exists");

            var current = versions.EnumerateArray().First();
            var sha = current.GetProperty("sha256").GetString();
            sha.Should().MatchRegex("^[0-9a-f]{64}$", $"{serial} must carry a checksum");

            var version = current.GetProperty("version").GetInt32();
            var pdf = await c.GetAsync($"/api/v1/passports/{serial}/versions/{version}/pdf");
            pdf.StatusCode.Should().Be(HttpStatusCode.OK);
            var bytes = await pdf.Content.ReadAsByteArrayAsync();
            bytes.Length.Should().BeGreaterThan(1000, $"{serial} must be a real document");
            Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");

            var qr = await c.GetAsync($"/api/v1/passports/{serial}/qr");
            qr.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        generatedSeen.Should().BeGreaterThan(0, $"{siteCode} seeds at least one issued passport");
    }

    /// <summary>Each plant's second passport is the "incomplete" half of the story and must name concrete gaps.</summary>
    [Theory]
    [InlineData("SITE-01")]
    [InlineData("SITE-02")]
    [InlineData("SITE-03")]
    [InlineData("SITE-04")]
    public async Task Every_plant_seeds_an_incomplete_passport_with_named_gaps(string siteCode)
    {
        using var c = await fx.AsAsync("QualityInspector");

        var list = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports?siteCode={siteCode}", ApiFixture.Json);
        var items = list.TryGetProperty("items", out var arr) ? arr : list;

        var incomplete = new List<string>();
        foreach (var row in items.EnumerateArray())
        {
            var serial = row.GetProperty("serial").GetString()!;
            var detail = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{serial}", ApiFixture.Json);
            var completeness = detail.GetProperty("completeness");
            if (completeness.GetProperty("complete").GetBoolean()) continue;

            incomplete.Add(serial);
            completeness.GetProperty("missing").GetArrayLength().Should().BeGreaterThan(0,
                $"{serial} is incomplete, so the screen must list what is missing");
            foreach (var m in completeness.GetProperty("missing").EnumerateArray())
                m.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
        }

        incomplete.Should().NotBeEmpty($"{siteCode} needs a passport with gaps to demonstrate the completeness rules");
    }

    /// <summary>
    /// A passport must name its own plant. The QR printed on the document links to /passports/{serial}, so the reader
    /// commonly arrives with a different plant selected — or none. Regression for the defect where a Zamość passport
    /// rendered under a Leszno heading with the word "Zamość" nowhere on screen.
    /// </summary>
    [Theory]
    [InlineData("SITE-01")]
    [InlineData("SITE-02")]
    [InlineData("SITE-03")]
    [InlineData("SITE-04")]
    public async Task Passport_names_its_own_plant_in_list_and_detail(string siteCode)
    {
        using var c = await fx.AsAsync("QualityInspector");

        var sites = await c.GetFromJsonAsync<JsonElement>("/api/v1/sites", ApiFixture.Json);
        var siteArr = sites.ValueKind == JsonValueKind.Array ? sites : sites.GetProperty("items");
        var expectedName = siteArr.EnumerateArray().First(x => x.GetProperty("code").GetString() == siteCode)
            .GetProperty("name").GetString();

        var list = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports?siteCode={siteCode}", ApiFixture.Json);
        var items = list.TryGetProperty("items", out var arr) ? arr : list;
        items.GetArrayLength().Should().BeGreaterThan(0);

        foreach (var row in items.EnumerateArray())
        {
            row.GetProperty("siteCode").GetString().Should().Be(siteCode, "the list row must name its plant");
            row.GetProperty("siteName").GetString().Should().Be(expectedName);

            var serial = row.GetProperty("serial").GetString()!;
            var detail = await c.GetFromJsonAsync<JsonElement>($"/api/v1/passports/{serial}", ApiFixture.Json);
            detail.GetProperty("siteCode").GetString().Should().Be(siteCode, $"{serial} must name its plant on the detail route");
            detail.GetProperty("siteName").GetString().Should().Be(expectedName);

            // The PDF header resolves the plant through the same helper as these fields (PassportService.SiteAsync),
            // so document and screen cannot name different plants. The rendered text itself is glyph-encoded inside
            // compressed streams and is not assertable byte-wise here; this checks the document exists and is a PDF.
            if (detail.GetProperty("status").GetString() != "Generated") continue;
            var version = detail.GetProperty("versions").EnumerateArray().First().GetProperty("version").GetInt32();
            var pdf = await c.GetAsync($"/api/v1/passports/{serial}/versions/{version}/pdf");
            pdf.StatusCode.Should().Be(HttpStatusCode.OK);
            var bytes = await pdf.Content.ReadAsByteArrayAsync();
            Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        }
    }

    /// <summary>The serial, its lot and the scenario that planned it must all name their plant for the same reason.</summary>
    [Fact]
    public async Task Trace_and_lot_records_name_their_plant()
    {
        using var c = await fx.AsAsync("QualityInspector");

        var serialTrace = await c.GetFromJsonAsync<JsonElement>("/api/v1/trace/serials/PMV-2026-0201-Z", ApiFixture.Json);
        serialTrace.GetProperty("siteCode").GetString().Should().Be("SITE-03");
        serialTrace.GetProperty("siteName").GetString().Should().Be("Zakład Zamość");

        var lot = await c.GetFromJsonAsync<JsonElement>("/api/v1/lots/HTS-22-3110", ApiFixture.Json);
        lot.GetProperty("siteCode").GetString().Should().Be("SITE-03");
        lot.GetProperty("siteName").GetString().Should().Be("Zakład Zamość");
    }
}
