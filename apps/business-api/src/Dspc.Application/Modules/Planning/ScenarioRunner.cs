using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dspc.Application.Modules.Planning;

/// <summary>In-process queue of scenario runs. The HTTP request only enqueues, so the solve survives the response.</summary>
public sealed class ScenarioRunQueue
{
    private readonly Channel<(Guid ScenarioId, string CorrelationId)> _channel =
        Channel.CreateUnbounded<(Guid, string)>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid scenarioId, string correlationId) => _channel.Writer.TryWrite((scenarioId, correlationId));

    public IAsyncEnumerable<(Guid ScenarioId, string CorrelationId)> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    /// <summary>Test hook: drains one item synchronously (returns false when the queue is empty).</summary>
    public bool TryDequeue(out (Guid ScenarioId, string CorrelationId) item) => _channel.Reader.TryRead(out item);
}

public sealed class ScenarioRunnerHostedService(ScenarioRunQueue queue, IServiceScopeFactory scopes, ILogger<ScenarioRunnerHostedService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (scenarioId, correlationId) in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ScenarioService>();
                await service.ExecuteAsync(scenarioId, correlationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                log.LogError(ex, "Scenario runner failed for {ScenarioId}", scenarioId);
            }
        }
    }
}
