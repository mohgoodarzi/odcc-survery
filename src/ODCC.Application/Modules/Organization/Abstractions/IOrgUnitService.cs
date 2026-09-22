using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Organization.Abstractions;

/// <summary>
/// سرویس مدیریت واحدهای سازمانی و ساختار درختی.
/// </summary>
public interface IOrgUnitService
{
    Task<Result<IReadOnlyList<OrgUnitDto>>> ListAsync(bool activeOnly, CancellationToken ct = default);

    Task<Result<OrgUnitDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>دریافت کل درخت سازمانی به‌صورت سلسله‌مراتبی.</summary>
    Task<Result<IReadOnlyList<OrgUnitTreeDto>>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>کلیه زیرمجموعه‌های یک واحد (شامل خودش) — برای محدوده‌سازی داده.</summary>
    Task<Result<IReadOnlyList<Guid>>> GetDescendantUnitIdsAsync(Guid rootId, CancellationToken ct = default);

    Task<Result<OrgUnitDto>> CreateAsync(SaveOrgUnitRequest request, CancellationToken ct = default);

    Task<Result<OrgUnitDto>> UpdateAsync(Guid id, SaveOrgUnitRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
