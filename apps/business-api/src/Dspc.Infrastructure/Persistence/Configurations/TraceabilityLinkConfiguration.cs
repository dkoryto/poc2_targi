using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class TraceabilityLinkConfiguration : IEntityTypeConfiguration<TraceabilityLink>
{
    public void Configure(EntityTypeBuilder<TraceabilityLink> b)
    {
        b.ToTable("traceability_links"); b.ConfigureEntity(); b.Property(e => e.Kind).AsEnumString(); b.Property(e => e.FromType).HasMaxLength(48); b.Property(e => e.ToType).HasMaxLength(48); b.Property(e => e.FromCode).HasMaxLength(64); b.Property(e => e.ToCode).HasMaxLength(64);
        b.HasIndex(e => new { e.FromType, e.FromId }); b.HasIndex(e => new { e.ToType, e.ToId });
    }
}
