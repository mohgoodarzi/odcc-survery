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
}
