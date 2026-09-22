using ODCC.Application.Abstractions;
using ODCC.Domain.Modules.Organization.Entities;

namespace ODCC.Application.Modules.Organization.Abstractions;

/// <summary>
/// مخزن اختصاصی واحدهای سازمانی.
/// </summary>
public interface IOrgUnitRepository : IRepository<OrgUnit>
{
    Task<OrgUnit?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// واحد ریشه (شرکت) یعنی اولین واحد بدون والد. در سامانه فقط یک ریشه وجود دارد.
    /// </summary>
    Task<OrgUnit?> FindRootAsync(CancellationToken ct = default);

    /// <summary>
    /// مسیر مادی یک واحد (برای ساخت مسیر فرزند).
    /// </summary>
    Task<string?> GetPathAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// تمام واحدهای زیردرخت یک ریشه (شامل خود ریشه) بر اساس پیشوند مسیر.
    /// </summary>
    Task<IReadOnlyList<OrgUnit>> GetDescendantsAsync(Guid rootId, CancellationToken ct = default);

    /// <summary>تعداد فرزندان مستقیم یک واحد (برای جلوگیری از حذف واحد با زیرمجموعه).</summary>
    Task<int> CountChildrenAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>تعداد کارمندان مستقیم یک واحد.</summary>
    Task<int> CountEmployeesAsync(Guid orgUnitId, CancellationToken ct = default);
}
