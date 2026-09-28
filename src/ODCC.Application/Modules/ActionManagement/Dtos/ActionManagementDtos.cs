using ODCC.Application.Abstractions;
using ODCC.Domain.Modules.ActionManagement.Enums;

namespace ODCC.Application.Modules.ActionManagement.Dtos;

// --- درخواست‌های جستجو -------------------------------------------------------

/// <summary>فیلتر جستجوی برنامه‌های اقدام.</summary>
public sealed record ActionPlanSearchRequest
{
    public string? SearchText { get; init; }
    public ActionPlanStatus? Status { get; init; }
    public ActionPriority? Priority { get; init; }
    public ActionSource? Source { get; init; }
    public Guid? SurveyId { get; init; }
    public Guid? OrgUnitId { get; init; }

    /// <summary>آیا زیرمجموعه‌های واحد سازمانی هم شامل شوند؟</summary>
    public bool IncludeDescendants { get; init; } = true;

    /// <summary>فقط برنامه‌های متعلق به کاربر جاری (مالک یا ایجادکننده).</summary>
    public bool MineOnly { get; init; }

    /// <summary>شامل برنامه‌های بایگانی‌شده (حذف نرم) شود؟</summary>
    public bool IncludeArchived { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>فیلتر جستجوی آیتم‌های اقدام.</summary>
public sealed record ActionItemSearchRequest
{
    public string? SearchText { get; init; }
    public ActionItemStatus? Status { get; init; }
    public ActionPriority? Priority { get; init; }
    public Guid? PlanId { get; init; }
    public Guid? SurveyId { get; init; }
    public Guid? OrgUnitId { get; init; }
    public bool IncludeDescendants { get; init; } = true;

    /// <summary>فقط آیتم‌های منتسب به کاربر جاری.</summary>
    public bool AssignedToMe { get; init; }

    /// <summary>فقط آیتم‌های سررسیده‌شده‌ی باز.</summary>
    public bool OverdueOnly { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

// --- درخواست‌های نوشتن --------------------------------------------------------

public sealed record SaveActionPlanRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }

    /// <summary>شناسه‌ی نظرسنجیِ مرتبط (در صورت ایجاد از یافته‌ی نظرسنجی).</summary>
    public Guid? SurveyId { get; init; }

    /// <summary>شناسه‌ی کمپینِ مرتبط (اختیاری).</summary>
    public Guid? CampaignId { get; init; }

    /// <summary>واحد سازمانی مقصد (null یعنی سراسری شرکت).</summary>
    public Guid? OrgUnitId { get; init; }

    public Guid? OwnerUserId { get; init; }
    public ActionPriority Priority { get; init; } = ActionPriority.Medium;

    /// <summary>مهلت نهایی (UTC). اختیاری.</summary>
    public DateTime? DueDate { get; init; }

    /// <summary>آیا بلافاصله پس از ساخت فعال شود؟ false یعنی پیش‌نویس.</summary>
    public bool ActivateImmediately { get; init; }

    /// <summary>آیتم‌های اولیه‌ی برنامه (اختیاری — می‌توان بعداً اضافه کرد).</summary>
    public IReadOnlyList<SaveActionItemRequest>? Items { get; init; }
}

public sealed record SaveActionItemRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Guid? AssigneeUserId { get; init; }
    public ActionPriority Priority { get; init; } = ActionPriority.Medium;
    public int DisplayOrder { get; init; }

    /// <summary>مهلت نهایی (UTC). اختیاری.</summary>
    public DateTime? DueDate { get; init; }

    /// <summary>زمان یادآوری (UTC). اختیاری — زمان‌بند آن را پردازش می‌کند.</summary>
    public DateTime? RemindAt { get; init; }
}

public sealed record TransitionActionItemRequest
{
    public required ActionItemStatus NewStatus { get; init; }
}

public sealed record AssessEffectivenessRequest
{
    public required EffectivenessRating Rating { get; init; }
    public string? Note { get; init; }
}

public sealed record RecordOutcomeRequest
{
    /// <summary>مقدار شاخص پس از اجرای برنامه (می‌تواند null باشد).</summary>
    public decimal? Value { get; init; }
}

public sealed record AddActionCommentRequest
{
    public required string Body { get; init; }
}

/// <summary>
/// درخواست ساخت خودکار برنامه از هشدار تحلیلات. فقط شاخص‌های تجمعی —
/// <b>هرگز</b> شناسه‌ی پاسخ‌گو.
/// </summary>
public sealed record AnalyticsAlertRequest
{
    public required Guid SurveyId { get; init; }
    public required string SurveyCode { get; init; }
    public required string SurveyTitle { get; init; }

    /// <summary>بُعد بخش‌بندی محاسبه (برای کلید یکتای منبع).</summary>
    public required string SegmentKey { get; init; }

    /// <summary>نوع شاخصی که هشدار بر اساس آن است.</summary>
    public required ActionMetricType MetricType { get; init; }

    /// <summary>مقدار فعلی شاخص.</summary>
    public required decimal MetricValue { get; init; }

