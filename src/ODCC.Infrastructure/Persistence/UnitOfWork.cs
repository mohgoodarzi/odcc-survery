using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Infrastructure.Modules.Identity.Persistence;
using ODCC.Infrastructure.Modules.Organization.Persistence;

namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// پیاده‌سازی <see cref="IUnitOfWork"/> برای یک DbContext مشخص.
/// هر ماژول نمونه‌ی خود را با context خود ثبت می‌کند تا مرز تراکنش ماژول حفظ شود.
/// </summary>
public class UnitOfWork<TContext>(TContext context, IDomainEventDispatcher domainEventDispatcher) : IUnitOfWork where TContext : DbContext
{
    private readonly TContext _context = context;
    private readonly IDomainEventDispatcher _domainEventDispatcher = domainEventDispatcher;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // مهر زمان به‌روزرسانی روی موجودیت‌های تغییر یافته.
        foreach (var entry in _context.ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        var result = await _context.SaveChangesAsync(ct);

        // پس از commit موفق، رویدادهای دامنه تحویل داده می‌شوند. اگر تحویل
        // قبل از commit انجام می‌شد، شنونده‌ای که دیتابیس می‌خواند داده‌ی
        // هنوز ذخیره‌نشده می‌دید. ترتیب: اول commit، بعد انتشار.
        await DispatchDomainEventsAsync(ct);

        return result;
    }

    /// <summary>
    /// تحویل رویدادهای دامنه‌ی موجودیت‌های ردیابی‌شده و سپس پاکسازی آن‌ها.
    /// خطای یک شنونده مانع پاکسازی و ادامه‌ی کار نمی‌شود (خطا لاگ می‌شود).
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        var entities = _context.ChangeTracker.Entries<Domain.Common.BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (entities.Count == 0)
        {
            return;
        }

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                await _domainEventDispatcher.DispatchAsync(domainEvent, ct);
            }

            entity.ClearDomainEvents();
        }
    }
}

/// <summary>
/// مرز تراکنشی ماژول هویت.
/// </summary>
public sealed class IdentityUnitOfWork(IdentityDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<IdentityDbContext>(context, domainEventDispatcher), IIdentityUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول سازمان.
/// </summary>
public sealed class OrganizationUnitOfWork(OrganizationDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<OrganizationDbContext>(context, domainEventDispatcher), IOrganizationUnitOfWork;
