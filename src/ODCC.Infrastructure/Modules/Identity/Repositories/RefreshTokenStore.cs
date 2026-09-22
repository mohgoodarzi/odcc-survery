using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Domain.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;

namespace ODCC.Infrastructure.Modules.Identity.Repositories;

/// <summary>
/// پیاده‌سازی مخزن توکن‌های تازه‌سازی.
/// فقط روی هش توکن‌ها پرس‌وجو می‌کند.
///
/// <b>مرز تراکنش:</b> <see cref="AddAsync"/> فقط موجودیت را به DbContext اضافه
/// می‌کند و ذخیره‌سازی را به <c>IIdentityUnitOfWork</c> واگذار می‌کند تا چرخش
/// توکن (ابطال توکن قدیمی + صدور توکن جدید) در یک تراکنش انجام شود.
/// عملیات دسته‌ای ابطال (<c>ExecuteUpdateAsync</c>) خودشان مستقیماً ذخیره می‌شوند
/// چون خارج از ChangeTracker کار می‌کنند و atomic هستند.
/// </summary>
public sealed class RefreshTokenStore(IdentityDbContext dbContext) : IRefreshTokenStore
{
    private readonly IdentityDbContext _dbContext = dbContext;

    /// <summary>
    /// جستجوی توکن با هش. فیلتر سراسری «ابطال‌نشده» عمداً نادیده گرفته می‌شود:
    /// تشخیص استفاده‌ی مجدد از توکن مصرف‌شده نیازمند یافتن همان توکن باطل‌شده است
    /// تا بتوانیم خانواده‌ی آن را باطل کنیم. در غیر این صورت توکن باطل‌شده
    /// «ناموجود» به نظر می‌رسد و زنجیره‌ی ابطال هرگز اجرا نمی‌شد.
    /// </summary>
    public Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        _dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    /// <summary>افزودن توکن به DbContext (بدون ذخیره — commit با UnitOfWork).</summary>
    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _dbContext.RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public async Task RevokeFamilyAsync(Guid familyId, string? revokedByIp, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await _dbContext.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedByIp, revokedByIp), ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string? revokedByIp, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedByIp, revokedByIp), ct);
    }

    public Task<int> CountActiveForUserAsync(Guid userId, CancellationToken ct = default) =>
        _dbContext.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct);
}
