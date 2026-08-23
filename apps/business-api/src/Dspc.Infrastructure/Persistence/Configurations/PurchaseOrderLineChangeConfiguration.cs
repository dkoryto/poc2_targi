using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderLineChangeConfiguration : IEntityTypeConfiguration<PurchaseOrderLineChange>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLineChange> b)
    {
        b.ToTable("purchase_order_line_changes"); b.ConfigureEntity(); b.Property(e => e.Field).HasMaxLength(64); b.Property(e => e.ChangedBy).HasMaxLength(100); b.Property(e => e.Comment).HasMaxLength(1000); b.HasIndex(e => e.PurchaseOrderLineId);
    }
}
