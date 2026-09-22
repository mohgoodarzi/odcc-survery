namespace ODCC.Infrastructure.Modules.Identity;

/// <summary>
/// تنظیمات JWT از پیکربندی (بخش <c>Jwt</c>).
///
/// امنیت: کلید امضا باید حداقل ۲۵۶ بیت باشد و از متغیرهای محیطی یا
/// user secrets تامین شود. در صورت نبودن یا کوتاه بودن کلید،
/// راه‌اندازی برنامه fail-fast می‌شود.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>کلید امضای توکن (حداقل ۳۲ کاراکتر). هرگز در مخزن کد قرار نگیرد.</summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "odcc-survey";

    public string Audience { get; set; } = "odcc-survey-web";

    /// <summary>مدت اعتبار توکن دسترسی به دقیقه.</summary>
    public int AccessExpirationMinutes { get; set; } = 15;

    /// <summary>مدت اعتبار توکن تازه‌سازی به روز.</summary>
    public int RefreshExpirationDays { get; set; } = 7;
}
