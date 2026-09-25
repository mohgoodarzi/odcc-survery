using FluentValidation;

namespace ODCC.Application.Modules.Response.Dtos;

/// <summary>
/// اعتبارسنجی یک پاسخ در درخواست ذخیره/ارسال.
///
/// توجه: پاسخ‌های خالی در اینجا مجاز شمرده می‌شوند (کاربر ممکن است سؤالی را
/// رها کرده باشد). اعتبارسنجی سؤال‌های اجباری در سرویس و نسبت به ساختار
/// پرسشنامه انجام می‌شود، چون نیاز به دانستن نوع سؤال و گزینه‌های مجاز دارد.
/// </summary>
public sealed class SaveAnswerRequestValidator : AbstractValidator<SaveAnswerRequest>
{
    private const int MaxTextLength = 4000;

    public SaveAnswerRequestValidator()
    {
        RuleFor(x => x.QuestionnaireItemId)
            .NotEmpty().WithMessage("شناسه‌ی آیتم الزامی است.");

        RuleFor(x => x.TextValue)
            .MaximumLength(MaxTextLength).WithMessage($"پاسخ متنی نمی‌تواند بیش از {MaxTextLength} کاراکتر باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.TextValue));

        // فقط یک شکل پاسخ معنا دارد: ترکیب‌های متناقض (مثلاً متن + گزینه) رد می‌شوند.
        RuleFor(x => x)
            .Must(HaveSingleShape)
            .WithMessage("هر پاسخ فقط می‌تواند یکی از اشکال متن، عدد یا گزینه را داشته باشد.")
            .When(x => x.SelectedOptionIds is { Count: > 0 } || x.NumericValue.HasValue || !string.IsNullOrWhiteSpace(x.TextValue));
    }

    /// <summary>حداکثر یکی از اشکال پاسخ مقدار داشته باشد.</summary>
    private static bool HaveSingleShape(SaveAnswerRequest answer)
    {
        var shapes = 0;
        if (answer.SelectedOptionIds is { Count: > 0 }) shapes++;
        if (answer.NumericValue.HasValue) shapes++;
        if (!string.IsNullOrWhiteSpace(answer.TextValue)) shapes++;
        return shapes <= 1;
    }
}

/// <summary>
/// اعتبارسنجی درخواست ذخیره‌ی جزئی.
/// </summary>
public sealed class SaveAnswersRequestValidator : AbstractValidator<SaveAnswersRequest>
{
    private const int MaxAnswersPerRequest = 500;

    public SaveAnswersRequestValidator()
    {
        RuleFor(x => x.Answers)
            .Must(answers => answers is null || answers.Count <= MaxAnswersPerRequest)
            .WithMessage($"حداکثر {MaxAnswersPerRequest} پاسخ در هر درخواست قابل ارسال است.");

        RuleForEach(x => x.Answers)
            .SetValidator(new SaveAnswerRequestValidator())
            .When(x => x.Answers is { Count: > 0 });
    }
}

/// <summary>
/// اعتبارسنجی درخواست ارسال نهایی.
/// </summary>
public sealed class SubmitResponseRequestValidator : AbstractValidator<SubmitResponseRequest>
{
    public SubmitResponseRequestValidator()
    {
        RuleForEach(x => x.Answers)
            .SetValidator(new SaveAnswerRequestValidator())
            .When(x => x.Answers is { Count: > 0 });
    }
}

/// <summary>
/// اعتبارسنجی درخواست شروع نشست.
/// </summary>
public sealed class StartSessionRequestValidator : AbstractValidator<StartSessionRequest>
{
    public StartSessionRequestValidator()
    {
        RuleFor(x => x.SurveyId)
            .NotEmpty().WithMessage("شناسه‌ی نظرسنجی الزامی است.");

        RuleFor(x => x.ResponseLanguage)
            .IsInEnum().WithMessage("زبان پاسخ نامعتبر است.");

        RuleFor(x => x.Source)
            .IsInEnum().WithMessage("کانال دسترسی نامعتبر است.");
    }
}

/// <summary>
/// اعتبارسنجی فیلتر جستجوی نشست‌های پاسخ.
/// </summary>
public sealed class ResponseSearchRequestValidator : AbstractValidator<ResponseSearchRequest>
{
    public ResponseSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200).WithMessage("اندازه‌ی صفحه باید بین ۱ و ۲۰۰ باشد.");
    }
}
