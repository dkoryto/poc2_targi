using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> b)
    {
        b.ToTable("purchase_order_lines"); b.ConfigureVersioned(); b.HasIndex(e => new { e.PurchaseOrderId, e.LineNo }).IsUnique(); b.Property(e => e.Status).AsEnumString(); b.EnumCheck<PurchaseOrderLine, PurchaseOrderLineStatus>("status");
        b.Property(e => e.RiskCategory).AsEnumString(); b.Property(e => e.Quantity).HasPrecision(18, 3); b.Property(e => e.DeliveredQuantity).HasPrecision(18, 3); b.Property(e => e.LotNumber).HasMaxLength(64); b.Property(e => e.HeatNumber).HasMaxLength(64); b.Property(e => e.LastComment).HasMaxLength(1000);
        b.HasOne(e => e.Part).WithMany().HasForeignKey(e => e.PartId); b.HasOne(e => e.Shipment).WithMany(s => s.Lines).HasForeignKey(e => e.ShipmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(e => e.Documents).WithOne(d => d.PurchaseOrderLine).HasForeignKey(d => d.PurchaseOrderLineId); b.HasMany(e => e.History).WithOne().HasForeignKey(h => h.PurchaseOrderLineId); b.HasIndex(e => e.RiskScore);
    }
}
