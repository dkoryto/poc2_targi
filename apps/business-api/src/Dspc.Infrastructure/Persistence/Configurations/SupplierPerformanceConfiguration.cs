using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class SupplierPerformanceConfiguration : IEntityTypeConfiguration<SupplierPerformance>
{
    public void Configure(EntityTypeBuilder<SupplierPerformance> b)
    {
        b.ToTable("supplier_performances"); b.ConfigureEntity(); b.HasOne(e => e.Supplier).WithMany(s => s.Performance).HasForeignKey(e => e.SupplierId); b.HasIndex(e => new { e.SupplierId, e.PeriodStart }).IsUnique();
    }
}
