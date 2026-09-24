namespace ODCC.Domain.Modules.Campaign.Enums;

/// <summary>
/// کانال توزیع دعوت‌نامه‌ی کمپین.
/// ارسال واقعی در ماژول اعلان‌ها (Notification) پیاده‌سازی می‌شود؛
/// کمپین فقط کانال مورد نظر را ثبت می‌کند.
/// </summary>
public enum DistributionChannel
{
    /// <summary>ایمیل سازمانی.</summary>
    Email = 1,

    /// <summary>پیامک.</summary>
    Sms = 2,

    /// <summary>اعلان درون‌برنامه‌ای.</summary>
    InApp = 3,

    /// <summary>لینک عمومی (بدون دعوت‌نامه‌ی شخصی).</summary>
    PublicLink = 4
}
