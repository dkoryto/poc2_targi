using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable("purchase_orders"); b.ConfigureVersioned(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Status).AsEnumString(); b.EnumCheck<PurchaseOrder, PurchaseOrderStatus>("status");
        b.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId); b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId); b.HasMany(e => e.Lines).WithOne(l => l.PurchaseOrder).HasForeignKey(l => l.PurchaseOrderId);
    }
}
