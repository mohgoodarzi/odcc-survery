using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Identity.Abstractions;

/// <summary>
/// سرویس مدیریت کاربران.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// جستجوی صفحه‌بندی‌شده‌ی کاربران.
    /// </summary>
    /// <param name="request">فیلتر جستجو و صفحه‌بندی.</param>
    /// <param name="ct">توکن لغو عملیات.</param>
    Task<Result<PagedResult<UserSummaryDto>>> SearchAsync(UserSearchRequest request, CancellationToken ct = default);

    Task<Result<UserSummaryDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<UserSummaryDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<Result<UserSummaryDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    /// <summary>فعال یا غیرفعال کردن حساب کاربری.</summary>
    Task<Result> SetActiveAsync(SetUserActiveRequest request, CancellationToken ct = default);

    /// <summary>تغییر رمز عبور توسط خود کاربر.</summary>
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>بازنشانی رمز عبور توسط مدیر.</summary>
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    /// <summary>انتصاب نقش‌ها به کاربر.</summary>
    Task<Result> AssignRolesAsync(AssignRolesRequest request, CancellationToken ct = default);

    /// <summary>دریافت پروفایل کامل کاربر احراز هویت‌شده.</summary>
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// بارگذاری (یا تعویض) تصویر آواتار کاربر. نوع و اندازه‌ی تصویر
    /// اعتبارسنجی می‌شود و محتوا در انبار فایل‌ها (بیرون از پایگاه داده)
    /// ذخیره می‌شود؛ فقط مسیر آن در پایگاه داده می‌ماند.
    /// </summary>
    /// <param name="userId">شناسه‌ی کاربر.</param>
    /// <param name="content">محتوای فایل تصویر.</param>
    /// <param name="fileName">نام اصلی فایل (فقط برای استخراج پسوند استفاده می‌شود).</param>
    /// <param name="contentType">نوع محتوای اعلام‌شده توسط کلاینت.</param>
    /// <param name="ct">توکن لغو.</param>
    Task<Result<UserSummaryDto>> SetAvatarAsync(
        Guid userId, Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>حذف تصویر آواتار کاربر.</summary>
    Task<Result> RemoveAvatarAsync(Guid userId, CancellationToken ct = default);

    /// <summary>باز کردن تصویر آواتار کاربر برای خواندن.</summary>
    Task<Result<AvatarFileDto>> OpenAvatarAsync(Guid userId, CancellationToken ct = default);
}

