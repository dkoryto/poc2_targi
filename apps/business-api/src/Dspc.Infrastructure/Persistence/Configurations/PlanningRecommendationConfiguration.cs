using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PlanningRecommendationConfiguration : IEntityTypeConfiguration<PlanningRecommendation>
{
    public void Configure(EntityTypeBuilder<PlanningRecommendation> b)
    {
        b.ToTable("planning_recommendations"); b.ConfigureEntity(); b.Property(e => e.ReasonCode).HasMaxLength(64); b.Property(e => e.OrderCode).HasMaxLength(32); b.Property(e => e.ParamsJson).AsJson();
    }
}
