using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PartDefinitionConfiguration : IEntityTypeConfiguration<PartDefinition>
{
    public void Configure(EntityTypeBuilder<PartDefinition> b)
    {
        b.ToTable("parts"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.NamePl).HasMaxLength(200); b.Property(e => e.NameEn).HasMaxLength(200); b.Property(e => e.Unit).HasMaxLength(16);
        b.Property(e => e.Category).AsEnumString(); b.EnumCheck<PartDefinition, PartCategory>("category"); b.Property(e => e.RequiredDocumentTypesJson).AsJson();
        b.HasOne(e => e.PrimarySupplier).WithMany().HasForeignKey(e => e.PrimarySupplierId); b.HasOne<Supplier>().WithMany().HasForeignKey(e => e.AlternativeSupplierId);
        b.ToTable(t => t.HasCheckConstraint("ck_parts_criticality", "criticality BETWEEN 1 AND 5"));
    }
}
