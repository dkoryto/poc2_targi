using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PassportTemplateConfiguration : IEntityTypeConfiguration<PassportTemplate>
{
    public void Configure(EntityTypeBuilder<PassportTemplate> b)
    {
        b.ToTable("passport_templates"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Name).HasMaxLength(200); b.Property(e => e.Description).HasMaxLength(1000); b.HasMany(e => e.Requirements).WithOne().HasForeignKey(r => r.PassportTemplateId);
    }
}
