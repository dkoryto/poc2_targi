using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class QualityInspectionConfiguration : IEntityTypeConfiguration<QualityInspection>
{
    public void Configure(EntityTypeBuilder<QualityInspection> b)
    {
        b.ToTable("quality_inspections"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Result).AsEnumString(); b.Property(e => e.InspectedBy).HasMaxLength(100); b.Property(e => e.Notes).HasMaxLength(2000); b.Property(e => e.MeasurementsJson).AsJson();
        b.HasOne(e => e.ProductSerial).WithMany().HasForeignKey(e => e.ProductSerialId);
    }
}
