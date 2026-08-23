using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> b)
    {
        b.ToTable("sites"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Name).HasMaxLength(200); b.Property(e => e.TimeZone).HasMaxLength(64); b.Property(e => e.City).HasMaxLength(120); b.Property(e => e.ProfileKey).HasMaxLength(64); b.Property(e => e.FeaturedScenarioKey).HasMaxLength(64);
        b.HasOne<Organization>().WithMany().HasForeignKey(e => e.OrganizationId);
    }
}
