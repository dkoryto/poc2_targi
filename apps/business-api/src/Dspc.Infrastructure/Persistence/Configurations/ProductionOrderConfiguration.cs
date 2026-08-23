using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> b)
    {
        b.ToTable("production_orders"); b.ConfigureVersioned(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Status).AsEnumString(); b.EnumCheck<ProductionOrder, ProductionOrderStatus>("status"); b.Property(e => e.CustomerReference).HasMaxLength(100);
        b.ToTable(t => t.HasCheckConstraint("ck_production_orders_priority", "priority BETWEEN 1 AND 5"));
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId); b.HasOne(e => e.BomVersion).WithMany().HasForeignKey(e => e.BomVersionId); b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId); b.HasOne(e => e.AssemblyLine).WithMany().HasForeignKey(e => e.AssemblyLineId);
        b.HasMany(e => e.Operations).WithOne(o => o.ProductionOrder).HasForeignKey(o => o.ProductionOrderId); b.HasMany(e => e.Serials).WithOne(s => s.ProductionOrder).HasForeignKey(s => s.ProductionOrderId);
    }
}
