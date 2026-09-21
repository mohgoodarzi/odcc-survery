using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;

namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// پیاده‌ی <see cref="IUnitOfWork"/> برای یک DbContext مشخص.
/// هر ماژول نمونه‌ی خود را با context خود ثبت می‌کند تا مرز تراکنش ماژول حفظ شود.
/// </summary>
public sealed class UnitOfWork<TContext>(TContext context) : IUnitOfWork where TContext : DbContext
{
    private readonly TContext _context = context;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // مهر زمان به‌روزرسانی روی موجودیت‌های تغییر یافته.
        foreach (var entry in _context.ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        return await _context.SaveChangesAsync(ct);
    }
}
