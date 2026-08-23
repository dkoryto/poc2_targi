using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class AssemblyLineConfiguration : IEntityTypeConfiguration<AssemblyLine>
{
    public void Configure(EntityTypeBuilder<AssemblyLine> b)
    {
        b.ToTable("assembly_lines"); b.ConfigureEntity(); b.HasIndex(e => e.Code).IsUnique(); b.Property(e => e.Code).HasMaxLength(32); b.Property(e => e.Name).HasMaxLength(200); b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId);
    }
}
