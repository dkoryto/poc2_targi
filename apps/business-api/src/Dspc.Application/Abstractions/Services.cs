using Dspc.Domain.Common;
using Dspc.Domain.Entities;

namespace Dspc.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? Id { get; }
    string Username { get; }
    Role? Role { get; }
    Guid? SupplierId { get; }
    string? SupplierCode { get; }
    Guid? SiteId { get; }
    string CorrelationId { get; }
    string? IpAddress { get; }
    bool IsSupplier => Role == Domain.Common.Role.SupplierUser;
    bool IsInRole(params Role[] roles) => Role is { } r && roles.Contains(r);
}

/// <summary>Supplier users only ever see their own organisation's data.</summary>
public interface ISupplierScope
{
    bool IsRestricted { get; }
    Guid? SupplierId { get; }
    IQueryable<PurchaseOrder> Apply(IQueryable<PurchaseOrder> q);
    IQueryable<PurchaseOrderLine> Apply(IQueryable<PurchaseOrderLine> q);
    IQueryable<Shipment> Apply(IQueryable<Shipment> q);
    IQueryable<QualityDocument> Apply(IQueryable<QualityDocument> q);
    IQueryable<MaterialLot> Apply(IQueryable<MaterialLot> q);
    IQueryable<Supplier> Apply(IQueryable<Supplier> q);
}

public interface IEventPublisher
{
    /// <summary>Adds an outbox message to the current unit of work (persisted with SaveChanges).</summary>
    void Publish(IDomainEvent domainEvent);
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}

public interface ILiveBroadcaster
{
    Task BroadcastAsync(string name, DateTime occurredAt, string correlationId, object payload, CancellationToken ct);
}

public interface IAuditWriter
{
    void Write(string action, string entity, string entityCode, Guid? entityId, object? before, object? after, AuditSource source = AuditSource.Api);
}

public interface IDemoClock
{
    /// <summary>Monday 06:00 site time of the demo week, as UTC.</summary>
    DateTime T0Utc { get; }
    DateOnly T0Date { get; }
    DateTime UtcNow { get; }
    TimeZoneInfo SiteTimeZone { get; }
    DateTime ToSiteLocal(DateTime utc);
    DateTime FromSiteLocal(DateTime local);
    DateOnly Today { get; }
}

public interface IDocumentStorage
{
    string Provider { get; }
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<Stream?> GetAsync(string key, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<bool> HealthCheckAsync(CancellationToken ct);
}

/// <summary>Replaceable malware scanner adapter. Demo ships a no-op implementation that only logs — see SECURITY.md.</summary>
public interface IFileScanner
{
    Task<(bool Clean, string? Reason)> ScanAsync(byte[] content, string fileName, CancellationToken ct);
}

public interface ISeedPostProcessor
{
    int Order { get; }
    Task RunAsync(CancellationToken ct);
}

public sealed record SeedResult(long DurationMs, string SeedVersion, DateTime SeededAt, IReadOnlyDictionary<string, int> Counts);

public interface IDemoSeeder
{
    Task<SeedResult> SeedIfEmptyAsync(CancellationToken ct);
    Task<SeedResult> ResetAsync(CancellationToken ct);
    SeedResult? LastResult { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed record IssuedToken(string AccessToken, DateTime ExpiresAt);

public interface IJwtTokenIssuer
{
    IssuedToken Issue(User user, Supplier? supplier);
}

public sealed record RecentError(DateTime At, string Operation, string Message, string CorrelationId);

public interface IRecentErrors
{
    void Record(string operation, string message, string correlationId);
    IReadOnlyList<RecentError> List();
}

public sealed record ServiceStatus(string Name, string Status, long? LatencyMs, string? Detail);

public interface IExternalServiceProbe
{
    Task<ServiceStatus> ProbePlanningEngineAsync(CancellationToken ct);
    Task<ServiceStatus> ProbeLocalAiAsync(CancellationToken ct);
}
