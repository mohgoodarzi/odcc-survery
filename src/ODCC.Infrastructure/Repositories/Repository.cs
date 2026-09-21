using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Repositories;

/// <summary>
/// پیاده‌سازی پایه‌ی <see cref="IRepository{T}"/> با EF Core.
/// فیلتر حذف نرم (QueryFilter) در <c>ConfigureBase</c> تعریف شده و به‌صورت خودکار اعمال می‌شود.
/// </summary>
public abstract class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected DbContext Context { get; }
    protected DbSet<T> Set { get; }

    protected Repository(DbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public virtual async Task<int> CountAsync(CancellationToken ct = default) =>
        await Set.CountAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    /// <summary>حذف نرم: فقط پرچم <c>IsDeleted</c> فعال می‌شود.</summary>
    public virtual void Remove(T entity) => entity.IsDeleted = true;

    public virtual void Update(T entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        Set.Update(entity);
    }
}
