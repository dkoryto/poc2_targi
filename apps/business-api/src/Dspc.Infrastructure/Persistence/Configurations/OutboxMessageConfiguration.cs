using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages"); b.HasKey(e => e.Id); b.Property(e => e.Id).ValueGeneratedOnAdd(); b.Property(e => e.EventName).HasMaxLength(100); b.Property(e => e.EventType).HasMaxLength(300); b.Property(e => e.PayloadJson).AsJson(); b.Property(e => e.CorrelationId).HasMaxLength(64); b.Property(e => e.LastError).HasMaxLength(2000);
        b.HasIndex(e => new { e.ProcessedAt, e.NextAttemptAt });
    }
}
