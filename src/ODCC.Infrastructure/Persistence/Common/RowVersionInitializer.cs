using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Persistence.Common;

/// <summary>
/// تضمین می‌کند که هر موجودیت جدید یک <c>row_version</c> غیرتهی دارد.
///
/// <b>چرا:</b> در SQL Server نوع <c>rowversion</c> توسط موتور پایگاه داده تولید
/// می‌شود، اما SQLite قادر به تولید آن نیست (در SQLite این نوع فقط نام مستعاری
/// برای کلید اصلی است) و <c>HasDefaultValueSql</c> در زمان INSERT نادیده گرفته
/// می‌شود. اگر مقدار ذخیره‌شده تهی باشد و بعداً موجودیت به‌عنوان <c>Modified</c>
/// ذخیره شود، قانون NOT NULL نقض می‌شود.
///
/// این متد فقط در SQLite (محیط آزمون) یک مقدار تصادفی ۸ بایتی تولید می‌کند و
/// در SQL Server (تولید) بی‌اثر است، زیرا آنجا مقدار نهایی توسط خود پایگاه داده
/// بازنویسی می‌شود و رفتار تولید تغییر نمی‌کند.
/// </summary>
public static class RowVersionInitializer
{
    /// <summary>طول بلاب تصادفی تولیدشده در SQLite.</summary>
    private const int RowVersionLength = 8;

    /// <summary>نام ارائه‌دهنده‌ی SQLite که این جبران‌سازی فقط برای آن فعال است.</summary>
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    /// <summary>
    /// پر کردن <c>RowVersion</c> موجودیت‌های جدیدی که مقدار تهی یا خالی دارند.
    /// باید پیش از <c>SaveChangesAsync</c> و فقط روی SQLite فراخوانی شود.
    /// </summary>
    public static void EnsureRowVersions(DbContext context)
    {
        if (!string.Equals(context.Database.ProviderName, SqliteProvider, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            // فقط مقدارهای تهی یا خالی را پر می‌کند تا هرگز مقدار مشخص‌شده‌ی
            // صریح توسط فراخوان را بازنویسی نکند.
            var current = entry.Entity.RowVersion;
            if (current is null || current.Length == 0)
            {
                entry.Entity.RowVersion = GenerateRowVersion();
            }
        }
    }

    private static byte[] GenerateRowVersion()
    {
        var bytes = new byte[RowVersionLength];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }
}
