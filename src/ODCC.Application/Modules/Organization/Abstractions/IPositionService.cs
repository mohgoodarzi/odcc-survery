using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Organization.Abstractions;

/// <summary>
/// سرویس مدیریت موقعیت‌های شغلی.
/// </summary>
public interface IPositionService
{
    Task<Result<IReadOnlyList<PositionDto>>> ListAsync(bool activeOnly, CancellationToken ct = default);

    Task<Result<PositionDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<PositionDto>> CreateAsync(SavePositionRequest request, CancellationToken ct = default);

    Task<Result<PositionDto>> UpdateAsync(Guid id, SavePositionRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
