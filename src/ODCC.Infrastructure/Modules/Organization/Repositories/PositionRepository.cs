using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Infrastructure.Modules.Organization.Persistence;

namespace ODCC.Infrastructure.Modules.Organization.Repositories;

/// <summary>
/// پیاده‌سازی مخزن موقعیت‌های شغلی.
/// </summary>
public sealed class PositionRepository(OrganizationDbContext dbContext) : IPositionRepository
{
    private readonly OrganizationDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<Position>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Positions.AsNoTracking().OrderBy(p => p.Title).ToListAsync(ct);

    public async Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Positions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Positions.CountAsync(ct);

    public async Task AddAsync(Position entity, CancellationToken ct = default) =>
        await _dbContext.Positions.AddAsync(entity, ct);

    public void Remove(Position entity) => entity.IsDeleted = true;

    public void Update(Position entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Positions.Update(entity);
    }

    public Task<Position?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _dbContext.Positions.FirstOrDefaultAsync(p => p.Code == code, ct);

    public Task<int> CountReportsToAsync(Guid positionId, CancellationToken ct = default) =>
        _dbContext.Positions.CountAsync(p => p.ReportsToPositionId == positionId, ct);

    public Task<int> CountActiveEmployeesAsync(Guid positionId, CancellationToken ct = default) =>
        _dbContext.Employees.CountAsync(e => e.PositionId == positionId && e.Status == ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.Active, ct);
}
