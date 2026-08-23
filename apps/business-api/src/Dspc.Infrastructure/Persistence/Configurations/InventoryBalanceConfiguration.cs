using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> b)
    {
        b.ToTable("inventory_balances"); b.ConfigureVersioned(); b.HasIndex(e => new { e.PartId, e.SiteId }).IsUnique(); b.Ignore(e => e.Free); b.Property(e => e.OnHand).HasPrecision(18, 3); b.Property(e => e.Blocked).HasPrecision(18, 3); b.Property(e => e.Reserved).HasPrecision(18, 3);
        b.HasOne(e => e.Part).WithMany().HasForeignKey(e => e.PartId); b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId);
    }
}
