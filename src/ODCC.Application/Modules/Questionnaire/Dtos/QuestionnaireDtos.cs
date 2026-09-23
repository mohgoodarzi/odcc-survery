using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Questionnaire.Enums;

namespace ODCC.Application.Modules.Questionnaire.Dtos;

/// <summary>
/// ترجمه‌ی عنوان و توضیح پرسشنامه.
/// </summary>
public sealed record QuestionnaireLocalizationDto
{
    public Language Language { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>
/// ترجمه‌ی عنوان یک بخش.
/// </summary>
public sealed record SectionLocalizationDto
{
    public Language Language { get; init; }
    public string Title { get; init; } = string.Empty;
}

/// <summary>
/// یک قانون انشعاب.
/// </summary>
public sealed record BranchingRuleDto
{
    public Guid Id { get; init; }
    public Guid TargetItemId { get; init; }
    public BranchingCondition Condition { get; init; }
    public string ExpectedValue { get; init; } = string.Empty;
}

/// <summary>
/// یک آیتم (سؤال) داخل پرسشنامه.
/// </summary>
public sealed record QuestionnaireItemDto
{
    public Guid Id { get; init; }
    public Guid SectionId { get; init; }
    public Guid QuestionId { get; init; }
    public int QuestionVersionNumber { get; init; }
    public string QuestionCode { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsRequired { get; init; }
    public string? TitleOverride { get; init; }

    /// <summary>متن سؤال در زبان درخواست‌شده (برای نمایش سریع در فهرست).</summary>
    public string QuestionText { get; init; } = string.Empty;

    public IReadOnlyList<BranchingRuleDto> BranchingRules { get; init; } = [];
}

/// <summary>
/// یک بخش از پرسشنامه.
/// </summary>
public sealed record SectionDto
{
    public Guid Id { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsOptional { get; init; }

    /// <summary>عنوان بخش در زبان درخواست‌شده.</summary>
    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<SectionLocalizationDto> Localizations { get; init; } = [];
    public IReadOnlyList<QuestionnaireItemDto> Items { get; init; } = [];
}

/// <summary>
/// خروجی استاندارد یک پرسشنامه (کل ساختار درختی).
/// </summary>
public sealed record QuestionnaireDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public QuestionnaireStatus Status { get; init; }
    public int Version { get; init; }

    /// <summary>عنوان پرسشنامه در زبان درخواست‌شده.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>توضیح پرسشنامه در زبان درخواست‌شده.</summary>
    public string? Description { get; init; }

    public IReadOnlyList<QuestionnaireLocalizationDto> Localizations { get; init; } = [];
    public IReadOnlyList<SectionDto> Sections { get; init; } = [];

    public int SectionCount { get; init; }
    public int ItemCount { get; init; }
    public bool IsPublishable { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// خلاصه‌ی پرسشنامه برای فهرست.
/// </summary>
public sealed record QuestionnaireSummaryDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public QuestionnaireStatus Status { get; init; }
    public int Version { get; init; }
    public string Title { get; init; } = string.Empty;
    public int SectionCount { get; init; }
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// یک قانون انشعاب در درخواست ذخیره.
/// </summary>
public sealed record SaveBranchingRuleRequest
{
    /// <summary>شناسه‌ی قانون موجود (در ویرایش؛ خالی برای قانون جدید).</summary>
    public Guid? Id { get; init; }

    /// <summary>آیتم مقصد — می‌تواند یک آیتم موجود یا یک آیتم جدید در همین درخواست باشد.</summary>
    public Guid TargetItemId { get; init; }
    public BranchingCondition Condition { get; init; }
    public string ExpectedValue { get; init; } = string.Empty;
}

/// <summary>
/// یک آیتم در درخواست ذخیره‌ی پرسشنامه.
/// </summary>
public sealed record SaveItemRequest
{
    /// <summary>شناسه‌ی آیتم موجود (در ویرایش؛ خالی برای آیتم جدید).</summary>
    public Guid? Id { get; init; }

    public Guid QuestionId { get; init; }
    public bool IsRequired { get; init; } = true;
    public string? TitleOverride { get; init; }
    public IReadOnlyList<SaveBranchingRuleRequest> BranchingRules { get; init; } = [];
}

/// <summary>
/// یک بخش در درخواست ذخیره‌ی پرسشنامه.
/// </summary>
public sealed record SaveSectionRequest
{
    /// <summary>شناسه‌ی بخش موجود (در ویرایش؛ خالی برای بخش جدید).</summary>
    public Guid? Id { get; init; }

    public bool IsOptional { get; init; }
    public IReadOnlyList<SectionLocalizationDto> Localizations { get; init; } = [];
    public IReadOnlyList<SaveItemRequest> Items { get; init; } = [];
}

/// <summary>
/// درخواست ایجاد یا ویرایش کامل پرسشنامه (کل ساختار).
/// </summary>
public sealed record SaveQuestionnaireRequest
{
    public string Code { get; init; } = string.Empty;
    public IReadOnlyList<QuestionnaireLocalizationDto> Localizations { get; init; } = [];
    public IReadOnlyList<SaveSectionRequest> Sections { get; init; } = [];
}

/// <summary>فیلتر جستجوی پرسشنامه‌ها.</summary>
public sealed record QuestionnaireSearchRequest
{
    public string? SearchText { get; init; }
    public QuestionnaireStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
