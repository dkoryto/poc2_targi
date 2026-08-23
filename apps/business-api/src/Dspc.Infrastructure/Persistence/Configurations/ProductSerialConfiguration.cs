using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class ProductSerialConfiguration : IEntityTypeConfiguration<ProductSerial>
{
    public void Configure(EntityTypeBuilder<ProductSerial> b)
    {
        b.ToTable("product_serials"); b.ConfigureVersioned(); b.HasIndex(e => e.SerialNumber).IsUnique(); b.Property(e => e.SerialNumber).HasMaxLength(48); b.Property(e => e.Status).AsEnumString();
        b.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId); b.HasOne<BomVersion>().WithMany().HasForeignKey(e => e.BomVersionId); b.HasOne(e => e.Passport).WithOne(p => p.ProductSerial).HasForeignKey<Passport>(p => p.ProductSerialId);
    }
}
