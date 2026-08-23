using System.Net;
using FluentAssertions;
using Dspc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dspc.Api.Tests;

/// <summary>
/// A shipment whose lines were all reassigned or closed used to make GET /dashboard/map throw
/// ("Sequence contains no elements"), returning 500 on the Control Room for every user until the
/// demo was reset. The map must skip such a shipment instead of failing.
/// </summary>
[Collection("api")]
public class DashboardMapTests(ApiFixture fx)
{
    [Fact]
    public async Task Map_survives_a_shipment_that_lost_all_its_lines()
    {
        using var client = await fx.AsAsync("ProductionPlanner");

        using (var scope = fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shipment = await db.Shipments.Include(s => s.Lines).FirstAsync(s => s.Lines.Count > 0);
            foreach (var line in shipment.Lines.ToList()) line.ShipmentId = null;
            await db.SaveChangesAsync();
        }

        var res = await client.GetAsync("/api/v1/dashboard/map");
        res.StatusCode.Should().Be(HttpStatusCode.OK, "the map must degrade rather than fail");

        await fx.ResetAsync();
    }
}
