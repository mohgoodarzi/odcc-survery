using ODCC.Domain.Modules.Organization.Enums;

namespace ODCC.Application.Modules.Organization.Dtos;

/// <summary>
/// کارمند سازمان.
/// </summary>
public sealed record EmployeeDto
{
    public Guid Id { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string? NationalCode { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? FatherName { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public Guid OrgUnitId { get; init; }
    public string? OrgUnitName { get; init; }
    public Guid? PositionId { get; init; }
    public string? PositionTitle { get; init; }
    public Guid? ManagerId { get; init; }
    public string? ManagerFullName { get; init; }
    public EmployeeStatus Status { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? WorkEmail { get; init; }
    public string? InternalPhone { get; init; }
    public bool IsCurrentlyEmployed { get; init; }
}

/// <summary>
/// درخواست ایجاد یا ویرایش کارمند.
/// </summary>
public sealed record SaveEmployeeRequest
{
    public string EmployeeCode { get; init; } = string.Empty;
    public string? NationalCode { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? FatherName { get; init; }
    public Guid? UserId { get; init; }
    public Guid OrgUnitId { get; init; }
    public Guid? PositionId { get; init; }
    public Guid? ManagerId { get; init; }
    public EmployeeStatus Status { get; init; } = EmployeeStatus.Active;
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? WorkEmail { get; init; }
    public string? InternalPhone { get; init; }
}

/// <summary>
/// خلاصه‌ی کارمند برای لیست‌ها و جستجو.
/// </summary>
public sealed record EmployeeSummaryDto
{
    public Guid Id { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public Guid OrgUnitId { get; init; }
    public string? OrgUnitName { get; init; }
    public string? PositionTitle { get; init; }
    public string? ManagerFullName { get; init; }
    public EmployeeStatus Status { get; init; }
    public bool IsCurrentlyEmployed { get; init; }
}

/// <summary>
/// درخواست جستجوی کارمندان.
/// </summary>
/// <param name="SearchText">متن جستجو در نام و کد پرسنلی.</param>
/// <param name="OrgUnitId">فیلتر واحد سازمانی.</param>
/// <param name="IncludeDescendants">آیا زیردرخت واحد داده‌شده هم شامل شود؟</param>
/// <param name="Status">فیلتر وضعیت شغلی.</param>
/// <param name="Page">شماره‌ی صفحه (یک‌پایه).</param>
/// <param name="PageSize">اندازه‌ی صفحه.</param>
public sealed record EmployeeSearchRequest(
    string? SearchText,
    Guid? OrgUnitId,
    bool IncludeDescendants = false,
    EmployeeStatus? Status = null,
    int Page = 1,
    int PageSize = 20);
