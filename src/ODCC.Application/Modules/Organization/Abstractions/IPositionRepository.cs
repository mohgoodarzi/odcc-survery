using ODCC.Application.Abstractions;
using ODCC.Domain.Modules.Organization.Entities;

namespace ODCC.Application.Modules.Organization.Abstractions;

/// <summary>
/// مخزن اختصاصی موقعیت‌های شغلی.
/// </summary>
public interface IPositionRepository : IRepository<Position>
{
    Task<Position?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>تعداد موقعیت‌های فرزند (گوزارش‌دهنده به این موقعیت).</summary>
    Task<int> CountReportsToAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>تعداد کارمندان فعال در این موقعیت.</summary>
    Task<int> CountActiveEmployeesAsync(Guid positionId, CancellationToken ct = default);
}
