using QuestPDF.Drawing;

namespace ODCC.Infrastructure.Modules.Reporting.Services;

/// <summary>
/// بارگذاری یک فونت فارسی‌پشتیبان برای رندر PDF.
///
/// <b>چرا لازم است:</b> کتابخانه‌ی QuestPDF فقط فونت «Lato» را همراه دارد که
/// گلیف‌های فارسی/عربی را پوشش نمی‌دهد. بدون فونت مناسب، متن فارسی در PDF به‌صورت
/// نویسه‌های نامرئ رندر می‌شود. این کلاس یک فونت سیستم را با نام دلخواه ثبت
/// می‌کند تا در کل عمر برنامه یک‌بار و به‌صورت thread-safe استفاده شود.
///
/// <b>ترتیب جستجو:</b>
/// <list type="number">
///   <item>مسیر صریح از پیکربندی (<c>Reports:PersianFontPath</c>).</item>
///   <item>فونت‌های رایج ویندوز (Tahoma، Arial) که فارسی را کامل پشتیبانی می‌کنند.</item>
///   <item>فونت پیش‌فرض کتابخانه به‌عنوان آخرین راه‌حل (زیرا سامانه ویندوز-اول است).</item>
/// </list>
/// </summary>
internal static class ReportFontLoader
{
    private const string FallbackFontFamily = "Calibri";
    private const string CustomFamilyName = "OdccPersian";

    private static readonly object Gate = new();
    private static string? _configuredPath;
    private static string? _resolvedFontFamily;
    private static bool _usedFallback;

    /// <summary>نام خانواده‌ی فونت بارگذاری‌شده برای استفاده در سبک متن PDF.</summary>
    public static string FontFamily
    {
        get
        {
            lock (Gate)
            {
                return _resolvedFontFamily ??= ResolveFontFamily();
            }
        }
    }

    /// <summary>آیا فونت فارسی پیدا نشد و به فونت پیش‌فرض کتابخانه تنزل شد؟</summary>
    public static bool UsedFallback
    {
        get
        {
            lock (Gate)
            {
                return _usedFallback;
            }
        }
    }

    /// <summary>
    /// تنظیم مسیر فونت پیکربندی‌شده و بارگذاری آن. باید در زمان بوت (قبل از
    /// اولین رندر) فراخوانی شود تا مقدار نهایی قطعی و ثابت بماند.
    /// </summary>
    /// <returns>نام خانواده‌ی فونت بارگذاری‌شده.</returns>
    public static string Initialize(string? configuredFontPath)
    {
        lock (Gate)
        {
            _configuredPath = configuredFontPath;
            return _resolvedFontFamily = ResolveFontFamily();
        }
    }

    private static string ResolveFontFamily()
    {
        _usedFallback = false;

        var candidates = BuildCandidatePaths();

        foreach (var candidate in candidates)
        {
            if (TryRegisterFont(candidate))
            {
                return CustomFamilyName;
            }
        }

        // فونت فارسی موجود نیست: متن فارسی به‌درستی رندر نمی‌شود، ولی سند هنوز
        // تولید می‌شود (اعداد و ساختار جدول‌ها خوانا هستند).
        _usedFallback = true;

        return FallbackFontFamily;
    }

    private static IEnumerable<string> BuildCandidatePaths()
    {
        if (!string.IsNullOrWhiteSpace(_configuredPath))
        {
            yield return Environment.ExpandEnvironmentVariables(_configuredPath);
        }

        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var fontsRoot = Path.Combine(systemRoot, "Fonts");

        // ترتیب بر اساس کیفیت پشتیبانی از فارسی.
        yield return Path.Combine(fontsRoot, "tahoma.ttf");
        yield return Path.Combine(fontsRoot, "arial.ttf");
    }

    private static bool TryRegisterFont(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var stream = File.OpenRead(path);
            FontManager.RegisterFontWithCustomName(CustomFamilyName, stream);

            return true;
        }
        catch
        {
            // فونت نامعتبر یا غیرقابل‌خواندن: کاندیدای بعدی امتحان می‌شود.
            return false;
        }
    }
}
