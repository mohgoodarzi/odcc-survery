using Microsoft.AspNetCore.Identity;

namespace ODCC.Infrastructure.Modules.Identity.Entities;

/// <summary>
/// مدل ماندگاری نقش در ASP.NET Core Identity.
/// مجوزها به‌صورت کلیم با نوع <c>permission</c> روی نقش ذخیره می‌شوند
/// (از جدول <c>RoleClaims</c>) تا نیازی به جدول مجزا نباشد.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>نام نمایشی فارسی نقش.</summary>
    public string? DisplayName { get; set; }

    /// <summary>توضیحات نقش.</summary>
    public string? Description { get; set; }

    /// <summary>آیا این یک نقش سیستمی است (غیرقابل حذف)؟</summary>
    public bool IsSystem { get; set; }

    /// <summary>حذف نرم.</summary>
    public bool IsDeleted { get; set; }
}
