using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class BomVersionConfiguration : IEntityTypeConfiguration<BomVersion>
{
    public void Configure(EntityTypeBuilder<BomVersion> b)
    {
        b.ToTable("bom_versions"); b.ConfigureEntity(); b.HasIndex(e => new { e.ProductId, e.Version }).IsUnique(); b.Property(e => e.Version).HasMaxLength(16); b.HasMany(e => e.Items).WithOne().HasForeignKey(i => i.BomVersionId);
    }
}
