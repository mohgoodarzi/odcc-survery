using System.Globalization;

namespace ODCC.Infrastructure.Modules.Identity.Services;

/// <summary>
/// ساخت نشانی‌ی عمومی تصویر آواتار از مسیر ذخیره‌شده‌ی آن در انبار فایل‌ها.
///
/// نشانی ساخته‌شده مستقل از فرهنگ است (تصویر آواتار محلی‌سازی ندارد) و شامل
/// نام فایلِ ذخیره‌شده است که با هر بارگذاری جدید تغییر می‌کند. به این ترتیب
/// مرورگر هرگز نسخه‌ی قدیمیِ تصویر را از حافظه‌ی نهان نشان نمی‌دهد.
/// </summary>
internal static class AvatarUrlBuilder
{
    /// <summary>نشانی‌ی عمومی تصویر آواتار یا <c>null</c> اگر کاربر تصویری ندارد.</summary>
    public static string? Build(Guid userId, string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        // مسیرها در ویندوز با «\» و در لینوکس با «/» ذخیره می‌شوند؛
        // فقط بخش آخر (نام فایل) برای نشانی عمومی لازم است.
        var segments = storedPath.Replace('\\', '/').TrimEnd('/').Split('/');

        return string.Concat(
            "/api/identity/users/",
            userId.ToString("D", CultureInfo.InvariantCulture),
            "/avatar/",
            Uri.EscapeDataString(segments[^1]));
    }
}
