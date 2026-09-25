using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Questionnaire.Enums;
using ODCC.Domain.Modules.Response.Enums;

namespace ODCC.Application.Modules.Response.Dtos;

/// <summary>
/// یک گزینه‌ی انتخاب‌شده در پاسخ یک سؤال گزینه‌ای.
/// </summary>
public sealed record ResponseAnswerSelectionDto
{
    public Guid OptionId { get; init; }
    public string OptionCode { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}

/// <summary>
/// پاسخ به یک آیتم (سؤال) از پرسشنامه.
/// </summary>
public sealed record ResponseAnswerDto
{
    public Guid Id { get; init; }
    public Guid QuestionnaireItemId { get; init; }
    public Guid QuestionId { get; init; }
    public string QuestionCode { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; }
    public int DisplayOrder { get; init; }
    public string? TextValue { get; init; }
    public decimal? NumericValue { get; init; }
    public IReadOnlyList<ResponseAnswerSelectionDto> Selections { get; init; } = [];

    /// <summary>مهم‌ترین مقدار پاسخ به‌صورت متن (برای نمایش فشرده).</summary>
    public string DisplayText { get; init; } = string.Empty;
}

/// <summary>
/// خروجی استاندارد یک نشست پاسخ‌گویی.
/// </summary>
public sealed record ResponseSessionDto
{
    public Guid Id { get; init; }
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public Guid? CampaignId { get; init; }
    public string? CampaignCode { get; init; }

    public ResponseStatus Status { get; init; }
    public ResponseSource Source { get; init; }
    public bool IsAnonymous { get; init; }

    /// <summary>نام پاسخ‌گو. برای نظرسنجی‌های ناشناس همواره <c>null</c> است.</summary>
    public string? RespondentDisplayName { get; init; }

    public DateTime StartedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime LastActivityAt { get; init; }
    public int AnswerCount { get; init; }

    public IReadOnlyList<ResponseAnswerDto> Answers { get; init; } = [];

    public bool IsEditable { get; init; }
}

/// <summary>
/// خلاصه‌ی نشست برای فهرست مدیریت پاسخ‌ها.
/// </summary>
public sealed record ResponseSessionSummaryDto
{
    public Guid Id { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string? CampaignCode { get; init; }
    public ResponseStatus Status { get; init; }
    public bool IsAnonymous { get; init; }

    /// <summary>نام پاسخ‌گو یا برچسب «ناشناس» (توسط کلاینت).</summary>
    public string? RespondentDisplayName { get; init; }

    public DateTime StartedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public int AnswerCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// یک گزینه‌ی یک سؤال برای نمایش به پاسخ‌گو.
/// </summary>
public sealed record RespondentOptionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}

/// <summary>
/// یک قانون انشعاب برای ارزیابی سمت کلاینت (نمایش/پنهان کردن آیتم‌ها).
/// </summary>
public sealed record RespondentBranchingRuleDto
{
    public Guid TargetItemId { get; init; }
    public BranchingCondition Condition { get; init; }
    public string ExpectedValue { get; init; } = string.Empty;
}

/// <summary>
/// یک آیتم (سؤال) از پرسشنامه برای نمایش به پاسخ‌گو.
/// </summary>
public sealed record RespondentItemDto
{
    public Guid Id { get; init; }
    public Guid SectionId { get; init; }
    public Guid QuestionId { get; init; }
    public string QuestionCode { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsRequired { get; init; }

    /// <summary>عنوان سؤال (یا عنوان جایگزین پرسشنامه) در زبان درخواست‌شده.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>توضیح سؤال در زبان درخواست‌شده.</summary>
    public string? Description { get; init; }

    /// <summary>حداکثر طیف برای سؤال‌های امتیازدهی.</summary>
    public int ScaleMax { get; init; }

    public IReadOnlyList<RespondentOptionDto> Options { get; init; } = [];
    public IReadOnlyList<RespondentBranchingRuleDto> BranchingRules { get; init; } = [];
}

/// <summary>
/// یک بخش از پرسشنامه برای نمایش به پاسخ‌گو.
/// </summary>
public sealed record RespondentSectionDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsOptional { get; init; }
    public IReadOnlyList<RespondentItemDto> Items { get; init; } = [];
}

/// <summary>
/// بسته‌ی کامل یک نظرسنجی برای پاسخ‌گویی: تنظیمات نظرسنجی، ساختار پرسشنامه
/// (بخش‌ها، آیتم‌ها، گزینه‌ها) و — در صورت وجود — نشست قبلی پاسخ‌گو.
/// </summary>
public sealed record RespondentSurveyContextDto
{
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? WelcomeMessage { get; init; }
    public string? ThankYouMessage { get; init; }
    public int EstimatedMinutes { get; init; }
    public bool ShowProgressBar { get; init; }
    public bool IsAnonymous { get; init; }
    public bool AllowEditResponse { get; init; }
    public DateTime? EndDate { get; init; }

