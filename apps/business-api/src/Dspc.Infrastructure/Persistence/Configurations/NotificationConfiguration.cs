using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications"); b.ConfigureEntity(); b.Property(e => e.TargetRole).AsEnumString(); b.Property(e => e.Severity).AsEnumString(); b.Property(e => e.TitleKey).HasMaxLength(120); b.Property(e => e.MessageKey).HasMaxLength(120); b.Property(e => e.ParamsJson).AsJson(); b.Property(e => e.Route).HasMaxLength(300);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.UserId); b.HasIndex(e => new { e.TargetRole, e.IsRead });
    }
}
