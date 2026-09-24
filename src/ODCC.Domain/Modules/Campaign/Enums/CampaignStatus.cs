namespace ODCC.Domain.Modules.Campaign.Enums;

/// <summary>
/// وضعیت چرخه‌ی عمر یک کمپین توزیع.
///
/// ماشین وضعیت:
/// <code>
/// Draft ──Schedule──▶ Scheduled (زمان‌بندی برای آینده)
/// Draft | Scheduled ──Launch──▶ Running (توزیع اجرا می‌شود)
/// Running ──Complete──▶ Completed
/// هر حالت ──Archive──▶ Archived
/// </code>
/// </summary>
public enum CampaignStatus
{
    /// <summary>پیش‌نویس — در حال تنظیم مخاطب و زمان‌بندی.</summary>
    Draft = 1,

    /// <summary>زمان‌بندی‌شده — برای اجرای خودکار در زمان مقرر آماده.</summary>
    Scheduled = 2,

    /// <summary>در حال اجرا — توزیع‌ها در حال ارسال/پیگیری هستند.</summary>
    Running = 3,

    /// <summary>تکمیل‌شده — اجرای کمپین به پایان رسیده.</summary>
    Completed = 4,

    /// <summary>بایگانی‌شده — کنار گذاشته شده و فقط خواندنی.</summary>
    Archived = 5
}
