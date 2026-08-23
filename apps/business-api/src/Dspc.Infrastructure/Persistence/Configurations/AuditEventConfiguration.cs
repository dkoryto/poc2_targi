using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events"); b.HasKey(e => e.Id); b.Property(e => e.Id).ValueGeneratedOnAdd(); b.Property(e => e.UserName).HasMaxLength(100); b.Property(e => e.UserRole).HasMaxLength(40); b.Property(e => e.Action).HasMaxLength(100); b.Property(e => e.Entity).HasMaxLength(64); b.Property(e => e.EntityCode).HasMaxLength(100);
        b.Property(e => e.BeforeJson).AsJson(); b.Property(e => e.AfterJson).AsJson(); b.Property(e => e.CorrelationId).HasMaxLength(64); b.Property(e => e.Source).AsEnumString(16); b.Property(e => e.IpAddress).HasMaxLength(64);
        b.HasIndex(e => e.OccurredAt); b.HasIndex(e => new { e.Entity, e.EntityCode });
    }
}
