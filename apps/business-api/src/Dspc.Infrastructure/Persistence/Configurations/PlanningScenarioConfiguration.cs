using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PlanningScenarioConfiguration : IEntityTypeConfiguration<PlanningScenario>
{
    public void Configure(EntityTypeBuilder<PlanningScenario> b)
    {
        b.ToTable("planning_scenarios"); b.ConfigureVersioned(); b.Property(e => e.Name).HasMaxLength(200); b.Property(e => e.PresetKey).HasMaxLength(64); b.Property(e => e.Status).AsEnumString(); b.EnumCheck<PlanningScenario, PlanningScenarioStatus>("status"); b.Property(e => e.CreatedBy).HasMaxLength(100); b.Property(e => e.DecidedBy).HasMaxLength(100); b.Property(e => e.Solver).HasMaxLength(64); b.Property(e => e.FailureReason).HasMaxLength(1000);
        b.Property(e => e.RequestJson).AsJson(); b.Property(e => e.ResponseJson).AsJson(); b.Property(e => e.BeforeJson).AsJson(); b.Property(e => e.AfterJson).AsJson(); b.Property(e => e.KpiBeforeJson).AsJson(); b.Property(e => e.KpiAfterJson).AsJson(); b.Property(e => e.ExplanationsJson).AsJson();
        b.HasOne(e => e.Baseline).WithMany().HasForeignKey(e => e.BaselineId); b.HasMany(e => e.Changes).WithOne().HasForeignKey(c => c.PlanningScenarioId); b.HasMany(e => e.Recommendations).WithOne().HasForeignKey(r => r.PlanningScenarioId);
    }
}
