using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class MaterialConsumptionConfiguration : IEntityTypeConfiguration<MaterialConsumption>
{
    public void Configure(EntityTypeBuilder<MaterialConsumption> b)
    {
        b.ToTable("material_consumptions"); b.ConfigureEntity(); b.Property(e => e.Quantity).HasPrecision(18, 3); b.Property(e => e.RecordedBy).HasMaxLength(100);
        b.HasOne(e => e.ProductionOrder).WithMany().HasForeignKey(e => e.ProductionOrderId); b.HasOne<OperationDefinition>().WithMany().HasForeignKey(e => e.OperationDefinitionId); b.HasOne(e => e.MaterialLot).WithMany().HasForeignKey(e => e.MaterialLotId); b.HasOne(e => e.ProductSerial).WithMany().HasForeignKey(e => e.ProductSerialId);
    }
}
