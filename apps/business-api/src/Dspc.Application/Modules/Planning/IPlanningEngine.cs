namespace Dspc.Application.Modules.Planning;

/// <summary>Result of a solve attempt: the plan plus whether the deterministic local fallback produced it.</summary>
public sealed record EngineOutcome(PlanningResponse Response, bool UsedFallback, string? FallbackReason)
{
    public string Solver => Response.Solver;
}

/// <summary>
/// Calls the Java planning engine. Implementations must never throw for transport problems — they degrade to the
/// deterministic local fallback so the demonstration can continue (see docs/adr/0005-scenario-execution-and-fallback.md).
/// </summary>
public interface IPlanningEngine
{
    Task<EngineOutcome> SolveAsync(PlanningRequest request, CancellationToken ct);
}

/// <summary>Last observed solver round-trip, surfaced on the administrator status page.</summary>
public sealed class PlanningEngineMetrics
{
    private long _lastMs = -1;
    private long _lastFallback;

    public long? LastSolverMs => Volatile.Read(ref _lastMs) < 0 ? null : Volatile.Read(ref _lastMs);
    public long FallbackCount => Volatile.Read(ref _lastFallback);

    public void RecordSolve(long ms) => Volatile.Write(ref _lastMs, ms);
    public void RecordFallback() => Interlocked.Increment(ref _lastFallback);
}
