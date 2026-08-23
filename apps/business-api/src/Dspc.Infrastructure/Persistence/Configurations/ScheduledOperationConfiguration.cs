using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class ScheduledOperationConfiguration : IEntityTypeConfiguration<ScheduledOperation>
{
    public void Configure(EntityTypeBuilder<ScheduledOperation> b)
    {
        b.ToTable("scheduled_operations"); b.ConfigureEntity(); b.HasIndex(e => new { e.PlanningBaselineId, e.OperationDefinitionId }).IsUnique(); b.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationDefinitionId);
        b.HasOne<WorkCenter>().WithMany().HasForeignKey(e => e.WorkCenterId); b.HasOne<AssemblyLine>().WithMany().HasForeignKey(e => e.AssemblyLineId);
    }
}
