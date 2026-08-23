using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class BomItemConfiguration : IEntityTypeConfiguration<BomItem>
{
    public void Configure(EntityTypeBuilder<BomItem> b)
    {
        b.ToTable("bom_items"); b.ConfigureEntity(); b.Property(e => e.QuantityPerUnit).HasPrecision(18, 3); b.HasOne(e => e.Part).WithMany().HasForeignKey(e => e.PartId); b.HasIndex(e => new { e.BomVersionId, e.PartId }).IsUnique();
    }
}
