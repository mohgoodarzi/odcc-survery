namespace ODCC.Application.Abstractions;

/// <summary>
/// دسترسی به کاربر جاری در contexto درخواست جاری.
/// با احراز هویت JWT در فاز ۱ پیاده‌سازی می‌شود؛ تا آن زمان برای درخواست‌های ناشناس خالی است.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>شناسه‌ی کاربر یا <c>null</c> اگر درخواست ناشناس باشد.</summary>
    Guid? UserId { get; }

    /// <summary>نام نمایشی کاربر یا <c>null</c>.</summary>
    string? UserName { get; }

    /// <summary>آیا کاربر احراز هویت شده است؟</summary>
    bool IsAuthenticated { get; }

    /// <summary>نقش‌های کاربر (خالی برای مهمان).</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>آیا کاربر حداقل یکی از نقش‌های داده‌شده را دارد؟</summary>
    bool IsInRole(params string[] roles);

    /// <summary>آدرس IP درخواست‌کننده (برای ممیزی و محدودیت نرخ).</summary>
    string? ClientIpAddress { get; }
}
