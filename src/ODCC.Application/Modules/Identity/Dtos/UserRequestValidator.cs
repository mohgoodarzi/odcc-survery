using FluentValidation;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Identity.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ایجاد کاربر.
/// </summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("نام کاربری الزامی است.")
            .MinimumLength(3).WithMessage("نام کاربری باید حداقل ۳ کاراکتر باشد.")
            .MaximumLength(256).WithMessage("نام کاربری نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.")
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("نام کاربری فقط می‌تواند شامل حروف انگلیسی، عدد، نقطه، خط‌زیر و خط‌تیره باشد.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("ایمیل الزامی است.")
            .EmailAddress().WithMessage("فرمت ایمیل نامعتبر است.")
            .MaximumLength(256).WithMessage("ایمیل نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("رمز عبور الزامی است.")
            .MinimumLength(8).WithMessage("رمز عبور باید حداقل ۸ کاراکتر باشد.")
            .MaximumLength(128).WithMessage("رمز عبور نمی‌تواند بیش از ۱۲۸ کاراکتر باشد.")
            .Must(HaveMixedCase).WithMessage("رمز عبور باید شامل حروف بزرگ و کوچک باشد.")
            .Must(HaveDigit).WithMessage("رمز عبور باید شامل حداقل یک عدد باشد.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("نام الزامی است.")
            .MaximumLength(128).WithMessage("نام نمی‌تواند بیش از ۱۲۸ کاراکتر باشد.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("نام خانوادگی الزامی است.")
            .MaximumLength(128).WithMessage("نام خانوادگی نمی‌تواند بیش از ۱۲۸ کاراکتر باشد.");
    }

    private static bool HaveMixedCase(string password) =>
        password.Any(char.IsUpper) && password.Any(char.IsLower);

    private static bool HaveDigit(string password) => password.Any(char.IsDigit);
}

/// <summary>
/// اعتبارسنجی درخواست تغییر رمز عبور.
/// </summary>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("رمز عبور فعلی الزامی است.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("رمز عبور جدید الزامی است.")
            .MinimumLength(8).WithMessage("رمز عبور باید حداقل ۸ کاراکتر باشد.")
            .Must(HaveMixedCase).WithMessage("رمز عبور باید شامل حروف بزرگ و کوچک باشد.")
            .Must(HaveDigit).WithMessage("رمز عبور باید شامل حداقل یک عدد باشد.")
            .NotEqual(x => x.CurrentPassword).WithMessage("رمز عبور جدید نباید با رمز عبور فعلی یکسان باشد.");
    }

    private static bool HaveMixedCase(string password) =>
        password.Any(char.IsUpper) && password.Any(char.IsLower);

    private static bool HaveDigit(string password) => password.Any(char.IsDigit);
}
