using FluentValidation;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;

namespace ODCC.Application.Modules.Campaign.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش کمپین.
/// </summary>
public sealed class SaveCampaignRequestValidator : AbstractValidator<SaveCampaignRequest>
{
    public SaveCampaignRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد کمپین الزامی است.")
            .MaximumLength(64).WithMessage("کد کمپین نمی‌تواند بیش از ۶۴ کاراکتر باشد.")
            .Matches("^[A-Za-z0-9\\-_.]+$").WithMessage("کد کمپین فقط می‌تواند شامل حروف انگلیسی، رقم، خط تیره و زیرخط باشد.");

        RuleFor(x => x.SurveyId)
            .NotEmpty().WithMessage("هر کمپین باید به یک نظرسنجی ارجاع دهد.");

        RuleFor(x => x.AudienceType)
            .IsInEnum().WithMessage("نوع جمعیت هدف نامعتبر است.");

        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage("کانال توزیع نامعتبر است.");

        RuleFor(x => x.Localizations)
            .NotEmpty().WithMessage("حداقل یک ترجمه برای کمپین الزامی است.")
            .Must(locs => locs.Any(l => l.Language == Language.Fa))
            .WithMessage("ترجمه‌ی فارسی کمپین الزامی است.")
            .Must(locs => locs.Select(l => l.Language).Distinct().Count() == locs.Count)
            .WithMessage("هر زبان فقط یک ترجمه می‌تواند داشته باشد.");

        RuleForEach(x => x.Localizations).SetValidator(new CampaignLocalizationDtoValidator());
        RuleForEach(x => x.Reminders).SetValidator(new SaveReminderRequestValidator());

        // جمعیت هدف باید با نوع آن همخوانی داشته باشد.
        RuleFor(x => x)
            .Must(x => x.AudienceType != TargetAudienceType.OrgUnits || x.TargetOrgUnitIds.Count > 0)
            .WithMessage("برای جمعیت هدف «واحدهای سازمانی» باید حداقل یک واحد مشخص کنید.")
            .Must(x => x.AudienceType != TargetAudienceType.Employees || x.TargetEmployeeIds.Count > 0)
            .WithMessage("برای جمعیت هدف «کارمندان مشخص» باید حداقل یک کارمند مشخص کنید.");

        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.ScheduledAt)
            .When(x => x.ScheduledAt.HasValue && x.EndsAt.HasValue)
            .WithMessage("مهلت نهایی پاسخ‌گویی باید بعد از زمان شروع کمپین باشد.");
    }
}

public sealed class CampaignLocalizationDtoValidator : AbstractValidator<CampaignLocalizationDto>
{
    public CampaignLocalizationDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان کمپین الزامی است.")
            .MaximumLength(256).WithMessage("عنوان کمپین نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2048).WithMessage("توضیح کمپین نمی‌تواند بیش از ۲۰۴۸ کاراکتر باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class SaveReminderRequestValidator : AbstractValidator<SaveReminderRequest>
{
    public SaveReminderRequestValidator()
    {
        RuleFor(x => x.SendAt)
            .NotEmpty().WithMessage("زمان ارسال یادآور الزامی است.")
            .GreaterThan(DateTime.MinValue).WithMessage("زمان ارسال یادآور نامعتبر است.");

        RuleFor(x => x.Localizations)
            .NotEmpty().WithMessage("هر یادآور باید حداقل یک ترجمه داشته باشد.")
            .Must(locs => locs.Any(l => l.Language == Language.Fa))
            .WithMessage("ترجمه‌ی فارسی یادآور الزامی است.")
            .Must(locs => locs.Select(l => l.Language).Distinct().Count() == locs.Count)
            .WithMessage("هر زبان فقط یک ترجمه می‌تواند داشته باشد.");

        RuleForEach(x => x.Localizations).SetValidator(new ReminderLocalizationDtoValidator());
    }
}

public sealed class ReminderLocalizationDtoValidator : AbstractValidator<ReminderLocalizationDto>
{
    public ReminderLocalizationDtoValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("موضوع یادآور الزامی است.")
            .MaximumLength(256).WithMessage("موضوع یادآور نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.Body)
            .MaximumLength(2048).WithMessage("متن یادآور نمی‌تواند بیش از ۲۰۴۸ کاراکتر باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.Body));
    }
}

/// <summary>
/// اعتبارسنجی فیلتر جستجوی کمپین‌ها.
/// </summary>
public sealed class CampaignSearchRequestValidator : AbstractValidator<CampaignSearchRequest>
{
    public CampaignSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("اندازه‌ی صفحه باید بین ۱ و ۱۰۰ باشد.");
    }
}

/// <summary>
/// اعتبارسنجی فیلتر جستجوی ردیف‌های توزیع.
/// </summary>
public sealed class DistributionSearchRequestValidator : AbstractValidator<DistributionSearchRequest>
{
    public DistributionSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200).WithMessage("اندازه‌ی صفحه باید بین ۱ و ۲۰۰ باشد.");
    }
}
