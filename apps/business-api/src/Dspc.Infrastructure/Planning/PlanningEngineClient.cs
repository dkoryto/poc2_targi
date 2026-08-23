using System.Diagnostics;
using System.Net.Http.Json;
using Dspc.Application.Common;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Planning.Scheduling;
using Dspc.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dspc.Infrastructure.Planning;

/// <summary>
/// Typed client for the Java planning engine (<c>POST /api/v1/plan/solve</c>, packages/contracts/planning-engine.yaml).
/// Any transport problem, timeout, error status or unusable body degrades to the deterministic local heuristic so the
/// demonstration never dead-ends — the result is then flagged <c>Heuristic fallback</c> / <c>FALLBACK</c>.
/// </summary>
public sealed class PlanningEngineClient(
    HttpClient http,
    IOptions<PlanningEngineOptions> options,
    PlanningEngineMetrics metrics,
    ILogger<PlanningEngineClient> log) : IPlanningEngine
{
    public const string ClientName = "planning-engine";

    public async Task<EngineOutcome> SolveAsync(PlanningRequest request, CancellationToken ct)
    {
        var timeout = Math.Max(200, options.Value.TimeoutMs);
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeout));
            using var response = await http.PostAsJsonAsync("/api/v1/plan/solve", request, Json.Options, cts.Token);
            if (!response.IsSuccessStatusCode)
                return Fallback(request, $"engine returned {(int)response.StatusCode}", sw);

            var body = await response.Content.ReadFromJsonAsync<PlanningResponse>(Json.Options, cts.Token);
            if (body is null || body.Operations.Count == 0)
                return Fallback(request, "engine returned an empty plan", sw);

            sw.Stop();
            metrics.RecordSolve(sw.ElapsedMilliseconds);
            if (body.ElapsedMs <= 0) body.ElapsedMs = (int)sw.ElapsedMilliseconds;
            log.LogInformation("Planning engine solved {ScenarioId} in {ElapsedMs} ms (round-trip {RoundTripMs} ms, status {Status})",
                request.ScenarioId, body.ElapsedMs, sw.ElapsedMilliseconds, body.Status);
            return new EngineOutcome(body, UsedFallback: false, FallbackReason: null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Fallback(request, $"engine timed out after {timeout} ms", sw);
        }
        catch (Exception ex)
        {
            return Fallback(request, ex.GetType().Name, sw);
        }
    }

    private EngineOutcome Fallback(PlanningRequest request, string reason, Stopwatch sw)
    {
        log.LogWarning("Planning engine unavailable ({Reason}) — using {Solver} for {ScenarioId}",
            reason, BaselineImpactEvaluator.SolverName, request.ScenarioId);
        metrics.RecordFallback();
        var response = new BaselineImpactEvaluator().Evaluate(request);
        response.Status = "FALLBACK";
        response.Solver = BaselineImpactEvaluator.SolverName;
        sw.Stop();
        response.ElapsedMs = (int)sw.ElapsedMilliseconds;
        return new EngineOutcome(response, UsedFallback: true, FallbackReason: reason);
    }
}
