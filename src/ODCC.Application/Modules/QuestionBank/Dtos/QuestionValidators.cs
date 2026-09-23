using FluentValidation;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;

namespace ODCC.Application.Modules.QuestionBank.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش سؤال کتابخانه.
/// </summary>
public sealed class SaveQuestionRequestValidator : AbstractValidator<SaveQuestionRequest>
{
    public SaveQuestionRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد سؤال الزامی است.")
            .MaximumLength(64).WithMessage("کد سؤال نمی‌تواند بیش از ۶۴ کاراکتر باشد.")
            .Matches("^[A-Za-z0-9\\-_.]+$").WithMessage("کد سؤال فقط می‌تواند شامل حروف انگلیسی، رقم، خط تیره و زیرخط باشد.");

        RuleFor(x => x.ScaleMax)
            .InclusiveBetween(2, 20).WithMessage("حداکثر طیف باید بین ۲ و ۲۰ باشد.");

        RuleFor(x => x.Localizations)
            .NotEmpty().WithMessage("حداقل یک ترجمه برای سؤال الزامی است.");

        RuleForEach(x => x.Localizations).SetValidator(new QuestionLocalizationDtoValidator());

        RuleFor(x => x.Localizations)
            .Must(locs => locs.Any(l => l.Language == Language.Fa))
            .WithMessage("ترجمه‌ی فارسی سؤال الزامی است.")
            .Must(locs => locs.Select(l => l.Language).Distinct().Count() == locs.Count)
            .WithMessage("هر زبان فقط یک ترجمه می‌تواند داشته باشد.");

        // سؤال‌های گزینه‌ای باید حداقل دو گزینه داشته باشند؛ سایر انواع نباید گزینه داشته باشند.
        RuleFor(x => x.Options)
            .Must((request, options) => !request.Type.HasOptions() || options.Count >= 2)
            .When(x => x.Type.HasOptions())
            .WithMessage("سؤال گزینه‌ای باید حداقل دو گزینه داشته باشد.");

        RuleFor(x => x.Options)
            .Empty()
            .When(x => !x.Type.HasOptions())
            .WithMessage("فقط سؤال‌های تک‌انتخابی و چندانتخابی می‌توانند گزینه داشته باشند.");

        RuleForEach(x => x.Options).SetValidator(new SaveQuestionOptionRequestValidator());
    }
}

public sealed class QuestionLocalizationDtoValidator : AbstractValidator<QuestionLocalizationDto>
{
    public QuestionLocalizationDtoValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("متن سؤال الزامی است.")
            .MaximumLength(1024).WithMessage("متن سؤال نمی‌تواند بیش از ۱۰۲۴ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2048).WithMessage("توضیح نمی‌تواند بیش از ۲۰۴۸ کاراکتر باشد.");
    }
}

public sealed class SaveQuestionOptionRequestValidator : AbstractValidator<SaveQuestionOptionRequest>
{
    public SaveQuestionOptionRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد گزینه الزامی است.")
            .MaximumLength(32).WithMessage("کد گزینه نمی‌تواند بیش از ۳۲ کاراکتر باشد.");

        RuleFor(x => x.Localizations)
            .NotEmpty().WithMessage("هر گزینه باید حداقل یک ترجمه داشته باشد.");

        RuleForEach(x => x.Localizations).SetValidator(new QuestionOptionLocalizationDtoValidator());
    }
}

public sealed class QuestionOptionLocalizationDtoValidator : AbstractValidator<QuestionOptionLocalizationDto>
{
    public QuestionOptionLocalizationDtoValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("متن گزینه الزامی است.")
            .MaximumLength(512).WithMessage("متن گزینه نمی‌تواند بیش از ۵۱۲ کاراکتر باشد.");
    }
}

/// <summary>
/// اعتبارسنجی فیلتر جستجوی سؤال‌ها.
/// </summary>
public sealed class QuestionSearchRequestValidator : AbstractValidator<QuestionSearchRequest>
{
    public QuestionSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("اندازه‌ی صفحه باید بین ۱ و ۱۰۰ باشد.");
    }
}
