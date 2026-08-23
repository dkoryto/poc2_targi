using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class WorkCenterConfiguration : IEntityTypeConfiguration<WorkCenter>
{
    public void Configure(EntityTypeBuilder<WorkCenter> b)
    {
        b.ToTable("work_centers"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.NamePl).HasMaxLength(200); b.Property(e => e.NameEn).HasMaxLength(200);
        b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId); b.HasOne(e => e.AssemblyLine).WithMany().HasForeignKey(e => e.AssemblyLineId); b.HasMany(e => e.Calendar).WithOne().HasForeignKey(c => c.WorkCenterId);
    }
}
