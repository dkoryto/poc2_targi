using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("suppliers"); b.ConfigureVersioned(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Name).HasMaxLength(200); b.Property(e => e.Country).HasMaxLength(2); b.Property(e => e.City).HasMaxLength(100);
    }
}
