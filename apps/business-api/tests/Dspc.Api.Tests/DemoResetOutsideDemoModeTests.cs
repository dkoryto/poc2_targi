using System.Net;
using FluentAssertions;

namespace Dspc.Api.Tests;

/// <summary>
/// A deployment that runs with Demo:Enabled=false still needs a way to restore the data — the
/// production instance resets nightly from a scheduled job. The reset must therefore work for the
/// DemoControl roles regardless of the profile, while the rest of the demo surface stays closed.
/// </summary>
[Collection("api")]
public class DemoResetOutsideDemoModeTests(ApiFixture fx)
{
    [Fact]
    public async Task Administrator_can_reset_and_anonymous_cannot()
    {
        using var admin = await fx.AsAsync("Administrator");
        (await admin.PostAsync("/api/v1/demo/reset", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var anon = fx.Anonymous();
        (await anon.PostAsync("/api/v1/demo/reset", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var auditor = await fx.AsAsync("Auditor");
        (await auditor.PostAsync("/api/v1/demo/reset", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
