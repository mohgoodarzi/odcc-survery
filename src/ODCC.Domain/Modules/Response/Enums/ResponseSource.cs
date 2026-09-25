namespace ODCC.Domain.Modules.Response.Enums;

/// <summary>
/// کانالی که پاسخ‌گو از طریق آن به نظرسنجی دسترسی پیدا کرده است.
/// برای گزارش‌گیری و محاسبه‌ی نرخ مشارکت به ازای هر کانال استفاده می‌شود.
/// </summary>
public enum ResponseSource
{
    /// <summary>پیوند مستقیم (داخل برنامه، بدون کمپین).</summary>
    DirectLink = 1,

    /// <summary>دعوت‌نامه‌ی ایمیلی یک کمپین.</summary>
    CampaignEmail = 2,

    /// <summary>دعوت‌نامه‌ی پیامکی یک کمپین.</summary>
    CampaignSms = 3,

    /// <summary>اعلان درون‌برنامه‌ای یک کمپین.</summary>
    CampaignInApp = 4,

    /// <summary>پیوند عمومی قابل‌اشتراک (در فاز اعلان‌ها تکمیل می‌شود).</summary>
    PublicLink = 5
}
