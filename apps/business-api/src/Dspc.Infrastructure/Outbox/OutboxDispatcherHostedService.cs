using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Dspc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dspc.Infrastructure.Outbox;

/// <summary>Polls the outbox and delivers events to in-process handlers and the SignalR broadcaster with retry/backoff.</summary>
public sealed class OutboxDispatcherHostedService(IServiceScopeFactory scopes, ILogger<OutboxDispatcherHostedService> log) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private const int MaxAttempts = 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1500, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogWarning(ex, "Outbox dispatch loop error"); }
            try { await Task.Delay(PollInterval, stoppingToken); } catch (OperationCanceledException) { }
        }
    }

    public async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var broadcaster = scope.ServiceProvider.GetService<ILiveBroadcaster>();
        var now = DateTime.UtcNow;
        var batch = await db.OutboxMessages.Where(m => m.ProcessedAt == null && (m.NextAttemptAt == null || m.NextAttemptAt <= now) && m.Attempts < MaxAttempts)
            .OrderBy(m => m.Id).Take(50).ToListAsync(ct);
        foreach (var msg in batch)
        {
            try
            {
                var type = Type.GetType(msg.EventType);
                object? payload = type is null ? Json.Deserialize<Dictionary<string, object?>>(msg.PayloadJson) : System.Text.Json.JsonSerializer.Deserialize(msg.PayloadJson, type, Json.Options);
                if (payload is IDomainEvent evt && type is not null)
                {
                    var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(type);
                    foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
                    {
                        var m = handlerType.GetMethod("HandleAsync")!;
                        await (Task)m.Invoke(handler, [evt, ct])!;
                    }
                }
                if (broadcaster is not null)
                    await broadcaster.BroadcastAsync(msg.EventName, msg.OccurredAt, msg.CorrelationId, payload ?? new { }, ct);
                msg.ProcessedAt = DateTime.UtcNow;
                msg.Attempts++;
            }
            catch (Exception ex)
            {
                msg.Attempts++;
                msg.LastError = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
                msg.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, msg.Attempts));
                log.LogWarning(ex, "Outbox message {Id} ({Event}) failed attempt {Attempt}", msg.Id, msg.EventName, msg.Attempts);
            }
        }
        if (batch.Count > 0) await db.SaveChangesAsync(ct);
        return batch.Count;
    }
}
