using System.Net.Http.Json;
using Dspc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dspc.Api.Tests;

/// <summary>
/// Upgrading a database written by the single-plant build must work: `docker compose up --build` over an existing
/// volume is the normal way the stand is restarted. This drives the real path — apply InitialCreate, populate it the
/// way the old build would have, then migrate to latest and boot the API against it.
/// Part of the "api" collection so it never races the shared fixture over process-wide environment variables.
/// </summary>
[Collection("api")]
public sealed class MultiSiteUpgradeTests : IAsyncLifetime
{
    private const string InitialCreate = "20260823044436_InitialCreate";
    private static readonly Guid Org = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Site = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Supplier = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Part = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Po = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid PoLine = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid LotWithLine = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid LotWithoutLine = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid Baseline = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private PostgreSqlContainer? _container;
    private string _cs = "";

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("dspc_upgrade").WithUsername("dspc").WithPassword("dspc").Build();
        await _container.StartAsync();
        _cs = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_cs, o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)).Options;
        return new AppDbContext(options);
    }

    private async Task ExecAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var value = await cmd.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, typeof(T));
    }

    /// <summary>Creates the pre-multi-site schema and fills it the way the single-plant build left it.</summary>
    private async Task GivenSinglePlantDatabaseAsync()
    {
        await using (var db = NewContext())
            await db.GetService<IMigrator>().MigrateAsync(InitialCreate);

        await ExecAsync($"""
            INSERT INTO organizations (id, code, name, country, created_at, updated_at)
                VALUES ('{Org}', 'ORG-01', 'Demo Defense Industries', 'PL', now(), now());
            INSERT INTO sites (id, code, name, country, city, latitude, longitude, time_zone, organization_id, created_at, updated_at)
                VALUES ('{Site}', 'SITE-01', 'Zakład Centralny', 'PL', 'Lokalizacja fikcyjna', 52.05, 19.45, 'Europe/Warsaw', '{Org}', now(), now());
            INSERT INTO suppliers (id, code, name, country, city, latitude, longitude, otif_percent, quality_score, is_active, created_at, updated_at)
                VALUES ('{Supplier}', 'SUP-02', 'Hydromech Actuators GmbH', 'DE', 'Stuttgart', 48.78, 9.18, 88, 90, true, now(), now());
            INSERT INTO parts (id, code, name_pl, name_en, unit, criticality, category, has_alternative_supplier, required_document_types_json, created_at, updated_at)
                VALUES ('{Part}', 'ACT-40', 'Siłownik ACT-40', 'Actuator ACT-40', 'szt', 5, 'Mechanika', false, '[]', now(), now());
            INSERT INTO purchase_orders (id, code, supplier_id, site_id, status, ordered_on, created_at, updated_at)
                VALUES ('{Po}', 'PO-2026-0007', '{Supplier}', '{Site}', 'Open', DATE '2026-07-13', now(), now());
            INSERT INTO purchase_order_lines (id, purchase_order_id, line_no, part_id, quantity, delivered_quantity, required_date, eta, original_eta, progress_percent, status, supplier_confirmed, risk_score, risk_category, created_at, updated_at)
                VALUES ('{PoLine}', '{Po}', 1, '{Part}', 12, 0, DATE '2026-08-26', DATE '2026-08-25', DATE '2026-08-25', 100, 'Shipped', true, 44, 'Medium', now(), now());
            INSERT INTO material_lots (id, lot_number, part_id, supplier_id, purchase_order_line_id, quantity, remaining_quantity, unit, status, country_of_origin, created_at, updated_at)
                VALUES ('{LotWithLine}', 'ACT-40-0412', '{Part}', '{Supplier}', '{PoLine}', 12, 12, 'szt', 'Accepted', 'DE', now(), now());
            INSERT INTO material_lots (id, lot_number, part_id, supplier_id, purchase_order_line_id, quantity, remaining_quantity, unit, status, country_of_origin, created_at, updated_at)
                VALUES ('{LotWithoutLine}', 'ACT-40-LEGACY', '{Part}', '{Supplier}', NULL, 5, 5, 'szt', 'Accepted', 'DE', now(), now());
            INSERT INTO planning_baselines (id, version, status, horizon_start, horizon_end, created_at, updated_at)
                VALUES ('{Baseline}', 1, 'Current', DATE '2026-08-17', DATE '2026-11-09', now(), now());
            """);
    }

    [Fact]
    public async Task Migrating_a_populated_single_plant_database_attributes_existing_rows_to_the_default_plant()
    {
        await GivenSinglePlantDatabaseAsync();

        await using (var db = NewContext())
            await db.Database.MigrateAsync();   // must not throw 23503 on the new foreign keys

        // The lot with a purchase-order line follows that line's plant; the orphan lot and the baseline fall back to SITE-01.
        Assert.Equal(Site, await ScalarAsync<Guid>($"SELECT site_id FROM material_lots WHERE id = '{LotWithLine}'"));
        Assert.Equal(Site, await ScalarAsync<Guid>($"SELECT site_id FROM material_lots WHERE id = '{LotWithoutLine}'"));
        Assert.Equal(Site, await ScalarAsync<Guid>($"SELECT site_id FROM planning_baselines WHERE id = '{Baseline}'"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM material_lots WHERE site_id = '00000000-0000-0000-0000-000000000000'"));

        // The seed marker table exists and is still empty: the old data was migrated, not yet re-seeded.
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM seed_metadata"));
    }

    [Fact]
    public async Task Starting_the_api_over_an_upgraded_database_reseeds_it_to_all_four_plants()
    {
        await GivenSinglePlantDatabaseAsync();

        var saved = new[] { "ConnectionStrings__Default", "Storage__Root" }.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        var storage = Path.Combine(Path.GetTempPath(), "dspc-upgrade-storage-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", _cs);
            Environment.SetEnvironmentVariable("Storage__Root", storage);

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Demo"));
            using var client = factory.CreateClient();

            var ready = await client.GetAsync("/health/ready");
            Assert.True(ready.IsSuccessStatusCode, "API did not become ready over an upgraded database: " + await ready.Content.ReadAsStringAsync());

            // A database written by an older seed must be refreshed, not left on stale single-plant data.
            var token = await client.GetFromJsonAsync<JsonElementToken>("/api/v1/auth/demo-login?role=DemoPresenter", ApiFixture.Json);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token!.AccessToken);
            var sites = await client.GetFromJsonAsync<List<SiteRow>>("/api/v1/sites", ApiFixture.Json);
            Assert.Equal(4, sites!.Count);
            Assert.Contains(sites, s => s.Code == "SITE-01" && s.Name.Contains("Kielce"));

            Assert.Equal("2026.08-wave1", await ScalarAsync<string>("SELECT seed_version FROM seed_metadata WHERE id = 1"));
            Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM material_lots WHERE site_id = '00000000-0000-0000-0000-000000000000'"));
        }
        finally
        {
            foreach (var (k, v) in saved) Environment.SetEnvironmentVariable(k, v);
            try { if (Directory.Exists(storage)) Directory.Delete(storage, true); } catch { /* ignore */ }
        }
    }

    private sealed record JsonElementToken(string AccessToken);
    private sealed record SiteRow(string Code, string Name);
}
