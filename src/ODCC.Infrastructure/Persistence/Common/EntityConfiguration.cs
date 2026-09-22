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
    /// <param name="builder">سازنده‌ی پیکربندی موجودیت.</param>
    /// <param name="tableName">نام جدول به snake_case.</param>
    /// <param name="databaseProvider">نام ارائه‌دهنده‌ی پایگاه داده (از <c>DbContext.Database.ProviderName</c>).</param>
    public static EntityTypeBuilder<T> ConfigureBase<T>(this EntityTypeBuilder<T> builder, string tableName, string? databaseProvider = null)
        where T : BaseEntity
    {
        builder.ToTable(tableName);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");
        builder.HasIndex(e => e.CreatedAt);
        builder.HasQueryFilter(e => !e.IsDeleted); // حذف نرم: فیلتر سراسری روی تمام پرس‌وجوها

        // کنترل همزمانی خوش‌بینانه.
        // در SQL Server نوع rowversion توسط خود موتور پایگاه داده تولید می‌شود.
        // SQLite قادر به تولید آن نیست (rowversion در SQLite فقط نام مستعاری برای
        // کلید اصلی است)، بنابراین در SQLite نسخه‌ی ردیف به‌صورت یک مقدار پیش‌فرض
        // تولید می‌شود تا قانون NOT NULL نقض نشود. این فقط در محیط آزمون
        // (SQLite درون‌حافظه‌ای) رخ می‌دهد و رفتار تولید را تغییر نمی‌دهد.
        if (string.Equals(databaseProvider, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            builder.Property(e => e.RowVersion).HasDefaultValueSql("randomblob(8)");
        }
        else
        {
            builder.Property(e => e.RowVersion).IsRowVersion();
        }

        return builder;
    }
}
