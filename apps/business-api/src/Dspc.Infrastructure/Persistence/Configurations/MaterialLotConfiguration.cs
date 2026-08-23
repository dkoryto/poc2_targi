using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class MaterialLotConfiguration : IEntityTypeConfiguration<MaterialLot>
{
    public void Configure(EntityTypeBuilder<MaterialLot> b)
    {
        b.ToTable("material_lots"); b.ConfigureVersioned(); b.HasIndex(e => e.LotNumber).IsUnique(); b.Property(e => e.LotNumber).HasMaxLength(64); b.Property(e => e.HeatNumber).HasMaxLength(64); b.Property(e => e.BatchNumber).HasMaxLength(64); b.Property(e => e.Unit).HasMaxLength(16); b.Property(e => e.CountryOfOrigin).HasMaxLength(2); b.Property(e => e.BlockReason).HasMaxLength(500);
        b.Property(e => e.Status).AsEnumString(); b.EnumCheck<MaterialLot, MaterialLotStatus>("status"); b.Property(e => e.Quantity).HasPrecision(18, 3); b.Property(e => e.RemainingQuantity).HasPrecision(18, 3);
        b.HasIndex(e => e.SiteId); b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId);
        b.HasOne(e => e.Part).WithMany().HasForeignKey(e => e.PartId); b.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId); b.HasOne(e => e.PurchaseOrderLine).WithMany().HasForeignKey(e => e.PurchaseOrderLineId);
        b.HasMany(e => e.Documents).WithOne(d => d.MaterialLot).HasForeignKey(d => d.MaterialLotId); b.HasMany(e => e.Inspections).WithOne(i => i.MaterialLot).HasForeignKey(i => i.MaterialLotId);
    }
}
