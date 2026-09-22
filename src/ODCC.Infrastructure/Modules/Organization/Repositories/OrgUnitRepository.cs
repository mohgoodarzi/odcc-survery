using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Infrastructure.Modules.Organization.Persistence;

namespace ODCC.Infrastructure.Modules.Organization.Repositories;

/// <summary>
/// پیاده‌سازی مخزن واحدهای سازمانی.
/// </summary>
public sealed class OrgUnitRepository(OrganizationDbContext dbContext) : IOrgUnitRepository
{
    private readonly OrganizationDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<OrgUnit>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.OrgUnits.AsNoTracking().OrderBy(u => u.Path).ToListAsync(ct);

    public async Task<OrgUnit?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.OrgUnits.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.OrgUnits.CountAsync(ct);

    public async Task AddAsync(OrgUnit entity, CancellationToken ct = default) =>
        await _dbContext.OrgUnits.AddAsync(entity, ct);

    public void Remove(OrgUnit entity) => entity.IsDeleted = true;

    public void Update(OrgUnit entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.OrgUnits.Update(entity);
    }

    public Task<OrgUnit?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _dbContext.OrgUnits.FirstOrDefaultAsync(u => u.Code == code, ct);

    public Task<OrgUnit?> FindRootAsync(CancellationToken ct = default) =>
        _dbContext.OrgUnits.FirstOrDefaultAsync(u => u.ParentId == null, ct);

    public async Task<string?> GetPathAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.OrgUnits
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => (string?)u.Path)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OrgUnit>> GetDescendantsAsync(Guid rootId, CancellationToken ct = default)
    {
        var rootPath = await GetPathAsync(rootId, ct);
        if (rootPath is null)
        {
            return [];
        }

        return await _dbContext.OrgUnits
            .AsNoTracking()
            .Where(u => u.Path == rootPath || u.Path.StartsWith(rootPath + "/"))
            .OrderBy(u => u.Path)
            .ToListAsync(ct);
    }

    public Task<int> CountChildrenAsync(Guid parentId, CancellationToken ct = default) =>
        _dbContext.OrgUnits.CountAsync(u => u.ParentId == parentId, ct);

    public Task<int> CountEmployeesAsync(Guid orgUnitId, CancellationToken ct = default) =>
        _dbContext.Employees.CountAsync(e => e.OrgUnitId == orgUnitId && e.Status != ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.Terminated, ct);
}
