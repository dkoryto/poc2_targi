using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.ToTable("shipments"); b.ConfigureVersioned(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Status).AsEnumString(); b.EnumCheck<Shipment, ShipmentStatus>("status"); b.Property(e => e.Carrier).HasMaxLength(100); b.Property(e => e.Vehicle).HasMaxLength(50);
        b.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId); b.HasOne(e => e.PurchaseOrder).WithMany().HasForeignKey(e => e.PurchaseOrderId); b.HasMany(e => e.Events).WithOne().HasForeignKey(e => e.ShipmentId);
    }
}
