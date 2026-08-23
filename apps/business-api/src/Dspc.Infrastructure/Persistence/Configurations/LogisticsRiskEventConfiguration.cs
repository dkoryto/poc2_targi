using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class LogisticsRiskEventConfiguration : IEntityTypeConfiguration<LogisticsRiskEvent>
{
    public void Configure(EntityTypeBuilder<LogisticsRiskEvent> b)
    {
        b.ToTable("logistics_risk_events"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Type).AsEnumString(); b.Property(e => e.Severity).AsEnumString(16); b.Property(e => e.Region).HasMaxLength(64); b.Property(e => e.Description).HasMaxLength(500);
        b.Ignore(e => e.IsActive); b.HasOne<Supplier>().WithMany().HasForeignKey(e => e.SupplierId); b.HasOne<Shipment>().WithMany().HasForeignKey(e => e.ShipmentId);
    }
}
