using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class CapacityCalendarConfiguration : IEntityTypeConfiguration<CapacityCalendar>
{
    public void Configure(EntityTypeBuilder<CapacityCalendar> b)
    {
        b.ToTable("capacity_calendars"); b.ConfigureEntity(); b.HasIndex(e => new { e.WorkCenterId, e.Date }).IsUnique(); b.Property(e => e.Reason).HasMaxLength(200);
    }
}
