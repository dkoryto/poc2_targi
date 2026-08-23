using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class PassportConfiguration : IEntityTypeConfiguration<Passport>
{
    public void Configure(EntityTypeBuilder<Passport> b)
    {
        b.ToTable("passports"); b.ConfigureVersioned(); b.HasIndex(e => e.ProductSerialId).IsUnique(); b.Property(e => e.Status).AsEnumString(); b.EnumCheck<Passport, PassportStatus>("status"); b.Property(e => e.ApprovedBy).HasMaxLength(100); b.Property(e => e.InvalidationReason).HasMaxLength(500); b.Property(e => e.DeviationsJson).AsJson();
        b.HasOne(e => e.Template).WithMany().HasForeignKey(e => e.PassportTemplateId); b.HasMany(e => e.Versions).WithOne(v => v.Passport).HasForeignKey(v => v.PassportId);
    }
}
