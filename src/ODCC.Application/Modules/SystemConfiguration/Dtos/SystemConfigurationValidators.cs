using FluentValidation;
using ODCC.Domain.Modules.SystemConfiguration.Enums;

namespace ODCC.Application.Modules.SystemConfiguration.Dtos;

/// <summary>
/// اعتبارسنجی مقدار تنظیم بر اساس نوع آن. تضمین می‌کند که رشته‌ی ذخیره‌شده
/// قابل تفسیر است و مقدار غیرمجاز (مثلاً تاریخ نامعتبر) وارد نشود.
/// </summary>
public sealed class UpdateSettingRequestValidator : AbstractValidator<UpdateSettingRequest>
{
    public UpdateSettingRequestValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("مقدار تنظیم نمی‌تواند خالی باشد.")
            .MaximumLength(4000).WithMessage("مقدار تنظیم نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد.");
    }
}

public sealed class UpdateFeatureFlagRequestValidator : AbstractValidator<UpdateFeatureFlagRequest>
{
    public UpdateFeatureFlagRequestValidator()
    {
        RuleFor(x => x.Percentage)
            .InclusiveBetween(0, 100)
            .When(x => x.State == FeatureFlagState.Percentage)
            .WithMessage("درصد باید بین ۰ و ۱۰۰ باشد.");

        RuleFor(x => x.AllowedUserIds)
            .Must(list => list is null || list.All(id => !string.IsNullOrWhiteSpace(id)))
            .When(x => x.State == FeatureFlagState.AllowList)
            .WithMessage("کلیدهای کاربر مجاز نمی‌توانند خالی باشند.");

        RuleFor(x => x.ExpiresAt)
            .Must(expires => expires is null || expires > DateTime.UtcNow)
            .WithMessage("تاریخ انقضا باید در آینده باشد.");
    }
}

public sealed class UpdateSystemPolicyRequestValidator : AbstractValidator<UpdateSystemPolicyRequest>
{
    public UpdateSystemPolicyRequestValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("مقدار سیاست نمی‌تواند خالی باشد.")
            .MaximumLength(2000).WithMessage("مقدار سیاست نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد.");
    }
}

/// <summary>اعتبارسنجی ایجاد تنظیم.</summary>
public sealed class CreateSettingRequestValidator : AbstractValidator<CreateSettingRequest>
{
    public CreateSettingRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("کلید تنظیم نمی‌تواند خالی باشد.")
            .MaximumLength(256).WithMessage("کلید تنظیم نمی‌تواند بیشتر از ۲۵۶ کاراکتر باشد.")
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9._-]*$").WithMessage("کلید فقط می‌تواند شامل حروف، اعداد، نقطه، خط تیره و زیرخط باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام نمی‌تواند خالی باشد.")
            .MaximumLength(300).WithMessage("نام نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد.");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("مقدار نمی‌تواند خالی باشد.")
            .MaximumLength(4000).WithMessage("مقدار نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد.");

        RuleFor(x => x.DefaultValue)
            .MaximumLength(4000).When(x => x.DefaultValue is not null);

        RuleFor(x => x.Group)
            .MaximumLength(128).When(x => x.Group is not null);
    }
}

/// <summary>اعتبارسنجی ایجاد پرچم ویژگی.</summary>
public sealed class CreateFeatureFlagRequestValidator : AbstractValidator<CreateFeatureFlagRequest>
{
    public CreateFeatureFlagRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("کلید پرچم نمی‌تواند خالی باشد.")
            .MaximumLength(128).WithMessage("کلید پرچم نمی‌تواند بیشتر از ۱۲۸ کاراکتر باشد.")
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9._-]*$").WithMessage("کلید فقط می‌تواند شامل حروف، اعداد، نقطه، خط تیره و زیرخط باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام نمی‌تواند خالی باشد.")
            .MaximumLength(300).WithMessage("نام نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد.");

        RuleFor(x => x.Percentage)
            .InclusiveBetween(0, 100)
            .When(x => x.State == FeatureFlagState.Percentage)
            .WithMessage("درصد باید بین ۰ و ۱۰۰ باشد.");

        RuleFor(x => x.AllowedUserIds)
            .Must(list => list is null || list.All(id => !string.IsNullOrWhiteSpace(id)))
            .When(x => x.State == FeatureFlagState.AllowList)
            .WithMessage("کلیدهای کاربر مجاز نمی‌توانند خالی باشند.");

        RuleFor(x => x.ExpiresAt)
            .Must(expires => expires is null || expires > DateTime.UtcNow)
            .WithMessage("تاریخ انقضا باید در آینده باشد.");
    }
}

/// <summary>اعتبارسنجی ایجاد سیاست سیستمی.</summary>
public sealed class CreateSystemPolicyRequestValidator : AbstractValidator<CreateSystemPolicyRequest>
{
    public CreateSystemPolicyRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("کلید سیاست نمی‌تواند خالی باشد.")
            .MaximumLength(256).WithMessage("کلید سیاست نمی‌تواند بیشتر از ۲۵۶ کاراکتر باشد.")
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9._-]*$").WithMessage("کلید فقط می‌تواند شامل حروف، اعداد، نقطه، خط تیره و زیرخط باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام نمی‌تواند خالی باشد.")
            .MaximumLength(300).WithMessage("نام نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد.");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("مقدار نمی‌تواند خالی باشد.")
            .MaximumLength(2000).WithMessage("مقدار نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد.");

        RuleFor(x => x.DefaultValue)
            .MaximumLength(2000).When(x => x.DefaultValue is not null);
    }
}