    /// <summary>مقدار هدف (بنچمارک).</summary>
    public required decimal TargetValue { get; init; }

    /// <summary>شناسه‌ی واحد سازمانی مقصد (در صورت بخش‌بندی سازمانی).</summary>
    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitPath { get; init; }

    /// <summary>عنوان پیشنهادی برنامه.</summary>
    public required string SuggestedTitle { get; init; }

    /// <summary>کاربری که هشدار را راه‌اندازی کرده (در هشدار خودکار null).</summary>
    public Guid? ActorUserId { get; init; }
}

// --- پیوست‌ها -----------------------------------------------------------------

/// <summary>درخواست آپلود پیوست. محتوا به‌صورت Stream ارائه می‌شود.</summary>
public sealed class UploadActionEvidenceRequest
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSizeBytes { get; init; }
}

// --- خروجی‌ها -----------------------------------------------------------------

public sealed record ActionPlanDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ActionSource Source { get; init; }
    public ActionPlanStatus Status { get; init; }
    public ActionPriority Priority { get; init; }

    public Guid? SurveyId { get; init; }
    public string? SurveyCode { get; init; }
    public string? SurveyTitle { get; init; }
    public Guid? CampaignId { get; init; }

    public ActionMetricType? TriggerMetricType { get; init; }
    public decimal? TriggerMetricValue { get; init; }
    public decimal? OutcomeMetricValue { get; init; }
    public DateTime? OutcomeMeasuredAt { get; init; }

    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitPath { get; init; }

    public Guid? OwnerUserId { get; init; }
    public string? OwnerUserName { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public string? CreatedByUserName { get; init; }

    public DateTime? DueDate { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public int TotalItemCount { get; init; }
    public int CompletedItemCount { get; init; }
    public decimal ProgressPercentage { get; init; }

    /// <summary>درصد آیتم‌هایی که اثربخشی‌شان ارزیابی «موثر» بوده.</summary>
    public decimal? EffectivenessScore { get; init; }

    /// <summary>آیا هیچ آیتم سررسیده‌شده‌ی بازی وجود دارد؟</summary>
    public bool HasOverdueItems { get; init; }
}

public sealed record ActionItemDto
{
    public Guid Id { get; init; }
    public Guid ActionPlanId { get; init; }

    /// <summary>عنوان برنامه‌ی مالک (برای نمایش در فهرست آیتم‌ها).</summary>
    public string? PlanTitle { get; init; }

    /// <summary>وضعیت برنامه‌ی مالک.</summary>
    public ActionPlanStatus? PlanStatus { get; init; }

    /// <summary>اولویت برنامه (برای فیلتر در سطح آیتم).</summary>
    public ActionPriority? PlanPriority { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeUserName { get; init; }
    public ActionPriority Priority { get; init; }
    public ActionItemStatus Status { get; init; }
    public int DisplayOrder { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? RemindAt { get; init; }
    public EscalationLevel EscalationLevel { get; init; }
    public DateTime? EscalatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    public EffectivenessRating Effectiveness { get; init; }
    public string? EffectivenessNote { get; init; }
    public DateTime? EffectivenessAssessedAt { get; init; }

    public int CommentCount { get; init; }
    public int EvidenceCount { get; init; }

    /// <summary>واحد سازمانی و مسیر آن از برنامه‌ی مالک (برای فیلتر دامنه).</summary>
    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitPath { get; init; }

    /// <summary>آیا این آیتم به کاربر جاری منتسب است؟</summary>
    public bool IsAssignedToMe { get; init; }

    public bool IsOverdue { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record ActionCommentDto
{
    public Guid Id { get; init; }
    public Guid ActionItemId { get; init; }
    public Guid? AuthorUserId { get; init; }
    public string? AuthorUserName { get; init; }
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed record ActionEvidenceDto
{
    public Guid Id { get; init; }
    public Guid ActionItemId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public Guid? UploadedByUserId { get; init; }
    public string? UploadedByUserName { get; init; }
    public DateTime UploadedAt { get; init; }
}

/// <summary>محتوای قابل دانلود یک پیوست.</summary>
public sealed class ActionEvidenceContent
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
}

/// <summary>خلاصه‌ی آماری اقدامات برای داشبورد.</summary>
public sealed record ActionStatsDto
{
    public int TotalOpenPlans { get; init; }
    public int TotalActivePlans { get; init; }
    public int TotalCompletedPlans { get; init; }

    public int MyOpenItems { get; init; }
    public int MyOverdueItems { get; init; }

    public int OpenItems { get; init; }
    public int OverdueItems { get; init; }
    public int EscalatedItems { get; init; }
    public int CompletedThisPeriod { get; init; }

    /// <summary>میانگین درصد پیشرفت برنامه‌های فعال.</summary>
    public decimal AveragePlanProgress { get; init; }

    /// <summary>درصد آیتم‌های تکمیل‌شده با ارزیابی «موثر».</summary>
    public decimal? EffectivenessScore { get; init; }
}
