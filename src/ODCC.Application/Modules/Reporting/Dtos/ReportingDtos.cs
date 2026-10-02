using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Application.Modules.Reporting.Dtos;

// --- درخواست‌ها -----------------------------------------------------------

/// <summary>درخواست ایجاد یا ویرایش یک تعریف گزارش.</summary>
public sealed record SaveReportRequest
{
    public string Name { get; init; } = string.Empty;

    /// <summary>توضیحات اختیاری.</summary>
    public string? Description { get; init; }

    /// <summary>نوع محتوای گزارش.</summary>
    public ReportType Type { get; init; } = ReportType.SurveyAnalytics;

    /// <summary>قالب خروجی.</summary>
    public ReportFormat Format { get; init; } = ReportFormat.Pdf;

    /// <summary>زمان‌بندی اجرا.</summary>
    public ReportSchedule Schedule { get; init; } = ReportSchedule.OneTime;

    /// <summary>
    /// آیا تعریف بلافاصله فعال شود؟ اگر <c>false</c> باشد، تعریف به‌صورت پیش‌نویس
    /// ذخیره می‌شود و زمان‌بند آن را نادیده می‌گیرد.
    /// </summary>
    public bool ActivateImmediately { get; init; } = true;

    /// <summary>شناسه‌ی نظرسنجی (الزامی برای SurveyAnalytics و BenchmarkComparison).</summary>
    public Guid? SurveyId { get; init; }

    /// <summary>شروع بازه‌ی زمانی گزارش (شامل).</summary>
    public DateTime? From { get; init; }

    /// <summary>پایان بازه‌ی زمانی گزارش (شامل).</summary>
    public DateTime? To { get; init; }

    /// <summary>شناسه‌ی واحد سازمانی محدودکننده (اختیاری).</summary>
    public Guid? OrgUnitId { get; init; }

    /// <summary>آیا زیرمجموعه‌های واحد سازمانی شامل شوند؟</summary>
    public bool IncludeDescendants { get; init; } = true;

    /// <summary>تعداد اجراهایی که خروجی آن‌ها نگه داشته می‌شود.</summary>
    public int RetentionCount { get; init; } = 10;
}

/// <summary>درخواست جستجوی تعاریف گزارش.</summary>
public sealed record ReportSearchRequest
{
    public string? SearchText { get; init; }
    public ReportType? Type { get; init; }
    public ReportStatus? Status { get; init; }

    /// <summary>شامل تعاریف بایگانی‌شده (حذف نرم) شود؟</summary>
    public bool IncludeArchived { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>درخواست جستجوی اجراهای گزارش.</summary>
public sealed record ExecutionSearchRequest
{
    public Guid? ReportDefinitionId { get; init; }
    public ReportExecutionStatus? Status { get; init; }

    /// <summary>شامل اجراهای بایگانی‌شده (حذف نرم) شود؟</summary>
    public bool IncludeArchived { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

// --- خروجی‌ها --------------------------------------------------------------

/// <summary>خروجی یک تعریف گزارش.</summary>
public sealed record ReportDefinitionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ReportType Type { get; init; }
    public ReportFormat Format { get; init; }
    public ReportSchedule Schedule { get; init; }
    public ReportStatus Status { get; init; }

    public Guid? SurveyId { get; init; }
    public string? SurveyCode { get; init; }
    public string? SurveyTitle { get; init; }

    public DateTime? From { get; init; }
    public DateTime? To { get; init; }

    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitPath { get; init; }
    public bool IncludeDescendants { get; init; }

    public Guid? OwnerUserId { get; init; }
    public string? OwnerUserName { get; init; }

    public DateTime? LastExecutedAt { get; init; }
    public DateTime? NextRunAt { get; init; }
    public int RetentionCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    /// <summary>زمان آخرین اجرای موفق (برای نمایش در فهرست).</summary>
    public DateTime? LastSuccessAt { get; init; }

    /// <summary>آیا حداقل یک اجرای موفق برای این تعریف ثبت شده است؟</summary>
    public bool HasSuccessfulExecution => LastSuccessAt.HasValue;
}

/// <summary>خروجی یک اجرای گزارش.</summary>
public sealed record ReportExecutionDto
{
    public Guid Id { get; init; }
    public Guid ReportDefinitionId { get; init; }
    public string ReportName { get; init; } = string.Empty;
    public ReportType ReportType { get; init; }
    public ReportFormat Format { get; init; }
    public ReportExecutionStatus Status { get; init; }

    public DateTime QueuedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    public Guid? TriggeredBy { get; init; }
    public string? TriggeredByName { get; init; }

    /// <summary>آیا خروجی این اجرا برای دانلود آماده است؟</summary>
    public bool HasArtifact => Status == ReportExecutionStatus.Succeeded && !string.IsNullOrWhiteSpace(FileName);

    public string? FileName { get; init; }
    public long? FileSizeBytes { get; init; }
    public int? RowCount { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>مدت زمان اجرا (در صورت تکمیل).</summary>
    public TimeSpan? Duration => StartedAt.HasValue && CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;
}

/// <summary>
/// داده‌ی نمایش‌گرای یک گزارش: مجموعه‌ای از بخش‌های جدولی.
///
/// این DTO دقیقاً همان داده‌ای است که رندر PDF/Excel از آن ساخته می‌شود،
/// بنابراین جدول روی صفحه با فایل قابل‌دانلود یکسان است. مانند خروجی فایل،
/// فقط شامل تجمع‌های تحلیلی است و هیچ شناسه‌ی پاسخ‌گویی ندارد.
/// </summary>
public sealed record ReportDataBundleDto
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public ReportType Type { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string GeneratedBy { get; init; } = string.Empty;
    public IReadOnlyList<ReportSectionDto> Sections { get; init; } = [];
}

/// <summary>یک بخش جدولی از داده‌ی گزارش.</summary>
public sealed record ReportSectionDto
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<ReportColumnDto> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; } = [];
    public string? Footnote { get; init; }
}

/// <summary>یک ستون جدول گزارش برای نمایش روی صفحه.</summary>
public sealed record ReportColumnDto(string Title, ReportColumnType ColumnType = ReportColumnType.Text);

/// <summary>فایل خروجی یک اجرا برای دانلود.</summary>
public sealed record ReportArtifact
{
    /// <summary>جریان محتوای فایل. فراخوان باید آن را dispose کند.</summary>
    public required Stream Content { get; init; }

    public required string FileName { get; init; }

    /// <summary>نوع محتوای HTTP (مثلاً <c>application/pdf</c>).</summary>
    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }
}
