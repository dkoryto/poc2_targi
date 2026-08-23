using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PassportVersionConfiguration : IEntityTypeConfiguration<PassportVersion>
{
    public void Configure(EntityTypeBuilder<PassportVersion> b)
    {
        b.ToTable("passport_versions"); b.ConfigureEntity(); b.HasIndex(e => new { e.PassportId, e.Version }).IsUnique(); b.Property(e => e.Status).AsEnumString(); b.Property(e => e.StorageKey).HasMaxLength(300); b.Property(e => e.Sha256).HasMaxLength(64); b.Property(e => e.GeneratedBy).HasMaxLength(100); b.Property(e => e.SnapshotJson).AsJson();
    }
}
