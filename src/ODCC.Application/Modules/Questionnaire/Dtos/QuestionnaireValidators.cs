using FluentValidation;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Questionnaire.Enums;

namespace ODCC.Application.Modules.Questionnaire.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش پرسشنامه.
/// </summary>
public sealed class SaveQuestionnaireRequestValidator : AbstractValidator<SaveQuestionnaireRequest>
{
    public SaveQuestionnaireRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد پرسشنامه الزامی است.")
            .MaximumLength(64).WithMessage("کد پرسشنامه نمی‌تواند بیش از ۶۴ کاراکتر باشد.")
            .Matches("^[A-Za-z0-9\\-_.]+$").WithMessage("کد پرسشنامه فقط می‌تواند شامل حروف انگلیسی، رقم، خط تیره و زیرخط باشد.");

        RuleFor(x => x.Localizations)
            .NotEmpty().WithMessage("حداقل یک ترجمه برای پرسشنامه الزامی است.")
            .Must(locs => locs.Any(l => l.Language == Language.Fa))
            .WithMessage("ترجمه‌ی فارسی پرسشنامه الزامی است.")
            .Must(locs => locs.Select(l => l.Language).Distinct().Count() == locs.Count)
            .WithMessage("هر زبان فقط یک ترجمه می‌تواند داشته باشد.");

        RuleForEach(x => x.Localizations).SetValidator(new QuestionnaireLocalizationDtoValidator());
        RuleForEach(x => x.Sections).SetValidator(new SaveSectionRequestValidator());

        RuleFor(x => x.Sections)
            .Must(sections => sections.Select(s => s.Id).Count(id => id.HasValue) ==
                              sections.Select(s => s.Id).Where(id => id.HasValue).Distinct().Count())
            .WithMessage("شناسه‌ی بخش‌های ارسالی نباید تکراری باشد.");
    }
}

public sealed class QuestionnaireLocalizationDtoValidator : AbstractValidator<QuestionnaireLocalizationDto>
{
    public QuestionnaireLocalizationDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان پرسشنامه الزامی است.")
            .MaximumLength(256).WithMessage("عنوان نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2048).WithMessage("توضیح نمی‌تواند بیش از ۲۰۴۸ کاراکتر باشد.");
    }
}

public sealed class SaveSectionRequestValidator : AbstractValidator<SaveSectionRequest>
{
    public SaveSectionRequestValidator()
    {
        RuleFor(x => x.Localizations)
            .NotEmpty().WithMessage("هر بخش باید حداقل یک ترجمه داشته باشد.")
            .Must(locs => locs.Any(l => l.Language == Language.Fa))
            .WithMessage("ترجمه‌ی فارسی بخش الزامی است.")
            .Must(locs => locs.Select(l => l.Language).Distinct().Count() == locs.Count)
            .WithMessage("هر زبان فقط یک ترجمه می‌تواند داشته باشد.");

        RuleForEach(x => x.Localizations).SetValidator(new SectionLocalizationDtoValidator());
        RuleForEach(x => x.Items).SetValidator(new SaveItemRequestValidator());
    }
}

public sealed class SectionLocalizationDtoValidator : AbstractValidator<SectionLocalizationDto>
{
    public SectionLocalizationDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان بخش الزامی است.")
            .MaximumLength(256).WithMessage("عنوان بخش نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");
    }
}

public sealed class SaveItemRequestValidator : AbstractValidator<SaveItemRequest>
{
    public SaveItemRequestValidator()
    {
        RuleFor(x => x.QuestionId)
            .NotEmpty().WithMessage("هر آیتم باید به یک سؤال از کتابخانه ارجاع دهد.");

        RuleFor(x => x.TitleOverride)
            .MaximumLength(1024).WithMessage("عنوان جایگزین نمی‌تواند بیش از ۱۰۲۴ کاراکتر باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.TitleOverride));

        RuleForEach(x => x.BranchingRules).SetValidator(new SaveBranchingRuleRequestValidator());
    }
}

public sealed class SaveBranchingRuleRequestValidator : AbstractValidator<SaveBranchingRuleRequest>
{
    public SaveBranchingRuleRequestValidator()
    {
        RuleFor(x => x.TargetItemId)
            .NotEmpty().WithMessage("آیتم مقصد انشعاب الزامی است.");

        RuleFor(x => x.ExpectedValue)
            .NotEmpty().WithMessage("مقدار مورد انتظار الزامی است.")
            .MaximumLength(256).WithMessage("مقدار مورد انتظار نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        // مقایسه‌های عددی فقط برای سؤال‌های عددی/امتیازی معنا دارند —
        // این بررسی در سرویس انجام می‌شود چون نیاز به دانستن نوع سؤال مبدأ دارد.
        RuleFor(x => x.Condition)
            .IsInEnum().WithMessage("نوع شرط انشعاب نامعتبر است.");
    }
}

/// <summary>
/// اعتبارسنجی فیلتر جستجوی پرسشنامه‌ها.
/// </summary>
public sealed class QuestionnaireSearchRequestValidator : AbstractValidator<QuestionnaireSearchRequest>
{
    public QuestionnaireSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("اندازه‌ی صفحه باید بین ۱ و ۱۰۰ باشد.");
    }
}

/// <summary>
/// شرایط مجاز برای شرط انشعاب بر اساس نوع سؤال مبدأ.
/// </summary>
public static class BranchingConditionRules
{
    /// <summary>آیا این شرط برای این نوع سؤال مجاز است؟</summary>
    public static bool IsApplicable(BranchingCondition condition, QuestionType questionType) =>
        questionType switch
        {
            QuestionType.SingleChoice => condition is BranchingCondition.Equals or BranchingCondition.NotEquals,
            QuestionType.MultipleChoice => condition is BranchingCondition.Contains,
            QuestionType.Rating => condition is BranchingCondition.Equals or BranchingCondition.NotEquals
                or BranchingCondition.GreaterThan or BranchingCondition.LessThan,
            QuestionType.YesNo => condition is BranchingCondition.Equals or BranchingCondition.NotEquals,
            QuestionType.Number => condition is BranchingCondition.Equals or BranchingCondition.NotEquals
                or BranchingCondition.GreaterThan or BranchingCondition.LessThan,
            QuestionType.ShortText or QuestionType.LongText => condition is BranchingCondition.Equals
                or BranchingCondition.NotEquals or BranchingCondition.Contains,
            _ => false
        };
}
