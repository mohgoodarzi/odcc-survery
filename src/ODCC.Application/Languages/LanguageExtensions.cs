using ODCC.Domain.Common;

namespace ODCC.Application.Languages;

/// <summary>
/// تنها نقطه‌ای که لایه‌ی کاربرد از بخش فرهنگ مسیر (مثل fa / en) مطلع است.
/// </summary>
public static class LanguageExtensions
{
    private static readonly Dictionary<string, Language> ByCultureSegment = new(StringComparer.OrdinalIgnoreCase)
    {
        { "fa", Language.Fa },
        { "fa-IR", Language.Fa },
        { "en", Language.En },
        { "en-US", Language.En }
    };

    /// <summary>بخش‌های فرهنگی که API می‌پذیرد.</summary>
    public static readonly IReadOnlyCollection<string> SupportedCultureSegments = ["fa", "en"];

    /// <summary>فرهنگ پیش‌فرض سامانه: فارسی.</summary>
    public const string DefaultCultureSegment = "fa";

    /// <summary>
    /// تبدیل بخش مسیر به <see cref="Language"/>. در صورت ناشناخته بودن به فارسی تنزل می‌کند.
    /// </summary>
    public static Language ToLanguage(this string? cultureSegment) =>
        cultureSegment is not null && ByCultureSegment.TryGetValue(cultureSegment, out var language)
            ? language
            : Language.Fa;
}
