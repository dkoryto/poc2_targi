using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;

namespace Dspc.Api.Tests;

/// <summary>
/// An incomplete or malformed upload form must be answered with a validation error naming the
/// field. It used to return 500: the "references a line or a lot" rule is an object-level
/// FluentValidation rule, whose empty property name crashed the camelCase conversion. A supplier
/// then saw "an unexpected error occurred" and the failure was recorded as a server fault.
/// </summary>
[Collection("api")]
public class DocumentUploadTests(ApiFixture fx)
{
    private static MultipartFormDataContent Form(string? poLineId, string documentNumber = "QA-UP-1", string issuedOn = "2026-08-01", string type = "MATERIAL_CERT")
    {
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent
        {
            { file, "file", "a.pdf" },
            { new StringContent(type), "type" },
            { new StringContent(documentNumber), "documentNumber" },
            { new StringContent(issuedOn), "issuedOn" },
        };
        if (poLineId is not null) form.Add(new StringContent(poLineId), "poLineId");
        return form;
    }

    [Theory]
    [InlineData(null)]          // field absent
    [InlineData("")]            // field present but blank
    [InlineData("not-a-guid")]  // field present but unparseable
    public async Task An_upload_without_a_usable_line_reference_is_a_validation_error(string? poLineId)
    {
        using var client = await fx.AsAsync("SupplierUser", "SUP-02");

        var res = await client.PostAsync("/api/v1/documents", Form(poLineId));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("errors", "the caller must be told which field is wrong");
    }

    [Fact]
    public async Task A_malformed_line_reference_is_reported_against_its_own_field()
    {
        using var client = await fx.AsAsync("SupplierUser", "SUP-02");

        var res = await client.PostAsync("/api/v1/documents", Form("not-a-guid"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("poLineId");
    }

    [Fact]
    public async Task A_malformed_issue_date_is_reported_against_its_own_field()
    {
        using var client = await fx.AsAsync("SupplierUser", "SUP-02");

        var res = await client.PostAsync("/api/v1/documents", Form("00000000-0000-0000-0000-000000000001", issuedOn: "31-31-2026"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("issuedOn");
    }

    [Fact]
    public async Task A_well_formed_reference_to_an_unknown_line_is_not_found()
    {
        using var client = await fx.AsAsync("SupplierUser", "SUP-02");

        var res = await client.PostAsync("/api/v1/documents", Form("00000000-0000-0000-0000-000000000001"));

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
