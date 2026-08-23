using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> b)
    {
        b.ToTable("reservations"); b.ConfigureEntity(); b.Property(e => e.Quantity).HasPrecision(18, 3); b.HasOne(e => e.Part).WithMany().HasForeignKey(e => e.PartId); b.HasOne(e => e.ProductionOrder).WithMany(o => o.Reservations).HasForeignKey(e => e.ProductionOrderId); b.HasOne(e => e.MaterialLot).WithMany().HasForeignKey(e => e.MaterialLotId);
    }
}
