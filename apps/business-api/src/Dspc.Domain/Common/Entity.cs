namespace Dspc.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Editable entity with optimistic concurrency (PostgreSQL xmin).</summary>
public abstract class VersionedEntity : Entity
{
    public uint RowVersion { get; set; }
}

public interface IDomainEvent
{
    string Name { get; }
    DateTime OccurredAt { get; }
    string CorrelationId { get; }
}

public abstract record DomainEventBase(string Name, DateTime OccurredAt, string CorrelationId) : IDomainEvent;
