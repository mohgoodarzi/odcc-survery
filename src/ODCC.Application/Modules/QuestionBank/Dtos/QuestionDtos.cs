using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;

namespace ODCC.Application.Modules.QuestionBank.Dtos;

/// <summary>
/// ترجمه‌ی یک سؤال.
/// </summary>
public sealed record QuestionLocalizationDto
{
    public Language Language { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>
/// ترجمه‌ی یک گزینه‌ی پاسخ.
/// </summary>
public sealed record QuestionOptionLocalizationDto
{
    public Language Language { get; init; }
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// یک گزینه‌ی پاسخ به‌همراه تمام ترجمه‌های آن.
/// </summary>
public sealed record QuestionOptionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }

    /// <summary>متن گزینه در زبان درخواست‌شده (با تنزل به اولین ترجمه‌ی موجود).</summary>
    public string Text { get; init; } = string.Empty;

    public IReadOnlyList<QuestionOptionLocalizationDto> Localizations { get; init; } = [];
}

/// <summary>
/// خروجی استاندارد یک سؤال کتابخانه برای رابط کاربری.
/// </summary>
public sealed record QuestionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public QuestionType Type { get; init; }
    public int ScaleMax { get; init; }
    public bool IsArchived { get; init; }
    public int CurrentVersionNumber { get; init; }

    /// <summary>متن سؤال در زبان درخواست‌شده (با تنزل به اولین ترجمه‌ی موجود).</summary>
    public string Text { get; init; } = string.Empty;

    public string? Description { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<QuestionOptionDto> Options { get; init; } = [];

    public IReadOnlyList<QuestionLocalizationDto> Localizations { get; init; } = [];

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// یک گزینه در درخواست ایجاد/ویرایش سؤال.
/// </summary>
public sealed record SaveQuestionOptionRequest
{
    /// <summary>شناسه‌ی گزینه‌ی موجود (در ویرایش؛ خالی برای گزینه‌ی جدید).</summary>
    public Guid? Id { get; init; }

    public string Code { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public IReadOnlyList<QuestionOptionLocalizationDto> Localizations { get; init; } = [];
}

/// <summary>
/// درخواست ایجاد یا ویرایش سؤال کتابخانه.
/// </summary>
public sealed record SaveQuestionRequest
{
    public string Code { get; init; } = string.Empty;
    public QuestionType Type { get; init; }
    public int ScaleMax { get; init; } = 5;
    public bool IsArchived { get; init; }

    /// <summary>برچسب‌های سؤال. برچسب‌های حذف‌شده از این لیست کنار گذاشته می‌شوند.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>ترجمه‌های سؤال. حداقل یک ترجمه‌ی فارسی الزامی است.</summary>
    public IReadOnlyList<QuestionLocalizationDto> Localizations { get; init; } = [];

    /// <summary>گزینه‌ها (الزامی برای انواع گزینه‌ای، ممنوع برای سایر انواع).</summary>
    public IReadOnlyList<SaveQuestionOptionRequest> Options { get; init; } = [];
}

/// <summary>فیلتر جستجوی سؤال‌های کتابخانه.</summary>
public sealed record QuestionSearchRequest
{
    public string? SearchText { get; init; }
    public QuestionType? Type { get; init; }
    public string? Tag { get; init; }
    public bool IncludeArchived { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>یک نسخه از تاریخچه‌ی سؤال.</summary>
public sealed record QuestionVersionDto
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string? ChangeSummary { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
}
