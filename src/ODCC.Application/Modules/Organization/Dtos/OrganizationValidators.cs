using FluentValidation;

namespace ODCC.Application.Modules.Organization.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش واحد سازمانی.
/// </summary>
public sealed class SaveOrgUnitRequestValidator : AbstractValidator<SaveOrgUnitRequest>
{
    public SaveOrgUnitRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد واحد سازمانی الزامی است.")
            .MaximumLength(64).WithMessage("کد واحد نمی‌تواند بیش از ۶۴ کاراکتر باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام واحد سازمانی الزامی است.")
            .MaximumLength(256).WithMessage("نام واحد نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        // واحد ریشه نباید والد داشته باشد؛ واحدهای غیرریشه باید والد داشته باشند.
        RuleFor(x => x.ParentId)
            .Null().When(x => x.Type == ODCC.Domain.Modules.Organization.Enums.OrgUnitType.Company,
                ApplyConditionTo.CurrentValidator)
            .WithMessage("واحد شرکت نباید والد داشته باشد.");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("تاریخ پایان باید بعد از تاریخ شروع باشد.");
    }
}

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش موقعیت شغلی.
/// </summary>
public sealed class SavePositionRequestValidator : AbstractValidator<SavePositionRequest>
{
    public SavePositionRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد موقعیت الزامی است.")
            .MaximumLength(64).WithMessage("کد موقعیت نمی‌تواند بیش از ۶۴ کاراکتر باشد.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان موقعیت الزامی است.")
            .MaximumLength(256).WithMessage("عنوان موقعیت نمی‌تواند بیش از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.OrgUnitId)
            .NotEmpty().WithMessage("هر موقعیت باید به یک واحد سازمانی تعلق داشته باشد.");

        // یک موقعیت نمی‌تواند به خودش گزارش دهد.
        RuleFor(x => x.ReportsToPositionId)
            .NotEqual(x => x.OrgUnitId)
            .When(x => x.ReportsToPositionId.HasValue)
            .WithMessage("ساختار گزارش‌دهی نمی‌تواند چرخه ایجاد کند.");
    }
}

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش کارمند.
/// </summary>
public sealed class SaveEmployeeRequestValidator : AbstractValidator<SaveEmployeeRequest>
{
    public SaveEmployeeRequestValidator()
    {
        RuleFor(x => x.EmployeeCode)
            .NotEmpty().WithMessage("کد پرسنلی الزامی است.")
            .MaximumLength(32).WithMessage("کد پرسنلی نمی‌تواند بیش از ۳۲ کاراکتر باشد.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("نام الزامی است.")
            .MaximumLength(128).WithMessage("نام نمی‌تواند بیش از ۱۲۸ کاراکتر باشد.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("نام خانوادگی الزامی است.")
            .MaximumLength(128).WithMessage("نام خانوادگی نمی‌تواند بیش از ۱۲۸ کاراکتر باشد.");

        RuleFor(x => x.NationalCode)
            .Matches("^[0-9]{10}$")
            .When(x => !string.IsNullOrWhiteSpace(x.NationalCode))
            .WithMessage("کد ملی باید دقیقاً ۱۰ رقم باشد.");

        RuleFor(x => x.OrgUnitId)
            .NotEmpty().WithMessage("هر کارمند باید به یک واحد سازمانی تعلق داشته باشد.");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("تاریخ پایان اشتغال باید بعد از تاریخ شروع باشد.");

        RuleFor(x => x.WorkEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.WorkEmail))
            .WithMessage("فرمت ایمیل کاری نامعتبر است.");
    }
}
