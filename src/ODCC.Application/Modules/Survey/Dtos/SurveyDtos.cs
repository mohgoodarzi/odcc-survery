using ODCC.Domain.Common;
using ODCC.Domain.Modules.Survey.Enums;

namespace ODCC.Application.Modules.Survey.Dtos;

/// <summary>
/// ترجمه‌ی عنوان، توضیح و پیام‌های یک نظرسنجی.
/// </summary>
public sealed record SurveyLocalizationDto
{
    public Language Language { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? WelcomeMessage { get; init; }
    public string? ThankYouMessage { get; init; }
}

/// <summary>
/// ترجمه‌ی عنوان و توضیح یک قالب نظرسنجی.
/// </summary>
public sealed record SurveyTemplateLocalizationDto
{
    public Language Language { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>
/// خروجی استاندارد یک نظرسنجی.
/// </summary>
public sealed record SurveyDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public SurveyStatus Status { get; init; }

    public Guid QuestionnaireId { get; init; }
    public int QuestionnaireVersion { get; init; }
    public string QuestionnaireCode { get; init; } = string.Empty;
    public Guid? TemplateId { get; init; }

    // تنظیمات
    public bool IsAnonymous { get; init; }
    public bool AllowEditResponse { get; init; }
    public bool ShowProgressBar { get; init; }
    public bool SingleResponsePerUser { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int EstimatedMinutes { get; init; }

    /// <summary>عنوان در زبان درخواست‌شده.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>توضیح در زبان درخواست‌شده.</summary>
    public string? Description { get; init; }

    public string? WelcomeMessage { get; init; }
    public string? ThankYouMessage { get; init; }

    public IReadOnlyList<SurveyLocalizationDto> Localizations { get; init; } = [];

    public bool IsPublishable { get; init; }
    public bool AcceptsResponses { get; init; }

    public DateTime? PublishedAt { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public DateTime? ArchivedAt { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// خلاصه‌ی نظرسنجی برای فهرست.
/// </summary>
public sealed record SurveySummaryDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public SurveyStatus Status { get; init; }
    public string Title { get; init; } = string.Empty;
    public string QuestionnaireCode { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// خروجی استاندارد یک قالب نظرسنجی.
/// </summary>
public sealed record SurveyTemplateDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public SurveyTemplateStatus Status { get; init; }
    public Guid QuestionnaireId { get; init; }
    public string QuestionnaireCode { get; init; } = string.Empty;

    public bool IsAnonymous { get; init; }
    public bool AllowEditResponse { get; init; }
    public bool ShowProgressBar { get; init; }
    public bool SingleResponsePerUser { get; init; }
    public int EstimatedMinutes { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    public IReadOnlyList<SurveyTemplateLocalizationDto> Localizations { get; init; } = [];

    public bool IsUsable { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// خلاصه‌ی قالب برای فهرست.
/// </summary>
public sealed record SurveyTemplateSummaryDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public SurveyTemplateStatus Status { get; init; }
    public string Title { get; init; } = string.Empty;
    public string QuestionnaireCode { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// درخواست ایجاد یا ویرایش یک نظرسنجی.
/// </summary>
public sealed record SaveSurveyRequest
{
    public string Code { get; init; } = string.Empty;
    public Guid QuestionnaireId { get; init; }
    public IReadOnlyList<SurveyLocalizationDto> Localizations { get; init; } = [];

    public bool IsAnonymous { get; init; }
    public bool AllowEditResponse { get; init; } = true;
    public bool ShowProgressBar { get; init; } = true;
    public bool SingleResponsePerUser { get; init; } = true;

    /// <summary>زمان باز شدن پنجره‌ی پاسخ‌گویی (UTC) یا خالی برای شروع فوری پس از انتشار.</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>زمان بسته شدن پنجره‌ی پاسخ‌گویی (UTC) یا خالی برای بدون انقضا.</summary>
    public DateTime? EndDate { get; init; }

    public int EstimatedMinutes { get; init; } = 5;
}

/// <summary>
/// درخواست ایجاد یا ویرایش یک قالب نظرسنجی.
/// </summary>
public sealed record SaveSurveyTemplateRequest
{
    public string Code { get; init; } = string.Empty;
    public Guid QuestionnaireId { get; init; }
    public IReadOnlyList<SurveyTemplateLocalizationDto> Localizations { get; init; } = [];

    public bool IsAnonymous { get; init; }
    public bool AllowEditResponse { get; init; } = true;
    public bool ShowProgressBar { get; init; } = true;
    public bool SingleResponsePerUser { get; init; } = true;
    public int EstimatedMinutes { get; init; } = 5;
}

/// <summary>
/// درخواست ساخت یک نظرسنجی از روی یک قالب.
/// </summary>
public sealed record CreateSurveyFromTemplateRequest
{
    public Guid TemplateId { get; init; }

    /// <summary>کد یکتای نظرسنجیِ جدید.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>توضیحات/پیام‌های نظرسنجی جدید (در صورت خالی بودن، از قالب کپی می‌شود).</summary>
    public IReadOnlyList<SurveyLocalizationDto>? Localizations { get; init; }

    /// <summary>لغو تاریخ شروع قالب (در صورت نیاز).</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>لغو تاریخ پایان قالب (در صورت نیاز).</summary>
    public DateTime? EndDate { get; init; }
}

/// <summary>فیلتر جستجوی نظرسنجی‌ها.</summary>
public sealed record SurveySearchRequest
{
    public string? SearchText { get; init; }
    public SurveyStatus? Status { get; init; }
    public Guid? QuestionnaireId { get; init; }
    public Guid? TemplateId { get; init; }
    public bool IncludeArchived { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>فیلتر جستجوی قالب‌های نظرسنجی.</summary>
public sealed record SurveyTemplateSearchRequest
{
    public string? SearchText { get; init; }
    public SurveyTemplateStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
