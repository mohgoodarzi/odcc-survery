namespace ODCC.Domain.Modules.Analytics.Enums;

/// <summary>
/// بُعد بخش‌بندی تحلیلات. مشخص می‌کند نتایج بر اساس چه معیاری شکسته (segment) می‌شوند.
/// </summary>
public enum AnalyticsSegment
{
    /// <summary>بدون بخش‌بندی — کل نظرسنجی به‌صورت یکپارچه.</summary>
    Survey = 1,

    /// <summary>بخش‌بندی بر اساس کمپین دعوت‌کننده.</summary>
    Campaign = 2,

    /// <summary>بخش‌بندی بر اساس واحد سازمانی پاسخ‌گو (فقط نظرسنجی‌های غیرناشناس).</summary>
    OrgUnit = 3,

    /// <summary>بخش‌بندی بر اساس کانال ورود پاسخ (لینک مستقیم، ایمیل، پیامک، ...).</summary>
    Source = 4
}
