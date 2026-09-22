using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Identity.Entities;

/// <summary>
/// توکن تازه‌سازی (Refresh Token).
///
/// امنیت:
/// - توکن به‌صورت <b>هش‌شده</b> (<see cref="TokenHash"/>) ذخیره می‌شود، هرگز به‌صورت متن‌وضوح.
/// - توکن‌ها در «خانواده» (<see cref="FamilyId"/>) دسته‌بندی می‌شوند. استفاده‌ی مجدد یک توکن
///   باطل‌شده باعث ابطال کل خانواده می‌شود (تشخیص سرقت توکن).
/// - هر توکن فقط یک بار قابل استفاده است (چرخش/Rotation).
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>شناسه‌ی کاربر مالک توکن.</summary>
    public Guid UserId { get; set; }

    /// <summary>هش توکن (SHA-256). هرگز توکن خام ذخیره نمی‌شود.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>خانواده‌ی توکن؛ همه‌ی توکن‌های یک زنجیره‌ی چرخش یک خانواده را دارند.</summary>
    public Guid FamilyId { get; set; } = Guid.CreateVersion7();

    /// <summary>زمان انقضای توکن بر حسب UTC.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>زمان ابطال توکن. <c>null</c> یعنی توکن هنوز معتبر است.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>هش توکنی که جایگزین این توکن شده است (برای زنجیره‌ی چرخش).</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>اطلاعات دستگاه/مرورگر برای ردیابی نشست.</summary>
    public string? DeviceInfo { get; set; }

    /// <summary>آدرس IP ایجادکننده‌ی توکن.</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>آدرس IP که توکن را باطل کرده است.</summary>
    public string? RevokedByIp { get; set; }

    /// <summary>آیا توکن ابطال شده است؟</summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>آیا توکن منقضی شده است؟</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>آیا توکن قابل استفاده است؟ (ابطال نشده و منقضی نشده)</summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    /// <summary>ابطال توکن.</summary>
    public void Revoke(string? revokedByIp = null, string? replacedByTokenHash = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
