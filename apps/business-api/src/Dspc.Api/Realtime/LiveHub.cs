using Dspc.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dspc.Api.Realtime;

/// <summary>Server → client: <c>DomainEvent({ name, occurredAt, correlationId, payload })</c>. Clients only listen.</summary>
[Authorize]
public sealed class LiveHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirst(Infrastructure.Identity.DspcClaims.Role)?.Value;
        if (role is not null) await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");
        var supplier = Context.User?.FindFirst(Infrastructure.Identity.DspcClaims.SupplierCode)?.Value;
        if (supplier is not null) await Groups.AddToGroupAsync(Context.ConnectionId, $"supplier:{supplier}");
        await base.OnConnectedAsync();
    }

    public Task<string> Ping() => Task.FromResult("pong");
}

public sealed class SignalRLiveBroadcaster(IHubContext<LiveHub> hub) : ILiveBroadcaster
{
    public Task BroadcastAsync(string name, DateTime occurredAt, string correlationId, object payload, CancellationToken ct)
        => hub.Clients.All.SendAsync("DomainEvent", new { name, occurredAt, correlationId, payload }, ct);
}
