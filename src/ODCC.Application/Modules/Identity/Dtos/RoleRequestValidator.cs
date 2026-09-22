using FluentValidation;
using ODCC.Application.Authorization;

namespace ODCC.Application.Modules.Identity.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش نقش.
/// </summary>
public sealed class SaveRoleRequestValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام نقش الزامی است.")
            .MaximumLength(128).WithMessage("نام نقش نمی‌تواند بیش از ۱۲۸ کاراکتر باشد.");

        RuleFor(x => x.Permissions)
            .Must(BeValidPermissions).WithMessage("یک یا چند مجوز نامعتبر هستند.");
    }

    /// <summary>همه‌ی مجوزهای ارسالی باید در کاتالوگ موجود باشند.</summary>
    private static bool BeValidPermissions(IReadOnlyCollection<string> permissions) =>
        permissions.Count == 0 || permissions.All(p => Permissions.All.Contains(p, StringComparer.Ordinal));
}
