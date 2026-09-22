using ODCC.Domain.Modules.Identity.Entities;

namespace ODCC.Application.Modules.Identity.Abstractions;

/// <summary>
/// مخزن توکن‌های تازه‌سازی.
/// فقط با هش توکن‌ها کار می‌کند؛ توکن خام هرگز ذخیره یا جستجو نمی‌شود.
/// </summary>
public interface IRefreshTokenStore
{
    Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>ابطال تمام توکن‌های فعال یک خانواده (تشخیص سرقت توکن).</summary>
    Task RevokeFamilyAsync(Guid familyId, string? revokedByIp, CancellationToken ct = default);

    /// <summary>ابطال تمام نشست‌های فعال یک کاربر.</summary>
    Task RevokeAllForUserAsync(Guid userId, string? revokedByIp, CancellationToken ct = default);

    /// <summary>تعداد توکن‌های فعال یک کاربر (برای گزارش نشست‌ها).</summary>
    Task<int> CountActiveForUserAsync(Guid userId, CancellationToken ct = default);
}
