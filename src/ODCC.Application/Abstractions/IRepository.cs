using ODCC.Domain.Common;

namespace ODCC.Application.Abstractions;

/// <summary>
/// قرارداد عمومی مخزن برای خواندن و نوشتن موجودیت‌ها.
/// پیاده‌سازی آن (<c>Repository&lt;T&gt;</c>) فیلتر حذف نرم را به‌صورت خودکار اعمال می‌کند
/// تا ردیف‌های حذف‌شده هرگز در نتایج ظاهر نشوند.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    /// <summary>علامت‌گذاری موجودیت به‌عنوان حذف‌شده (Soft delete).</summary>
    void Remove(T entity);

    void Update(T entity);
}
