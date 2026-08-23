using System.Diagnostics;
using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Admin;
using Microsoft.Extensions.Options;

namespace Dspc.Infrastructure.Services;

public sealed class PlanningEngineOptions
{
    public const string Section = "PlanningEngine";
    public string BaseUrl { get; set; } = "http://localhost:8081";
    public int TimeoutMs { get; set; } = 3000;
}

public sealed class ExternalServiceProbe(IHttpClientFactory factory, IOptions<PlanningEngineOptions> engine, IOptions<LocalAiOptions> ai) : IExternalServiceProbe
{
    public Task<ServiceStatus> ProbePlanningEngineAsync(CancellationToken ct) => ProbeAsync("planning-engine", engine.Value.BaseUrl.TrimEnd('/') + "/actuator/health", ct);
    public Task<ServiceStatus> ProbeLocalAiAsync(CancellationToken ct) => ProbeAsync("local-ai", ai.Value.BaseUrl.TrimEnd('/') + "/models", ct);

    private async Task<ServiceStatus> ProbeAsync(string name, string url, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = factory.CreateClient("probe");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(2000));
            var resp = await client.GetAsync(url, cts.Token);
            return new ServiceStatus(name, resp.IsSuccessStatusCode ? "up" : "down", sw.ElapsedMilliseconds, $"{(int)resp.StatusCode} {url}");
        }
        catch (Exception ex)
        {
            return new ServiceStatus(name, "down", sw.ElapsedMilliseconds, ex.GetType().Name + ": " + url);
        }
    }
}
