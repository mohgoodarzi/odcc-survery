using Microsoft.AspNetCore.Identity;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Modules.Identity.Entities;

/// <summary>
/// مدل ماندگاری کاربر در ASP.NET Core Identity.
///
/// این کلاس در لایه‌ی زیرساخت قرار دارد چون به <see cref="IdentityUser{TKey}"/> وابسته است.
/// لایه‌ی کاربرد هرگز با این نوع مستقیم کار نمی‌کند؛ فقط با DTOها.
///
/// رمز عبور به‌صورت هش‌شده توسط Identity ذخیره می‌شود و هرگز به‌صورت متن‌وضوح
/// در پایگاه داده یا لاگها قرار نمی‌گیرد.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>نام.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>نام خانوادگی.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>نام کامل نمایشی.</summary>
    public string DisplayName => $"{FirstName} {LastName}".Trim();

    /// <summary>کد ملی (اختیاری).</summary>
    public string? NationalCode { get; set; }

    /// <summary>آدرس تصویر آواتار (اختیاری).</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>آیا حساب کاربری فعال است؟ حساب غیرفعال نمی‌تواند وارد شود.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>زمان غیرفعال‌سازی حساب.</summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>واحد سازمانی کاربر (لنگر دامنه‌ی دسترسی به داده).</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>دامنه‌ی دسترسی کاربر به داده‌های سازمانی.</summary>
    public DataScope DataScope { get; set; } = DataScope.Own;

    /// <summary>زمان ایجاد.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>زمان آخرین تغییر.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>حذف نرم.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>زمان آخرین ورود موفق.</summary>
    public DateTime? LastLoginAt { get; set; }
}