    public IReadOnlyList<RespondentSectionDto> Sections { get; init; } = [];

    /// <summary>نشست فعلی پاسخ‌گو برای این نظرسنجی (در صورت وجود).</summary>
    public ResponseSessionDto? Session { get; init; }
}

/// <summary>
/// یک نظرسنجی که کاربر می‌تواند به آن پاسخ دهد (دعوت‌شده یا باز).
/// </summary>
public sealed record RespondableSurveyDto
{
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int EstimatedMinutes { get; init; }
    public bool IsAnonymous { get; init; }

    /// <summary>کمپین دعوت‌کننده (در صورت دعوت شدن از طریق کمپین).</summary>
    public Guid? CampaignId { get; init; }
    public string? CampaignCode { get; init; }

    /// <summary>ردیف توزیع دعوت‌نامه (در صورت وجود).</summary>
    public Guid? DistributionId { get; init; }

    /// <summary>وضعیت نشست پاسخ‌گو برای این نظرسنجی.</summary>
    public ResponseStatus? SessionStatus { get; init; }
}

/// <summary>
/// یک پاسخ در درخواست ذخیره/ارسال.
/// </summary>
public sealed record SaveAnswerRequest
{
    public Guid QuestionnaireItemId { get; init; }

    /// <summary>پاسخ متنی (سؤال‌های متنی و بله/خیر).</summary>
    public string? TextValue { get; init; }

    /// <summary>پاسخ عددی (امتیازدهی و عدد).</summary>
    public decimal? NumericValue { get; init; }

    /// <summary>شناسه‌ی گزینه‌های انتخاب‌شده (سؤال‌های گزینه‌ای).</summary>
    public IReadOnlyList<Guid> SelectedOptionIds { get; init; } = [];
}

/// <summary>
/// درخواست ذخیره‌ی جزئی پاسخ‌ها.
/// </summary>
public sealed record SaveAnswersRequest
{
    public IReadOnlyList<SaveAnswerRequest> Answers { get; init; } = [];
}

/// <summary>
/// درخواست ارسال نهایی پاسخ‌ها.
/// </summary>
public sealed record SubmitResponseRequest
{
    public IReadOnlyList<SaveAnswerRequest> Answers { get; init; } = [];

    /// <summary>
    /// ردیف توزیع دعوت‌نامه (در صورت ورود از طریق کمپین). در نظرسنجی‌های
    /// ناشناس این شناسه در پایگاه داده ذخیره نمی‌شود و فقط برای علامت‌زدن
    /// دعوت‌نامه به‌عنوان «پاسخ‌داده» استفاده می‌شود.
    /// </summary>
    public Guid? DistributionId { get; init; }
}

/// <summary>
/// درخواست شروع یا از سرگیری یک نشست پاسخ‌گویی.
/// </summary>
public sealed record StartSessionRequest
{
    public Guid SurveyId { get; init; }

    /// <summary>کمپین دعوت‌کننده (در صورت دسترسی از طریق کمپین).</summary>
    public Guid? CampaignId { get; init; }

    /// <summary>
    /// ردیف توزیع دعوت‌نامه. برای نظرسنجی‌های ناشناس این شناسه در پایگاه داده‌ی
    /// پاسخ‌ها ذخیره نمی‌شود و فقط برای علامت‌زدن دعوت‌نامه استفاده می‌شود.
    /// </summary>
    public Guid? DistributionId { get; init; }

    public ResponseSource Source { get; init; } = ResponseSource.DirectLink;

    public Language ResponseLanguage { get; init; } = Language.Fa;
}

/// <summary>فیلتر جستجوی نشست‌های پاسخ.</summary>
public sealed record ResponseSearchRequest
{
    public string? SearchText { get; init; }
    public Guid? SurveyId { get; init; }
    public ResponseStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
