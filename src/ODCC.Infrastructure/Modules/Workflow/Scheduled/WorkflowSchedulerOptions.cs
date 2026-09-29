namespace ODCC.Infrastructure.Modules.Workflow.Scheduled;

/// <summary>
/// تنظیمات زمان‌بند گردش کار.
/// </summary>
public sealed class WorkflowSchedulerOptions
{
    public const string SectionName = "Workflows";

    /// <summary>
    /// فعال‌سازی زمان‌بند انقضای درخواست‌های تأیید. یک اثر جانبی است و باید
    /// صریحاً فعال شود (سیاست پروژه).
    /// </summary>
    public bool EnableApprovalExpiryScheduler { get; set; }

    /// <summary>فاصله‌ی هر چرخه‌ی بررسی (ثانیه).</summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>حداکثر تعداد درخواست در هر چرخه.</summary>
    public int MaxPerCycle { get; set; } = 100;
}
