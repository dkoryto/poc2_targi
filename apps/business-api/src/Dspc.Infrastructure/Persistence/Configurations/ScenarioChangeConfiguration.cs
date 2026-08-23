using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class ScenarioChangeConfiguration : IEntityTypeConfiguration<ScenarioChange>
{
    public void Configure(EntityTypeBuilder<ScenarioChange> b)
    {
        b.ToTable("scenario_changes"); b.ConfigureEntity(); b.Property(e => e.Type).AsEnumString(); b.Property(e => e.TargetCode).HasMaxLength(64); b.Property(e => e.ParametersJson).AsJson();
    }
}
