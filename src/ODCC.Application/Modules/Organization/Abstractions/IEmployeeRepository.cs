using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Modules.Organization.Entities;

namespace ODCC.Application.Modules.Organization.Abstractions;

/// <summary>
/// مخزن اختصاصی کارمندان.
/// </summary>
public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> FindByEmployeeCodeAsync(string code, CancellationToken ct = default);

    Task<Employee?> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// جستجوی صفحه‌بندی‌شده با محدودسازی به دامنه‌ی سازمانی قابل‌مشاهده.
    /// </summary>
    Task<IReadOnlyList<Employee>> SearchAsync(EmployeeSearchRequest request, string? pathPrefix, CancellationToken ct = default);

    Task<int> CountByOrgUnitAsync(Guid orgUnitId, CancellationToken ct = default);

    /// <summary>
    /// چند کارمند با شناسه (برای جمعیت هدف صریح در کمپین‌ها).
    /// </summary>
    Task<IReadOnlyList<Employee>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// تمام کارمندان شاغل (فعال یا مرخصی) — جمعیت هدف «تمام شرکت» در کمپین‌ها.
    /// </summary>
    Task<IReadOnlyList<Employee>> GetEmployedAsync(CancellationToken ct = default);

    /// <summary>
    /// کارمندان چند واحد سازمانی، با یا بدون زیرمجموعه‌ها — جمعیت هدف کمپین‌ها.
    /// </summary>
    /// <param name="orgUnitIds">شناسه‌ی واحدهای هدف.</param>
    /// <param name="includeDescendants">آیا زیرمجموعه‌های این واحدها هم شامل شوند؟</param>
    /// <param name="employedOnly">آیا فقط کارمندان شاغل بازگردانده شوند؟</param>
    /// <param name="ct">توکن لغو.</param>
    Task<IReadOnlyList<Employee>> GetByOrgUnitsAsync(
        IReadOnlyCollection<Guid> orgUnitIds,
        bool includeDescendants,
        bool employedOnly,
        CancellationToken ct = default);
}
