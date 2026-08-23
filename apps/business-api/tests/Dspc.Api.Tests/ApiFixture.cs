using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Dspc.Api.Tests;

/// <summary>
/// Boots the real API (migrations + demo seed, T0 pinned to 2026-09-07) against a throwaway PostgreSQL container.
/// Set <c>ConnectionStrings__Test</c> to reuse an existing database instead of Testcontainers (e.g. when Docker is unavailable).
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program>? _factory;
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "dspc-tests-storage-" + Guid.NewGuid().ToString("N"));

    public WebApplicationFactory<Program> Factory => _factory!;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Test");
        if (string.IsNullOrWhiteSpace(cs))
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("dspc_test").WithUsername("dspc").WithPassword("dspc").Build();
            await _container.StartAsync();
            cs = _container.GetConnectionString();
        }
        // Program.cs reads configuration right after WebApplication.CreateBuilder (JWT key fail-fast), i.e. before the
        // factory's ConfigureAppConfiguration runs — so the test settings go through environment variables.
        var settings = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Demo",
            ["ConnectionStrings__Default"] = cs,
            ["Identity__Jwt__Key"] = "dspc-test-jwt-signing-key-0123456789abcdef",
            ["Demo__Enabled"] = "true",
            ["Demo__ClockAnchor"] = "2026-09-07",
            ["Storage__Provider"] = "FileSystem",
            ["Storage__Root"] = _storageRoot,
            ["PlanningEngine__BaseUrl"] = "http://127.0.0.1:1",
            ["Serilog__MinimumLevel__Default"] = "Warning",
            ["RateLimits__loginPerMinute"] = "1000",
            ["RateLimits__resetPerMinute"] = "1000"
        };
        foreach (var (k, v) in settings) Environment.SetEnvironmentVariable(k, v);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Demo"));
        // force host start (migrate + seed happen in the hosted service before the server accepts requests)
        using var client = _factory.CreateClient();
        var ready = await client.GetAsync("/health/ready");
        if (!ready.IsSuccessStatusCode) throw new InvalidOperationException("API not ready: " + await ready.Content.ReadAsStringAsync());
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        if (_container is not null) await _container.DisposeAsync();
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, true); } catch { /* ignore */ }
    }

    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task<HttpClient> AsAsync(string role, string? supplierCode = null)
    {
        var client = Factory.CreateClient();
        var q = supplierCode is null ? "" : $"&supplierCode={supplierCode}";
        var res = await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/demo-login?role={role}{q}", Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", res.GetProperty("accessToken").GetString());
        return client;
    }

    public async Task ResetAsync()
    {
        using var presenter = await AsAsync("DemoPresenter");
        var res = await presenter.PostAsync("/api/v1/demo/reset", null);
        res.EnsureSuccessStatusCode();
    }

    public static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && d is not null; i++, d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "packages", "contracts", "examples", "baseline.json"))) return d.FullName;
        throw new DirectoryNotFoundException("repo root not found");
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture> { }
