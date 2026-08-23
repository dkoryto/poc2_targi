using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class RiskAssessmentConfiguration : IEntityTypeConfiguration<RiskAssessment>
{
    public void Configure(EntityTypeBuilder<RiskAssessment> b)
    {
        b.ToTable("risk_assessments"); b.ConfigureEntity(); b.Property(e => e.Category).AsEnumString(); b.Property(e => e.FactorsJson).AsJson(); b.Property(e => e.EndangeredOrdersJson).AsJson(); b.Property(e => e.Trigger).HasMaxLength(64);
        b.HasOne(e => e.PurchaseOrderLine).WithMany().HasForeignKey(e => e.PurchaseOrderLineId); b.HasIndex(e => new { e.PurchaseOrderLineId, e.AssessedAt });
    }
}
