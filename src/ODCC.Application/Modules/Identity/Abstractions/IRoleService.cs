using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Identity.Abstractions;

/// <summary>
/// سرویس مدیریت نقش‌ها و مجوزها.
/// </summary>
public interface IRoleService
{
    Task<Result<IReadOnlyList<RoleSummaryDto>>> ListAsync(CancellationToken ct = default);

    Task<Result<RoleSummaryDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<RoleSummaryDto>> CreateAsync(SaveRoleRequest request, CancellationToken ct = default);

    Task<Result<RoleSummaryDto>> UpdateAsync(Guid id, SaveRoleRequest request, CancellationToken ct = default);

    /// <summary>انتصاب مجوزها به یک نقش.</summary>
    Task<Result> AssignPermissionsAsync(AssignRolePermissionsRequest request, CancellationToken ct = default);
}
