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
        //
        // در SQL Server نوع rowversion توسط خود موتور پایگاه داده تولید و در هر
        // به‌روزرسانی تغییر می‌کند، بنابراین مقایسه‌ی مقدار اصلی بارگذاری‌شده با
        // مقدار ذخیره‌شده همواره برقرار می‌ماند.
        //
        // SQLite قادر به تولید یا به‌روزرسانی rowversion نیست (در SQLite این نوع فقط
        // نام مستعاری برای کلید اصلی است). اگر در SQLite از IsRowVersion() استفاده
        // شود، EF Core در به‌روزرسانیِ موجودیتی که فرزند جدیدی دریافت کرده، یک
        // بلاب خالی را به‌عنوان توکن اصلی می‌فرستد و چون آن ردیف در دیتابیس مقدار
        // متفاوتی دارد، DbUpdateConcurrencyException پرتاب می‌شود. بنابراین در
        // SQLite این ستون یک مقدار پیش‌فرض تصادفی می‌گیرد اما توکن همزمانی
        // محسوب نمی‌شود. این فقط در محیط آزمون (SQLite درون‌حافظه‌ای) رخ می‌دهد
        // و رفتار تولید (SQL Server) را تغییر نمی‌دهد.
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
