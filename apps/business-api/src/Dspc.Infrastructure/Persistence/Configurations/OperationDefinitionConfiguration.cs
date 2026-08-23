using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class OperationDefinitionConfiguration : IEntityTypeConfiguration<OperationDefinition>
{
    public void Configure(EntityTypeBuilder<OperationDefinition> b)
    {
        b.ToTable("operation_definitions"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(48); b.Property(e => e.NamePl).HasMaxLength(200); b.Property(e => e.NameEn).HasMaxLength(200); b.Property(e => e.Status).AsEnumString(); b.Property(e => e.MaterialRequirementsJson).AsJson();
        b.HasOne(e => e.WorkCenter).WithMany().HasForeignKey(e => e.WorkCenterId);
    }
}
