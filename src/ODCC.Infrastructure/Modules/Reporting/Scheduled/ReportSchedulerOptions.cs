namespace ODCC.Infrastructure.Modules.Reporting.Scheduled;

/// <summary>
/// تنظیمات زمان‌بند گزارش‌ها از بخش <c>Reports</c> پیکربندی.
/// </summary>
public sealed class ReportSchedulerOptions
{
    public const string SectionName = "Reports";

    /// <summary>
    /// آیا زمان‌بند گزارش‌ها فعال باشد؟ طبق سیاست پروژه، اجرای خودکار
    /// یک اثر جانبی است و باید صریحاً فعال شود. پیش‌فرض <c>false</c> است.
    /// </summary>
    public bool EnableScheduler { get; set; }

    /// <summary>دوره‌ی بررسی تعاریف رسیده (ثانیه).</summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>حداکثر تعداد گزارشی که در هر دوره اجرا می‌شود.</summary>
    public int MaxReportsPerCycle { get; set; } = 10;
}
