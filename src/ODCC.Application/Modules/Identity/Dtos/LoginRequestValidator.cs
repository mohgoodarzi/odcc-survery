using FluentValidation;

namespace ODCC.Application.Modules.Identity.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ورود با پیام‌های فارسی.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("نام کاربری الزامی است.")
            .MaximumLength(256).WithMessage("نام کاربری نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("رمز عبور الزامی است.")
            .MaximumLength(128).WithMessage("رمز عبور نمی‌تواند بیش از ۱۲۸ کاراکتر باشد.");
    }
}

/// <summary>
/// اعتبارسنجی درخواست تازه‌سازی توکن.
/// </summary>
public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("توکن دسترسی الزامی است.");

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("توکن تازه‌سازی الزامی است.");
    }
}
