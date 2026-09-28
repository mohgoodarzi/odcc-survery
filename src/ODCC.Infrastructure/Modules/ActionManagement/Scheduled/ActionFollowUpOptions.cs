namespace ODCC.Infrastructure.Modules.ActionManagement.Scheduled;

/// <summary>
/// تنظیمات پیگیری خودکار اقدامات از بخش <c>Actions:FollowUp</c> پیکربندی.
/// </summary>
public sealed class ActionFollowUpOptions
{
    public const string SectionName = "Actions:FollowUp";

    /// <summary>
    /// آیا زمان‌بند پیگیری فعال باشد؟ طبق سیاست پروژه، یادآور/تشدید یک اثر
    /// جانبی است و باید صریحاً فعال شود. پیش‌فرض <c>false</c> است.
    /// </summary>
    public bool EnableFollowUpScheduler { get; set; }

    /// <summary>دوره‌ی بررسی یادآورها و آیتم‌های سررسیده‌شده (ثانیه).</summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>حداکثر تعداد آیتم پردازش‌شده در هر دوره.</summary>
    public int MaxItemsPerCycle { get; set; } = 100;
}
