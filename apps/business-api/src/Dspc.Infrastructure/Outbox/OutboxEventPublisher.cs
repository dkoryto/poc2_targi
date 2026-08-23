using Dspc.Application.Abstractions;
using Dspc.Application.Common;
using Dspc.Domain.Common;
using Dspc.Domain.Entities;

namespace Dspc.Infrastructure.Outbox;

/// <summary>Writes the event to the outbox table inside the caller's unit of work.</summary>
public sealed class OutboxEventPublisher(IAppDbContext db) : IEventPublisher
{
    public void Publish(IDomainEvent domainEvent)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventName = domainEvent.Name,
            EventType = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName ?? domainEvent.Name,
            PayloadJson = Json.Serialize((object)domainEvent),
            CorrelationId = domainEvent.CorrelationId,
            OccurredAt = domainEvent.OccurredAt
        });
    }
}
