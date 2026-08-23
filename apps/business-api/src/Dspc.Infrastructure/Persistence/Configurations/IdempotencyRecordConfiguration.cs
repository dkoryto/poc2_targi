using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records"); b.HasKey(e => e.Key); b.Property(e => e.Key).HasMaxLength(128); b.Property(e => e.RequestHash).HasMaxLength(64); b.Property(e => e.ResponseBody).HasColumnType("text"); b.HasIndex(e => e.CreatedAt);
    }
}
