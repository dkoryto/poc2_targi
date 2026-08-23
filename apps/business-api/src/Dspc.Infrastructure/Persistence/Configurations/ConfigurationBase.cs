using Dspc.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public static EntityTypeBuilder<T> ConfigureEntity<T>(this EntityTypeBuilder<T> b) where T : Entity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.CreatedAt).IsRequired();
        b.Property(e => e.UpdatedAt).IsRequired();
        return b;
    }

    public static EntityTypeBuilder<T> ConfigureVersioned<T>(this EntityTypeBuilder<T> b) where T : VersionedEntity
    {
        b.ConfigureEntity();
        b.Property(e => e.RowVersion).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        return b;
    }

    public static PropertyBuilder<TEnum> AsEnumString<TEnum>(this PropertyBuilder<TEnum> p, int maxLength = 40) where TEnum : struct, Enum
        => p.HasConversion<string>().HasMaxLength(maxLength);

    public static PropertyBuilder<TEnum?> AsEnumString<TEnum>(this PropertyBuilder<TEnum?> p, int maxLength = 40) where TEnum : struct, Enum
        => p.HasConversion<string>().HasMaxLength(maxLength);

    public static PropertyBuilder<string> AsJson(this PropertyBuilder<string> p) => p.HasColumnType("jsonb");

    public static EntityTypeBuilder<T> EnumCheck<T, TEnum>(this EntityTypeBuilder<T> b, string column) where T : class where TEnum : struct, Enum
    {
        var values = string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"));
        b.ToTable(t => t.HasCheckConstraint($"ck_{typeof(T).Name.ToLowerInvariant()}_{column}", $"{column} IN ({values})"));
        return b;
    }
}
