using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Persistence.Common;

/// <summary>
/// شکل مشترک تمام موجودیت‌ها در پایگاه داده:
/// کلید GUID، نام snake_case جدول‌ها، حذف نرم، و کنترل همزمانی خوش‌بینانه.
/// </summary>
public static class EntityConfiguration
{
    /// <summary>
    /// اعمال قراردادهای مشترک روی یک موجودیت.
    /// </summary>
    public static EntityTypeBuilder<T> ConfigureBase<T>(this EntityTypeBuilder<T> builder, string tableName)
        where T : BaseEntity
    {
        builder.ToTable(tableName);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.CreatedAt);
        builder.HasQueryFilter(e => !e.IsDeleted); // حذف نرم: فیلتر سراسری روی تمام پرس‌وجوها

        return builder;
    }
}
