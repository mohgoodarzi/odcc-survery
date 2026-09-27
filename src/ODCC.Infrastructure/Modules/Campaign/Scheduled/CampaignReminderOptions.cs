namespace ODCC.Infrastructure.Modules.Campaign.Scheduled;

/// <summary>
/// گزینه‌های زمان‌بند یادآورهای کمپین از بخش <c>Campaigns</c> پیکربندی.
/// </summary>
public sealed class CampaignReminderOptions
{
    public const string SectionName = "Campaigns";

    /// <summary>
    /// فعال‌سازی زمان‌بند یادآورها. این یک اثر جانبی است (ارسال پیام) و طبق
    /// سیاست پروژه باید صریحاً فعال شود. پیش‌فرض <c>false</c> است.
    /// </summary>
    public bool EnableReminderScheduler { get; set; }

    /// <summary>فاصله‌ی بررسی یادآورهای سررسیده (ثانیه).</summary>
    public int ReminderPollingIntervalSeconds { get; set; } = 60;
}
