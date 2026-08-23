using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentEventConfiguration : IEntityTypeConfiguration<ShipmentEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentEvent> b)
    {
        b.ToTable("shipment_events"); b.ConfigureEntity(); b.Property(e => e.Type).AsEnumString(); b.Property(e => e.Note).HasMaxLength(1000); b.Property(e => e.Location).HasMaxLength(200); b.Property(e => e.RecordedBy).HasMaxLength(100); b.HasIndex(e => e.ShipmentId);
    }
}
