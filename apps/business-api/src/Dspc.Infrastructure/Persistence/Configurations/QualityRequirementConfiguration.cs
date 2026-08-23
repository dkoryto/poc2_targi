using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class QualityRequirementConfiguration : IEntityTypeConfiguration<QualityRequirement>
{
    public void Configure(EntityTypeBuilder<QualityRequirement> b)
    {
        b.ToTable("quality_requirements"); b.ConfigureEntity(); b.HasIndex(e => new { e.PassportTemplateId, e.Code }).IsUnique(); b.Property(e => e.Code).HasMaxLength(64); b.Property(e => e.TitlePl).HasMaxLength(300); b.Property(e => e.TitleEn).HasMaxLength(300); b.Property(e => e.MappingNote).HasMaxLength(1000);
    }
}
