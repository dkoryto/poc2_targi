using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class ProductDefinitionConfiguration : IEntityTypeConfiguration<ProductDefinition>
{
    public void Configure(EntityTypeBuilder<ProductDefinition> b)
    {
        b.ToTable("products"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.NamePl).HasMaxLength(200); b.Property(e => e.NameEn).HasMaxLength(200); b.Property(e => e.SerialPrefix).HasMaxLength(16); b.Property(e => e.Family).HasMaxLength(64);
        b.HasMany(e => e.BomVersions).WithOne(v => v.Product).HasForeignKey(v => v.ProductId);
    }
}
