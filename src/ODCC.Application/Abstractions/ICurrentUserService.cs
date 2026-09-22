namespace ODCC.Application.Abstractions;

/// <summary>
/// دسترسی به کاربر جاری در context درخواست جاری.
/// پیاده‌سازی آن در لایه‌ی زیرساخت، کاربر را از کلیم‌های توکن JWT استخراج می‌کند.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>شناسه‌ی کاربر یا <c>null</c> اگر درخواست ناشناس باشد.</summary>
    Guid? UserId { get; }

    /// <summary>نام نمایشی کاربر یا <c>null</c>.</summary>
    string? UserName { get; }

    /// <summary>نام و نام خانوادگی کاربر.</summary>
    string? DisplayName { get; }

    /// <summary>آیا کاربر احراز هویت شده است؟</summary>
    bool IsAuthenticated { get; }

    /// <summary>نقش‌های کاربر (خالی برای مهمان).</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>مجوزهای کاربر (از نقش‌هایش استخراج می‌شوند).</summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>واحد سازمانی کاربر (لنگر دامنه‌ی دسترسی).</summary>
    Guid? OrgUnitId { get; }

    /// <summary>دامنه‌ی دسترسی کاربر به داده‌های سازمانی.</summary>
    ODCC.Domain.Common.DataScope DataScope { get; }

    /// <summary>آیا کاربر حداقل یکی از نقش‌های داده‌شده را دارد؟</summary>
    bool IsInRole(params string[] roles);

    /// <summary>آیا کاربر مجوز داده‌شده را دارد؟</summary>
    bool HasPermission(string permission);

    /// <summary>آیا کاربر مجوز داده‌شده را دارد؟</summary>
    bool HasPermission(ODCC.Application.Authorization.Permissions.PermissionKey permission);

    /// <summary>آدرس IP درخواست‌کننده (برای ممیزی و محدودیت نرخ).</summary>
    string? ClientIpAddress { get; }
}
