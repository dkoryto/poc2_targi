using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PlanningBaselineConfiguration : IEntityTypeConfiguration<PlanningBaseline>
{
    public void Configure(EntityTypeBuilder<PlanningBaseline> b)
    {
        b.ToTable("planning_baselines"); b.ConfigureEntity(); b.HasIndex(e => new { e.SiteId, e.Version }).IsUnique(); b.Property(e => e.Status).AsEnumString(); b.Property(e => e.ApprovedBy).HasMaxLength(100); b.Property(e => e.KpiJson).AsJson(); b.Property(e => e.Notes).HasMaxLength(1000);
        b.HasMany(e => e.Operations).WithOne(o => o.PlanningBaseline).HasForeignKey(o => o.PlanningBaselineId);
        b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId);
    }
}
