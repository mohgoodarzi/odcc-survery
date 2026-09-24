using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Domain.Modules.Organization.Enums;
using ODCC.Infrastructure.Modules.Organization.Persistence;

namespace ODCC.Infrastructure.Modules.Organization.Repositories;

/// <summary>
/// پیاده‌سازی مخزن کارمندان.
/// </summary>
public sealed class EmployeeRepository(OrganizationDbContext dbContext) : IEmployeeRepository
{
    private readonly OrganizationDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<Employee>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Employees.AsNoTracking().OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToListAsync(ct);

    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Employees.CountAsync(ct);

    public async Task AddAsync(Employee entity, CancellationToken ct = default) =>
        await _dbContext.Employees.AddAsync(entity, ct);

    public void Remove(Employee entity) => entity.IsDeleted = true;

    public void Update(Employee entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Employees.Update(entity);
    }

    public Task<Employee?> FindByEmployeeCodeAsync(string code, CancellationToken ct = default) =>
        _dbContext.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == code, ct);

    public Task<Employee?> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _dbContext.Employees.FirstOrDefaultAsync(e => e.UserId == userId, ct);

    public async Task<IReadOnlyList<Employee>> SearchAsync(
        EmployeeSearchRequest request,
        string? pathPrefix,
        CancellationToken ct = default)
    {
        var query = _dbContext.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(e =>
                EF.Functions.Like((e.FirstName + " " + e.LastName), $"%{text}%") ||
                EF.Functions.Like(e.EmployeeCode, $"%{text}%") ||
                EF.Functions.Like(e.NationalCode ?? string.Empty, $"%{text}%"));
        }

        if (request.OrgUnitId is { } unitId)
        {
            if (request.IncludeDescendants)
            {
                // پیشوند مسیر مادی: اگر فراخوان نداده، خودمان آن را از دیتابیس حل می‌کنیم.
                var prefix = pathPrefix;
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = await _dbContext.OrgUnits
                        .AsNoTracking()
                        .Where(u => u.Id == unitId)
                        .Select(u => (string?)u.Path)
                        .FirstOrDefaultAsync(ct);
                }

                // مسیر یافت نشد → واحد وجود ندارد → نتیجه‌ی خالی.
                if (string.IsNullOrEmpty(prefix))
                {
                    return [];
                }

                // محدودسازی زیردرخت با پیشوند مسیر مادی.
                query = query.Join(
                    _dbContext.OrgUnits.AsNoTracking(),
                    e => e.OrgUnitId,
                    u => u.Id,
                    (e, u) => new { Employee = e, Unit = u })
                    .Where(x => x.Unit.Path == prefix || x.Unit.Path.StartsWith(prefix + "/"))
                    .Select(x => x.Employee);
            }
            else
            {
                query = query.Where(e => e.OrgUnitId == unitId);
            }
        }

        if (request.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        var employees = await query
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Skip((Math.Max(request.Page, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return employees;
    }

    public Task<int> CountByOrgUnitAsync(Guid orgUnitId, CancellationToken ct = default) =>
        _dbContext.Employees.CountAsync(e => e.OrgUnitId == orgUnitId, ct);

    /// <summary>
    /// چند کارمند با شناسه. کوئری ساده با <c>Contains</c> که در هر دو ارائه‌دهنده
    /// (SQL Server و SQLite) به‌خوبی ترجمه می‌شود.
    /// </summary>
    public async Task<IReadOnlyList<Employee>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Employees.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(ct);
    }

    /// <summary>
    /// تمام کارمندان شاغل (فعال یا مرخصی).
    /// </summary>
    public async Task<IReadOnlyList<Employee>> GetEmployedAsync(CancellationToken ct = default) =>
        await _dbContext.Employees.AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active || e.Status == EmployeeStatus.OnLeave)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .ToListAsync(ct);

    /// <summary>
    /// کارمندان چند واحد سازمانی.
    ///
    /// برای شامل‌کردن زیرمجموعه‌ها، ابتدا مسیر مادی واحدهای هدف خوانده می‌شود،
    /// سپس شناسه‌ی همه‌ی واحدهای زیردرختِ آن‌ها در حافظه محاسبه می‌شود و در نهایت
    /// یک پرس‌وجوی <c>Contains</c> ساده روی کارمندان زده می‌شود. این کار از
    /// ترجمه‌ی عبارات پیچیده‌ی <c>StartsWith</c> در ارائه‌دهنده‌های مختلف پایگاه
    /// داده می‌کاهد و در SQL Server و SQLite یکسان کار می‌کند.
    /// </summary>
    public async Task<IReadOnlyList<Employee>> GetByOrgUnitsAsync(
        IReadOnlyCollection<Guid> orgUnitIds,
        bool includeDescendants,
        bool employedOnly,
        CancellationToken ct = default)
    {
        if (orgUnitIds.Count == 0)
        {
            return [];
        }

        // گام ۱: شناسه‌ی واحدهای هدف (در صورت نبودن واحد، نتیجه خالی است).
        var targetUnits = await _dbContext.OrgUnits.AsNoTracking()
            .Where(u => orgUnitIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Path })
            .ToListAsync(ct);

        if (targetUnits.Count == 0)
        {
            return [];
        }

        HashSet<Guid> unitIds;

        if (includeDescendants)
        {
            // گام ۲: همه‌ی واحدها برای محاسبه‌ی زیردرخت‌ها در حافظه.
            var allUnits = await _dbContext.OrgUnits.AsNoTracking()
                .Select(u => new { u.Id, u.Path })
                .ToListAsync(ct);

            var prefixes = targetUnits.Select(t => t.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

            unitIds = allUnits
                .Where(u => prefixes.Any(p => string.Equals(u.Path, p, StringComparison.OrdinalIgnoreCase)
                                              || u.Path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
                .Select(u => u.Id)
                .ToHashSet();
        }
        else
        {
            unitIds = targetUnits.Select(t => t.Id).ToHashSet();
        }

        if (unitIds.Count == 0)
        {
            return [];
        }

        // گام ۳: کارمندان واحدهای منتخب.
        var query = _dbContext.Employees.AsNoTracking().Where(e => unitIds.Contains(e.OrgUnitId));

        if (employedOnly)
        {
            query = query.Where(e => e.Status == EmployeeStatus.Active || e.Status == EmployeeStatus.OnLeave);
        }

        return await query
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .ToListAsync(ct);
    }
}
