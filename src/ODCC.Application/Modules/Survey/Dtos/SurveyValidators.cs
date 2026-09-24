using FluentValidation;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Survey.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش نظرسنجی.
/// </summary>
public sealed class SaveSurveyRequestValidator : AbstractValidator<SaveSurveyRequest>
{
    public SaveSurveyRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد نظرسنجی الزامی است.")
            .MaximumLength(64).WithMessage("کد نظرسنجی نمی‌تواند بیش از ۶۴ کاراکتر باشد.")
            .Matches("^[A-Za-z0-9\\-_.]+$").WithMessage("کد نظرسنجی فقط می‌تواند شامل حروف انگلیسی، رقم، خط تیره و زیرخط باشد.");

        RuleFor(x => x.QuestionnaireId)
            .NotEmpty().WithMessage("هر نظرسنجی باید به یک پرسشنامه ارجاع دهد.");

        RuleFor(x => x.Localizations)
            .NotEmpty().WithMessage("حداقل یک ترجمه برای نظرسنجی الزامی است.")
            .Must(locs => locs.Any(l => l.Language == Language.Fa))
            .WithMessage("ترجمه‌ی فارسی نظرسنجی الزامی است.")
            .Must(locs => locs.Select(l => l.Language).Distinct().Count() == locs.Count)
            .WithMessage("هر زبان فقط یک ترجمه می‌تواند داشته باشد.");

        RuleForEach(x => x.Localizations).SetValidator(new SurveyLocalizationDtoValidator());

        // تاریخ شروع باید قبل از تاریخ پایان باشد (در صورت تنظیم هر دو).
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("تاریخ پایان نظرسنجی باید بعد از تاریخ شروع باشد.");

        RuleFor(x => x.EstimatedMinutes)
            .InclusiveBetween(1, 480).WithMessage("مدت زمان تخمینی باید بین ۱ و ۴۸۰ دقیقه باشد.");
    }
}

public sealed class SurveyLocalizationDtoValidator : AbstractValidator<SurveyLocalizationDto>
{
    public SurveyLocalizationDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان نظرسنجی الزامی است.")
            .MaximumLength(256).WithMessage("عنوان نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2048).WithMessage("توضیح نمی‌تواند بیش از ۲۰۴۸ کاراکتر باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.WelcomeMessage)
            .MaximumLength(2048).WithMessage("پیام خوش‌آمدگویی نمی‌تواند بیش از ۲۰۴۸ کاراکتر باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.WelcomeMessage));

        RuleFor(x => x.ThankYouMessage)
            .MaximumLength(2048).WithMessage("پیام تشکر نمی‌تواند بیش از ۲۰۴۸ کاراکتر باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.ThankYouMessage));
    }
}

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش قالب نظرسنجی.
/// </summary>
public sealed class SaveSurveyTemplateRequestValidator : AbstractValidator<SaveSurveyTemplateRequest>
{
    public SaveSurveyTemplateRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد قالب الزامی است.")
            .MaximumLength(64).WithMessage("کد قالب نمی‌تواند بیش از ۶۴ کاراکتر باشد.")
            .Matches("^[A-Za-z0-9\\-_.]+$").WithMessage("کد قالب فقط می‌تواند شامل حروف انگلیسی، رقم، خط تیره و زیرخط باشد.");

        RuleFor(x => x.QuestionnaireId)
            .NotEmpty().WithMessage("هر قالب باید به یک پرسشنامه ارجاع دهد.");

        RuleFor(x => x.Localizations)
            .NotEmpty().WithMessage("حداقل یک ترجمه برای قالب الزامی است.")
            .Must(locs => locs.Any(l => l.Language == Language.Fa))
            .WithMessage("ترجمه‌ی فارسی قالب الزامی است.")
            .Must(locs => locs.Select(l => l.Language).Distinct().Count() == locs.Count)
            .WithMessage("هر زبان فقط یک ترجمه می‌تواند داشته باشد.");

        RuleForEach(x => x.Localizations).SetValidator(new SurveyTemplateLocalizationDtoValidator());

        RuleFor(x => x.EstimatedMinutes)
            .InclusiveBetween(1, 480).WithMessage("مدت زمان تخمینی باید بین ۱ و ۴۸۰ دقیقه باشد.");
    }
}

public sealed class SurveyTemplateLocalizationDtoValidator : AbstractValidator<SurveyTemplateLocalizationDto>
{
    public SurveyTemplateLocalizationDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان قالب الزامی است.")
            .MaximumLength(256).WithMessage("عنوان قالب نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2048).WithMessage("توضیح قالب نمی‌تواند بیش از ۲۰۴۸ کاراکتر باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

/// <summary>
/// اعتبارسنجی درخواست ساخت نظرسنجی از قالب.
/// </summary>
public sealed class CreateSurveyFromTemplateRequestValidator : AbstractValidator<CreateSurveyFromTemplateRequest>
{
    public CreateSurveyFromTemplateRequestValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("شناسه‌ی قالب الزامی است.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد نظرسنجی جدید الزامی است.")
            .MaximumLength(64).WithMessage("کد نظرسنجی نمی‌تواند بیش از ۶۴ کاراکتر باشد.")
            .Matches("^[A-Za-z0-9\\-_.]+$").WithMessage("کد نظرسنجی فقط می‌تواند شامل حروف انگلیسی، رقم، خط تیره و زیرخط باشد.");

        RuleForEach(x => x.Localizations!)
            .SetValidator(new SurveyLocalizationDtoValidator())
            .When(x => x.Localizations is { Count: > 0 });

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("تاریخ پایان نظرسنجی باید بعد از تاریخ شروع باشد.");
    }
}

/// <summary>
/// اعتبارسنجی فیلتر جستجوی نظرسنجی‌ها.
/// </summary>
public sealed class SurveySearchRequestValidator : AbstractValidator<SurveySearchRequest>
{
    public SurveySearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("اندازه‌ی صفحه باید بین ۱ و ۱۰۰ باشد.");
    }
}

/// <summary>
/// اعتبارسنجی فیلتر جستجوی قالب‌های نظرسنجی.
/// </summary>
public sealed class SurveyTemplateSearchRequestValidator : AbstractValidator<SurveyTemplateSearchRequest>
{
    public SurveyTemplateSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("اندازه‌ی صفحه باید بین ۱ و ۱۰۰ باشد.");
    }
}
