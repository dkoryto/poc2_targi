using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users"); b.ConfigureVersioned(); b.HasIndex(e => e.Username).IsUnique(); b.Property(e => e.Username).HasMaxLength(100); b.Property(e => e.DisplayName).HasMaxLength(200); b.Property(e => e.PasswordHash).HasMaxLength(500);
        b.Property(e => e.Role).AsEnumString(); b.EnumCheck<User, Role>("role"); b.Property(e => e.Locale).HasMaxLength(5);
        b.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId); b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId);
    }
}
