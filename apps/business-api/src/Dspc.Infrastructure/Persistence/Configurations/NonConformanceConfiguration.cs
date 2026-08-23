using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class NonConformanceConfiguration : IEntityTypeConfiguration<NonConformance>
{
    public void Configure(EntityTypeBuilder<NonConformance> b)
    {
        b.ToTable("non_conformances"); b.ConfigureVersioned(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Title).HasMaxLength(300); b.Property(e => e.Description).HasMaxLength(2000); b.Property(e => e.Status).AsEnumString(); b.Property(e => e.RaisedBy).HasMaxLength(100); b.Property(e => e.Disposition).HasMaxLength(1000);
        b.HasOne(e => e.MaterialLot).WithMany().HasForeignKey(e => e.MaterialLotId); b.HasOne<Supplier>().WithMany().HasForeignKey(e => e.SupplierId);
    }
}
