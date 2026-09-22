namespace ODCC.Application.Modules.Identity.Dtos;

/// <summary>
/// خلاصه‌ی کاربر برای لیست‌ها.
/// </summary>
public sealed record UserSummaryDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
}

/// <summary>
/// درخواست ایجاد کاربر.
/// </summary>
public sealed record CreateUserRequest
{
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }

    /// <summary>کد ملی (اختیاری، دقیقاً ۱۰ رقم).</summary>
    public string? NationalCode { get; init; }

    /// <summary>تصویر آواتار کاربر (اختیاری).</summary>
    public string? AvatarUrl { get; init; }

    public Guid? OrgUnitId { get; init; }
    public ODCC.Domain.Common.DataScope DataScope { get; init; }
    public IReadOnlyCollection<Guid> RoleIds { get; init; } = [];

    /// <summary>آیا کاربر باید بلافاصله فعال شود؟</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// درخواست ویرایش کاربر.
/// </summary>
public sealed record UpdateUserRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public Guid? OrgUnitId { get; init; }
    public ODCC.Domain.Common.DataScope DataScope { get; init; }
}

/// <summary>
/// درخواست تغییر رمز عبور توسط خود کاربر (نیازمند رمز فعلی).
/// </summary>
public sealed record ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// درخواست بازنشانی رمز عبور توسط مدیر (بدون رمز فعلی).
/// </summary>
public sealed record ResetPasswordRequest
{
    public Guid UserId { get; init; }
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// درخواست انتصاب نقش‌ها به یک کاربر.
/// </summary>
public sealed record AssignRolesRequest
{
    public Guid UserId { get; init; }
    public IReadOnlyCollection<Guid> RoleIds { get; init; } = [];
}

/// <summary>
/// درخواست فعال/غیرفعال کردن حساب کاربری.
/// </summary>
public sealed record SetUserActiveRequest
{
    public Guid UserId { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// درخواست جستجوی کاربران.
/// </summary>
/// <param name="SearchText">متن جستجو در نام کاربری، ایمیل، نام و نام خانوادگی.</param>
/// <param name="IsActive">فیلتر وضعیت حساب (<c>null</c> یعنی همه).</param>
/// <param name="RoleId">فیلتر نقش (<c>null</c> یعنی همه).</param>
/// <param name="Page">شماره‌ی صفحه (یک‌پایه).</param>
/// <param name="PageSize">اندازه‌ی صفحه.</param>
public sealed record UserSearchRequest(
    string? SearchText,
    bool? IsActive = null,
    Guid? RoleId = null,
    int Page = 1,
    int PageSize = 20);
