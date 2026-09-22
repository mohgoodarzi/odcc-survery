using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Domain.Modules.Organization.Events;
using ODCC.Infrastructure.Modules.Organization.Persistence;

namespace ODCC.Infrastructure.Modules.Organization.Services;

/// <summary>
/// پیاده‌سازی سرویس واحدهای سازمانی.
/// مسیر مادی هر واحد هنگام ایجاد/ویرایش محاسبه می‌شود.
///
/// <b>داده‌ای جداگانه:</b> تمام پرس‌وجوها به دامنه‌ی سازمانی قابل‌مشاهده توسط
/// کاربر جاری محدود می‌شوند. کاربر با دامنه‌ی محدود نمی‌تواند با دانستن یک شناسه
/// یا کد، واحدهای خارج از دامنه‌ی خود را ببیند یا ویرایش کند.
/// </summary>
public sealed class OrgUnitService(
    IOrgUnitRepository orgUnitRepository,
    IEmployeeRepository employeeRepository,
    IOrgScopeProvider orgScopeProvider,
    ICurrentUserService currentUserService,
    IOrganizationUnitOfWork unitOfWork,
    OrganizationDbContext dbContext) : IOrgUnitService
{
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;
    private readonly IOrgScopeProvider _orgScopeProvider = orgScopeProvider;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IOrganizationUnitOfWork _unitOfWork = unitOfWork;
    private readonly OrganizationDbContext _dbContext = dbContext;

    public async Task<Result<IReadOnlyList<OrgUnitDto>>> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        // fail-closed: دامنه بدون Sicht سازمانی قابل‌مشاهده → لیست خالی.
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Success<IReadOnlyList<OrgUnitDto>>([]);
        }

        var query = _dbContext.OrgUnits.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(u => u.IsActive);
        }

        if (!scope.IsUnrestricted)
        {
            var prefix = scope.VisiblePathPrefix!;
            query = query.Where(u => u.Path == prefix || u.Path.StartsWith(prefix + "/"));
        }

        var units = await query.OrderBy(u => u.Path).ToListAsync(ct);

        var parentNames = await GetParentNamesAsync(units, ct);
        var employeeCounts = await GetEmployeeCountsAsync(units, ct);

        var dtos = units.Select(u => ToDto(
            u,
            u.ParentId is { } pid && parentNames.TryGetValue(pid, out var pname) ? pname : null,
            employeeCounts.TryGetValue(u.Id, out var count) ? count : 0)).ToList();

        return Result.Success<IReadOnlyList<OrgUnitDto>>(dtos);
    }

    public async Task<Result<OrgUnitDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var unit = await _orgUnitRepository.GetByIdAsync(id, ct);
        if (unit is null)
        {
            return Result.Failure<OrgUnitDto>("org_unit_not_found", "واحد سازمانی یافت نشد.");
        }

        // بررسی دامنه: کاربر فقط واحدهای داخل دامنه‌ی خود را می‌بیند.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        if (!scope.CanAccess(unit.Id, unit.Path))
        {
            return Result.Failure<OrgUnitDto>("access_denied", "شما به این واحد سازمانی دسترسی ندارید.");
        }

        var parentName = unit.ParentId is { } parentId
            ? (await _orgUnitRepository.GetByIdAsync(parentId, ct))?.Name
            : null;

        var employeeCount = await _orgUnitRepository.CountEmployeesAsync(id, ct);

        return Result.Success(ToDto(unit, parentName, employeeCount));
    }

    public async Task<Result<IReadOnlyList<OrgUnitTreeDto>>> GetTreeAsync(CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        // fail-closed: دامنه بدون Sicht سازمانی قابل‌مشاهده → درخت خالی.
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Success<IReadOnlyList<OrgUnitTreeDto>>([]);
        }

        var query = _dbContext.OrgUnits.AsNoTracking().Where(u => u.IsActive);

        if (!scope.IsUnrestricted)
        {
            var prefix = scope.VisiblePathPrefix!;
            query = query.Where(u => u.Path == prefix || u.Path.StartsWith(prefix + "/"));
        }

        var units = await query.OrderBy(u => u.Path).ToListAsync(ct);

        var lookup = units.ToLookup(u => u.ParentId);
        var visibleIds = units.Select(u => u.Id).ToHashSet();

        // ریشه‌های جنگلِ قابل‌مشاهده: واحدهایی که والد ندارند، یا والدشان
        // خارج از دامنه‌ی قابل‌مشاهده است (فیلتر شده). اگر همیشه از null شروع
        // کنیم، کاربری که دامنه‌اش از عمق درخت شروع می‌شود هیچ چیزی نمی‌بیند،
        // حتی اگر خودش و زیردرختش در نتایج باشند.
        static IReadOnlyList<OrgUnitTreeDto> BuildTree(IReadOnlyList<OrgUnit> all, ILookup<Guid?, OrgUnit> byParent, Guid? parentId) =>
            byParent[parentId]
                .Select(u => new OrgUnitTreeDto
                {
                    Id = u.Id,
                    Code = u.Code,
                    Name = u.Name,
                    Type = u.Type,
                    Level = u.Level,
                    IsActive = u.IsActive,
                    ManagerEmployeeId = u.ManagerEmployeeId,
                    Children = BuildTree(all, byParent, u.Id)
                })
                .ToList();

        var roots = units
            .Where(u => u.ParentId is null || !visibleIds.Contains(u.ParentId.Value))
            .Select(u => new OrgUnitTreeDto
            {
                Id = u.Id,
                Code = u.Code,
                Name = u.Name,
                Type = u.Type,
                Level = u.Level,
                IsActive = u.IsActive,
                ManagerEmployeeId = u.ManagerEmployeeId,
                Children = BuildTree(units, lookup, u.Id)
            })
            .ToList();

        return Result.Success<IReadOnlyList<OrgUnitTreeDto>>(roots);
    }

    public async Task<Result<IReadOnlyList<Guid>>> GetDescendantUnitIdsAsync(Guid rootId, CancellationToken ct = default)
    {
        var root = await _orgUnitRepository.GetByIdAsync(rootId, ct);
        if (root is null)
        {
            return Result.Failure<IReadOnlyList<Guid>>("org_unit_not_found", "واحد سازمانی یافت نشد.");
        }

        // فقط زیردرخت داخل دامنه‌ی کاربر برمی‌گردد.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        if (!scope.CanAccess(root.Id, root.Path))
        {
            return Result.Failure<IReadOnlyList<Guid>>("access_denied", "شما به این واحد سازمانی دسترسی ندارید.");
        }

        var descendants = await _orgUnitRepository.GetDescendantsAsync(rootId, ct);
        return Result.Success<IReadOnlyList<Guid>>(descendants.Select(u => u.Id).ToList());
    }

    public async Task<Result<OrgUnitDto>> CreateAsync(SaveOrgUnitRequest request, CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        // واحد جدید باید داخل دامنه‌ی قابل‌مشاهده کاربر باشد.
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Failure<OrgUnitDto>("access_denied", "شما اجازه‌ی ایجاد واحد سازمانی در این دامنه را ندارید.");
        }

        var existing = await _orgUnitRepository.FindByCodeAsync(request.Code, ct);
        if (existing is not null)
        {
            return Result.Failure<OrgUnitDto>("org_unit_code_taken", "این کد واحد سازمانی قبلاً استفاده شده است.");
        }

        // اعتبارسنجی والد: چرخه مجاز نیست.
        string? parentPath = null;
        if (request.ParentId is { } parentId)
        {
            var parent = await _orgUnitRepository.GetByIdAsync(parentId, ct);
            if (parent is null)
            {
                return Result.Failure<OrgUnitDto>("parent_not_found", "واحد والد یافت نشد.");
            }

            // والد باید داخل دامنه‌ی کاربر باشد تا نتواند زیرمجموعه‌ی خارج از دامنه بسازد.
            if (!scope.CanAccess(parent.Id, parent.Path))
            {
                return Result.Failure<OrgUnitDto>("access_denied", "شما به واحد والد انتخاب‌شده دسترسی ندارید.");
            }

            // جلوگیری از چرخه: والد نمی‌تواند زیردرختِ خود واحد باشد (در ایجاد غیرممکن است اما برای ایمنی).
            parentPath = parent.Path;
        }
        else if (!scope.IsUnrestricted)
        {
            // کاربر با دامنه‌ی محدود نمی‌تواند یک ریشه‌ی جدید (شرکت) خارج از دامنه‌ی خود بسازد.
            return Result.Failure<OrgUnitDto>("access_denied", "شما اجازه‌ی ایجاد واحد ریشه در این دامنه را ندارید.");
        }

        var unit = new OrgUnit
        {
            Code = request.Code,
            Name = request.Name,
            Type = request.Type,
            ParentId = request.ParentId,
            IsActive = request.IsActive,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ManagerEmployeeId = request.ManagerEmployeeId
        };

        unit.SetPath(parentPath);
        unit.RaiseDomainEvent(new OrgUnitCreatedEvent(unit.Id, unit.Code, unit.Path, _currentUserService.UserId));

        await _orgUnitRepository.AddAsync(unit, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(unit, null, 0));
    }

    public async Task<Result<OrgUnitDto>> UpdateAsync(Guid id, SaveOrgUnitRequest request, CancellationToken ct = default)
    {
        var unit = await _orgUnitRepository.GetByIdAsync(id, ct);
        if (unit is null)
        {
            return Result.Failure<OrgUnitDto>("org_unit_not_found", "واحد سازمانی یافت نشد.");
        }

        // بررسی دامنه: ویرایش واحدی خارج از دامنه ممکن نیست.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        if (!scope.CanAccess(unit.Id, unit.Path))
        {
            return Result.Failure<OrgUnitDto>("access_denied", "شما به این واحد سازمانی دسترسی ندارید.");
        }

        var codeChanged = !string.Equals(unit.Code, request.Code, StringComparison.Ordinal);
        if (codeChanged)
        {
            var existing = await _orgUnitRepository.FindByCodeAsync(request.Code, ct);
            if (existing is not null && existing.Id != id)
            {
                return Result.Failure<OrgUnitDto>("org_unit_code_taken", "این کد واحد سازمانی قبلاً استفاده شده است.");
            }
        }

        string? parentPath = null;
        if (request.ParentId is { } parentId)
        {
            if (parentId == id)
            {
                return Result.Failure<OrgUnitDto>("invalid_parent", "یک واحد نمی‌تواند والد خودش باشد.");
            }

            var parent = await _orgUnitRepository.GetByIdAsync(parentId, ct);
            if (parent is null)
            {
                return Result.Failure<OrgUnitDto>("parent_not_found", "واحد والد یافت نشد.");
            }

            // والد جدید باید داخل دامنه‌ی کاربر باشد.
            if (!scope.CanAccess(parent.Id, parent.Path))
            {
                return Result.Failure<OrgUnitDto>("access_denied", "شما به واحد والد انتخاب‌شده دسترسی ندارید.");
            }

            // جلوگیری از چرخه: والد جدید نباید زیردرخت واحد فعلی باشد.
            if (parent.Path.StartsWith(unit.Path, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<OrgUnitDto>("invalid_parent", "انتقال یک واحد به زیردرخت خودش مجاز نیست.");
            }

            parentPath = parent.Path;
        }

        unit.Code = request.Code;
        unit.Name = request.Name;
        unit.Type = request.Type;
        unit.ParentId = request.ParentId;
        unit.IsActive = request.IsActive;
        unit.StartDate = request.StartDate;
        unit.EndDate = request.EndDate;
        unit.ManagerEmployeeId = request.ManagerEmployeeId;

        // مسیر و سطح بر اساس والد جدید بازمحاسبه می‌شود.
        var oldPath = unit.Path;
        unit.SetPath(parentPath);
        await RepathDescendantsAsync(id, oldPath, unit.Path, ct);
        unit.RaiseDomainEvent(new OrgUnitUpdatedEvent(unit.Id, unit.Code, unit.Path, _currentUserService.UserId));

        _orgUnitRepository.Update(unit);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(unit, null, await _orgUnitRepository.CountEmployeesAsync(id, ct)));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var unit = await _orgUnitRepository.GetByIdAsync(id, ct);
        if (unit is null)
        {
            return Result.Failure("org_unit_not_found", "واحد سازمانی یافت نشد.");
        }

        // بررسی دامنه: حذف واحدی خارج از دامنه ممکن نیست.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        if (!scope.CanAccess(unit.Id, unit.Path))
        {
            return Result.Failure("access_denied", "شما به این واحد سازمانی دسترسی ندارید.");
        }

        var childCount = await _orgUnitRepository.CountChildrenAsync(id, ct);
        if (childCount > 0)
        {
            return Result.Failure("org_unit_has_children", "این واحد دارای زیرمجموعه است و نمی‌تواند حذف شود.");
        }

        var employeeCount = await _orgUnitRepository.CountEmployeesAsync(id, ct);
        if (employeeCount > 0)
        {
            return Result.Failure("org_unit_has_employees", "این واحد دارای کارمند است و نمی‌تواند حذف شود.");
        }

        _orgUnitRepository.Remove(unit);
        unit.RaiseDomainEvent(new OrgUnitDeletedEvent(unit.Id, unit.Code, _currentUserService.UserId));
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// بازنویسی مسیر تمام زیرمجموعه‌ها پس از جابجایی یک واحد.
    /// </summary>
    private async Task RepathDescendantsAsync(Guid unitId, string oldPath, string newPath, CancellationToken ct)
    {
        var descendants = await _dbContext.OrgUnits
            .Where(u => u.Path.StartsWith(oldPath + "/"))
            .ToListAsync(ct);

        foreach (var d in descendants)
        {
            d.Path = newPath + d.Path[oldPath.Length..];
            d.Level = d.Path.Count(c => c == '/') - 1;
        }
    }

    private async Task<Dictionary<Guid, string>> GetParentNamesAsync(IReadOnlyList<OrgUnit> units, CancellationToken ct)
    {
        var parentIds = units.Select(u => u.ParentId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (parentIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.OrgUnits
            .AsNoTracking()
            .Where(u => parentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    private async Task<Dictionary<Guid, int>> GetEmployeeCountsAsync(IReadOnlyList<OrgUnit> units, CancellationToken ct)
    {
        var ids = units.Select(u => u.Id).ToList();
        return await _dbContext.Employees
            .AsNoTracking()
            .Where(e => ids.Contains(e.OrgUnitId) && e.Status != ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.Terminated)
            .GroupBy(e => e.OrgUnitId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
    }

    private static OrgUnitDto ToDto(OrgUnit unit, string? parentName, int employeeCount) => new()
    {
        Id = unit.Id,
        Code = unit.Code,
        Name = unit.Name,
        Type = unit.Type,
        ParentId = unit.ParentId,
        ParentName = parentName,
        Path = unit.Path,
        Level = unit.Level,
        IsActive = unit.IsActive,
        StartDate = unit.StartDate,
        EndDate = unit.EndDate,
        ManagerEmployeeId = unit.ManagerEmployeeId,
        EmployeeCount = employeeCount
    };
}
