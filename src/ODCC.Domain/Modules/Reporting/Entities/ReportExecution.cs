using ODCC.Domain.Common;
using ODCC.Domain.Modules.Reporting.Enums;
using ODCC.Domain.Modules.Reporting.Events;

namespace ODCC.Domain.Modules.Reporting.Entities;

/// <summary>
/// یک اجرای واحد از یک تعریف گزارش.
///
/// هر اجرا یک ردیف مستقل است: وضعیت، زمان‌ها، خروجی تولیدشده (نام و مسیر فایل)،
/// تعداد ردیف‌های داده و (در صورت شکست) پیام خطا را ثبت می‌کند. عکس‌العملِ
/// نام/نوع/قالبِ تعریف در زمان صف‌شدن روی این ردیف کپی می‌شود تا تاریخچه‌ی
/// اجراها حتی پس از تغییر یا بایگانی تعریف اصلی تفسیرپذیر بماند.
///
/// **حذف نرم:** وقتی اجرایی از پنجره‌ی نگه‌داری (<c>RetentionCount</c>) بیرون
/// می‌رود، نرم حذف می‌شود. فایل فیزیکی خروجی توسط لایه‌ی زیرساخت پاک می‌شود
/// تا فضای دیسک اشغال نشود.
/// </summary>
public class ReportExecution : BaseEntity
{
    /// <summary>تعریف گزارشی که این اجرا به آن تعلق دارد.</summary>
    public Guid ReportDefinitionId { get; set; }

    /// <summary>نام تعریف در زمان اجرا (عکس‌العمل).</summary>
    public string ReportName { get; set; } = string.Empty;

    /// <summary>نوع گزارش در زمان اجرا (عکس‌العمل).</summary>
    public ReportType ReportType { get; set; } = ReportType.SurveyAnalytics;

    /// <summary>قالب خروجی در زمان اجرا (عکس‌العمل).</summary>
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;

    /// <summary>وضعیت این اجرا.</summary>
    public ReportExecutionStatus Status { get; set; } = ReportExecutionStatus.Pending;

    /// <summary>زمان صف‌شدن اجرا (UTC).</summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>زمان شروع رندر (UTC).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>زمان پایان اجرا (UTC) — موفق یا ناموفق.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>کاربری که اجرا را راه‌اندازی کرده (در اجرای زمان‌بندی‌شده <c>null</c>).</summary>
    public Guid? TriggeredBy { get; set; }

    /// <summary>نام نمایشی راه‌انداز (عکس‌العمل).</summary>
    public string? TriggeredByName { get; set; }

    // --- خروجی ---------------------------------------------------------------

    /// <summary>نام فایل خروجی (مثلاً «گزارش-NPS-۱۴۰۴۰۷۰۱.pdf»).</summary>
    public string? FileName { get; set; }

    /// <summary>مسیر نسبی فایل خروجی در انبار فایل‌های گزارش.</summary>
    public string? FilePath { get; set; }

    /// <summary>اندازه‌ی فایل خروجی به بایت.</summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>تعداد ردیف‌های داده‌ای که در خروجی نوشته شده.</summary>
    public int? RowCount { get; set; }

    /// <summary>پیام خطا (فقط در صورت شکست اجرا).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>شروع رندر: گذر از صف به حالت اجرا.</summary>
    public void MarkRunning()
    {
        Status = ReportExecutionStatus.Running;
        StartedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ReportExecutionStartedEvent(
            Id, ReportDefinitionId, ReportName, TriggeredBy));
    }

    /// <summary>ثبت موفقیت اجرا و خروجی تولیدشده.</summary>
    public void MarkSucceeded(string fileName, string filePath, long fileSizeBytes, int rowCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Status = ReportExecutionStatus.Succeeded;
        CompletedAt = DateTime.UtcNow;
        FileName = fileName;
        FilePath = filePath;
        FileSizeBytes = fileSizeBytes;
        RowCount = rowCount;
        ErrorMessage = null;

        RaiseDomainEvent(new ReportExecutionSucceededEvent(
            Id, ReportDefinitionId, ReportName, fileName, fileSizeBytes, rowCount, TriggeredBy));
    }

    /// <summary>ثبت شکست اجرا. اجرای شکست‌خورده خروجی ندارد ولی ردیف باقی می‌ماند.</summary>
    public void MarkFailed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        Status = ReportExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        FileName = null;
        FilePath = null;
        FileSizeBytes = null;
        RowCount = null;

        RaiseDomainEvent(new ReportExecutionFailedEvent(
            Id, ReportDefinitionId, ReportName, errorMessage, TriggeredBy));
    }
}
