namespace ODCC.Application.Modules.Identity.Abstractions;

/// <summary>
/// جستجوی اطلاعات پایه‌ی کاربران برای ماژول‌های دیگر.
///
/// این قرارداد تنها مسیر مجاز خواندن داده‌ی کاربران توسط سایر ماژول‌هاست
/// (مثلاً ماژول سازمان برای نمایش نام کاربریِ مرتبط با یک کارمند).
/// ماژول‌ها هرگز نباید جداول هویت را مستقیماً پرس‌وجو کنند.
/// </summary>
public interface IUserLookupService
{
    /// <summary>نام نمایشی یک کاربر یا <c>null</c>.</summary>
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct = default);

    /// <summary>نام کاربری یک کاربر یا <c>null</c>.</summary>
    Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct = default);
}
