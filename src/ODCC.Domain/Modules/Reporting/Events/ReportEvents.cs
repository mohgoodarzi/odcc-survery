using ODCC.Domain.Common;
using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Domain.Modules.Reporting.Events;

/// <summary>
/// رویدادهای دامنه‌ی ماژول گزارش‌گیری.
///
/// این رویدادها توسط شنونده‌ی ممیزی ثبت می‌شوند تا تاریخچه‌ی کامل ایجاد،
/// تغییر، فعال‌سازی، بایگانی و اجرای گزارش‌ها قابل پیگیری باشد. رویدادهای
/// اجرا فقط متاداده (شناسه، نام، اندازه‌ی فایل) حمل می‌کنند — هرگز محتوای
/// داده‌های پاسخ‌گو.
/// </summary>
public sealed class ReportCreatedEvent : DomainEvent
{
    public Guid ReportId { get; init; }
    public string Name { get; init; } = string.Empty;
    public ReportType Type { get; init; }
    public ReportFormat Format { get; init; }
    public ReportSchedule Schedule { get; init; }
    public Guid? ActorUserId { get; init; }

    public ReportCreatedEvent(
        Guid reportId, string name, ReportType type, ReportFormat format,
        ReportSchedule schedule, Guid? actorUserId)
    {
        ReportId = reportId;
        Name = name;
        Type = type;
        Format = format;
        Schedule = schedule;
        ActorUserId = actorUserId;
    }
}

public sealed class ReportUpdatedEvent : DomainEvent
{
    public Guid ReportId { get; init; }
    public string Name { get; init; } = string.Empty;
    public ReportType Type { get; init; }
    public ReportFormat Format { get; init; }
    public ReportSchedule Schedule { get; init; }
    public Guid? ActorUserId { get; init; }

    public ReportUpdatedEvent(
        Guid reportId, string name, ReportType type, ReportFormat format,
        ReportSchedule schedule, Guid? actorUserId)
    {
        ReportId = reportId;
        Name = name;
        Type = type;
        Format = format;
        Schedule = schedule;
        ActorUserId = actorUserId;
    }
}

public sealed class ReportActivatedEvent : DomainEvent
{
    public Guid ReportId { get; init; }
    public string Name { get; init; } = string.Empty;
    public ReportSchedule Schedule { get; init; }
    public DateTime? NextRunAt { get; init; }
    public Guid? ActorUserId { get; init; }

    public ReportActivatedEvent(
        Guid reportId, string name, ReportSchedule schedule, DateTime? nextRunAt, Guid? actorUserId)
    {
        ReportId = reportId;
        Name = name;
        Schedule = schedule;
        NextRunAt = nextRunAt;
        ActorUserId = actorUserId;
    }
}

public sealed class ReportArchivedEvent : DomainEvent
{
    public Guid ReportId { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public ReportArchivedEvent(Guid reportId, string name, Guid? actorUserId)
    {
        ReportId = reportId;
        Name = name;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد «شروع اجرای گزارش».</summary>
public sealed class ReportExecutionStartedEvent : DomainEvent
{
    public Guid ExecutionId { get; init; }
    public Guid ReportId { get; init; }
    public string ReportName { get; init; } = string.Empty;
    public Guid? TriggeredBy { get; init; }

    public ReportExecutionStartedEvent(
        Guid executionId, Guid reportId, string reportName, Guid? triggeredBy)
    {
        ExecutionId = executionId;
        ReportId = reportId;
        ReportName = reportName;
        TriggeredBy = triggeredBy;
    }
}

/// <summary>رویداد «موفقیت اجرای گزارش». فقط متاداده‌ی فایل را حمل می‌کند.</summary>
public sealed class ReportExecutionSucceededEvent : DomainEvent
{
    public Guid ExecutionId { get; init; }
    public Guid ReportId { get; init; }
    public string ReportName { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public int RowCount { get; init; }
    public Guid? TriggeredBy { get; init; }

    public ReportExecutionSucceededEvent(
        Guid executionId, Guid reportId, string reportName,
        string fileName, long fileSizeBytes, int rowCount, Guid? triggeredBy)
    {
        ExecutionId = executionId;
        ReportId = reportId;
        ReportName = reportName;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        RowCount = rowCount;
        TriggeredBy = triggeredBy;
    }
}

/// <summary>رویداد «شکست اجرای گزارش».</summary>
public sealed class ReportExecutionFailedEvent : DomainEvent
{
    public Guid ExecutionId { get; init; }
    public Guid ReportId { get; init; }
    public string ReportName { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public Guid? TriggeredBy { get; init; }

    public ReportExecutionFailedEvent(
        Guid executionId, Guid reportId, string reportName, string errorMessage, Guid? triggeredBy)
    {
        ExecutionId = executionId;
        ReportId = reportId;
        ReportName = reportName;
        ErrorMessage = errorMessage;
        TriggeredBy = triggeredBy;
    }
}
