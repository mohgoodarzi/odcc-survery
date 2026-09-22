using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Infrastructure.Modules.Organization.Persistence;

namespace ODCC.Infrastructure.Modules.Organization.Services;

/// <summary>
/// پیاده‌سازی سرویس کارمندان.
///
/// جستجو به‌طور خودکار به دامنه‌ی سازمانی قابل‌مشاهده توسط کاربر جاری محدود می‌شود
/// تا کاربر نتواند با دانستن یک شناسه، داده‌های خارج از دامنه‌ی خود را ببیند.
/// </summary>
public sealed class EmployeeService(
    IEmployeeRepository employeeRepository,
    IOrgUnitRepository orgUnitRepository,
    IOrgScopeProvider orgScopeProvider,
    ICurrentUserService currentUserService,
    IUserLookupService userLookupService,
    IOrganizationUnitOfWork unitOfWork,
    OrganizationDbContext dbContext) : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly IOrgScopeProvider _orgScopeProvider = orgScopeProvider;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IUserLookupService _userLookupService = userLookupService;
    private readonly IOrganizationUnitOfWork _unitOfWork = unitOfWork;
    private readonly OrganizationDbContext _dbContext = dbContext;

    public async Task<Result<IReadOnlyList<EmployeeSummaryDto>>> SearchAsync(EmployeeSearchRequest request, CancellationToken ct = default)
    {
        // محدودسازی دامنه: مسیر قابل‌مشاهده توسط کاربر جاری.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var effectiveRequest = request;

        // fail-closed: دامنه‌ای که داده‌ی سازمانی قابل‌مشاهده ندارد، لیست خالی برمی‌گرداند.
        // (کاربر ناشناس، دامنه‌ی Own، یا لنگر نامعتبر.) داده‌ی شخصی کاربر از مسیر
        // GetByUserIdAsync قابل دسترسی است، نه از جستجوی سازمانی.
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Success<IReadOnlyList<EmployeeSummaryDto>>([]);
        }

        if (!scope.IsUnrestricted)
        {
            // اگر درخواست خارج از دامنه‌ی کاربر بود، آن را به دامنه‌ی کاربر محدود می‌کنیم.
            if (request.OrgUnitId is { } requestedUnit && requestedUnit != scope.AnchorOrgUnitId)
            {
                var requestedPath = await _orgUnitRepository.GetPathAsync(requestedUnit, ct);
                if (!scope.CanAccess(requestedPath))
                {
                    // شناسه‌ی خارج از دامنه: به جای خطا، جستجو را به دامنه‌ی کاربر محدود می‌کنیم.
                    effectiveRequest = request with { OrgUnitId = scope.AnchorOrgUnitId, IncludeDescendants = true };
                }
            }
            else if (request.OrgUnitId is null)
            {
                // بدون شناسه: کل دامنه‌ی کاربر.
                effectiveRequest = request with { OrgUnitId = scope.AnchorOrgUnitId, IncludeDescendants = true };
            }
        }

        var employees = await _employeeRepository.SearchAsync(effectiveRequest, scope.VisiblePathPrefix, ct);

        var unitNames = await GetUnitNamesAsync(employees, ct);
        var positionTitles = await GetPositionTitlesAsync(employees, ct);
        var managerNames = await GetManagerNamesAsync(employees, ct);

        var dtos = employees.Select(e => ToSummary(
            e,
            unitNames.GetValueOrDefault(e.OrgUnitId),
            positionTitles.GetValueOrDefault(e.PositionId ?? Guid.Empty),
            managerNames.GetValueOrDefault(e.ManagerId ?? Guid.Empty))).ToList();

        return Result.Success<IReadOnlyList<EmployeeSummaryDto>>(dtos);
    }

    public async Task<Result<EmployeeDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, ct);
        if (employee is null)
        {
            return Result.Failure<EmployeeDto>("employee_not_found", "کارمند یافت نشد.");
        }

        // بررسی دامنه: دامنه یک‌بار به‌صورت ناهمگام محاسبه و سپس به‌صورت خالص بررسی می‌شود
        // (از متدهای sync روی نتایج async استفاده نمی‌کنیم تا بن‌بست رخ ندهد).
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        // کاربر با دامنه‌ی Own فقط رکورد خودش را می‌بیند.
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope && employee.UserId != _currentUserService.UserId)
        {
            return Result.Failure<EmployeeDto>("access_denied", "شما به این کارمند دسترسی ندارید.");
        }

        // اگر کاربر لنگر سازمانی معتبر دارد، مسیر واحد او را بررسی می‌کنیم.
        // برای دامنه‌ی Own این بررسی با بررسی «رکورد خودش» در بالا جایگزین شده است.
        if (scope.HasVisibleOrgScope)
        {
            if (!scope.CanAccess(employee.OrgUnitId, await GetUnitPathAsync(employee.OrgUnitId, ct)))
            {
                return Result.Failure<EmployeeDto>("access_denied", "شما به این کارمند دسترسی ندارید.");
            }
        }

        return Result.Success(await ToDtoAsync(employee, ct));
    }

    public async Task<Result<EmployeeDto>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var employee = await _employeeRepository.FindByUserIdAsync(userId, ct);
        if (employee is null)
        {
            return Result.Failure<EmployeeDto>("employee_not_found", "کارمند یافت نشد.");
        }

        return Result.Success(await ToDtoAsync(employee, ct));
    }

    public async Task<Result<EmployeeDto>> CreateAsync(SaveEmployeeRequest request, CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Failure<EmployeeDto>("access_denied", "شما اجازه‌ی ایجاد کارمند در این دامنه را ندارید.");
        }

        var existing = await _employeeRepository.FindByEmployeeCodeAsync(request.EmployeeCode, ct);
        if (existing is not null)
        {
            return Result.Failure<EmployeeDto>("employee_code_taken", "این کد پرسنلی قبلاً استفاده شده است.");
        }

        var unit = await _orgUnitRepository.GetByIdAsync(request.OrgUnitId, ct);
        if (unit is null)
        {
            return Result.Failure<EmployeeDto>("org_unit_not_found", "واحد سازمانی یافت نشد.");
        }

        // واحد مقصد باید داخل دامنه‌ی کاربر باشد.
        if (!scope.CanAccess(unit.Id, unit.Path))
        {
            return Result.Failure<EmployeeDto>("access_denied", "شما به واحد سازمانی انتخاب‌شده دسترسی ندارید.");
        }

        if (request.ManagerId is { } managerId)
        {
            var manager = await _employeeRepository.GetByIdAsync(managerId, ct);
            if (manager is null)
            {
                return Result.Failure<EmployeeDto>("manager_not_found", "کارمند مدیریت‌کننده یافت نشد.");
            }

            // مدیر باید داخل دامنه‌ی کاربر باشد.
            if (!scope.CanAccess(manager.OrgUnitId, await GetUnitPathAsync(manager.OrgUnitId, ct)))
            {
                return Result.Failure<EmployeeDto>("access_denied", "شما به مدیر انتخاب‌شده دسترسی ندارید.");
            }
        }

        var employee = new Employee
        {
            EmployeeCode = request.EmployeeCode,
            NationalCode = request.NationalCode,
            FirstName = request.FirstName,
            LastName = request.LastName,
            FatherName = request.FatherName,
            UserId = request.UserId,
            OrgUnitId = request.OrgUnitId,
            PositionId = request.PositionId,
            ManagerId = request.ManagerId,
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            WorkEmail = request.WorkEmail,
            InternalPhone = request.InternalPhone
        };

        await _employeeRepository.AddAsync(employee, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(await ToDtoAsync(employee, ct));
    }

    public async Task<Result<EmployeeDto>> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, ct);
        if (employee is null)
        {
            return Result.Failure<EmployeeDto>("employee_not_found", "کارمند یافت نشد.");
        }

        // بررسی دامنه: ویرایش کارمندی خارج از دامنه ممکن نیست.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (scope.HasVisibleOrgScope)
        {
            if (!scope.CanAccess(employee.OrgUnitId, await GetUnitPathAsync(employee.OrgUnitId, ct)))
            {
                return Result.Failure<EmployeeDto>("access_denied", "شما به این کارمند دسترسی ندارید.");
            }
        }
        else if (!scope.IsUnrestricted && employee.UserId != _currentUserService.UserId)
        {
            return Result.Failure<EmployeeDto>("access_denied", "شما به این کارمند دسترسی ندارید.");
        }

        // واحد مقصد جدید هم باید داخل دامنه باشد.
        var newUnit = await _orgUnitRepository.GetByIdAsync(request.OrgUnitId, ct);
        if (newUnit is null)
        {
            return Result.Failure<EmployeeDto>("org_unit_not_found", "واحد سازمانی یافت نشد.");
        }

        if (!scope.CanAccess(newUnit.Id, newUnit.Path))
        {
            return Result.Failure<EmployeeDto>("access_denied", "شما به واحد سازمانی انتخاب‌شده دسترسی ندارید.");
        }

        if (request.ManagerId is { } managerId && managerId != id)
        {
            var manager = await _employeeRepository.GetByIdAsync(managerId, ct);
            if (manager is null)
            {
                return Result.Failure<EmployeeDto>("manager_not_found", "کارمند مدیریت‌کننده یافت نشد.");
            }

            if (!scope.CanAccess(manager.OrgUnitId, await GetUnitPathAsync(manager.OrgUnitId, ct)))
            {
                return Result.Failure<EmployeeDto>("access_denied", "شما به مدیر انتخاب‌شده دسترسی ندارید.");
            }
        }

        if (!string.Equals(employee.EmployeeCode, request.EmployeeCode, StringComparison.Ordinal))
        {
            var existing = await _employeeRepository.FindByEmployeeCodeAsync(request.EmployeeCode, ct);
            if (existing is not null && existing.Id != id)
            {
                return Result.Failure<EmployeeDto>("employee_code_taken", "این کد پرسنلی قبلاً استفاده شده است.");
            }
        }

        employee.EmployeeCode = request.EmployeeCode;
        employee.NationalCode = request.NationalCode;
        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.FatherName = request.FatherName;
        employee.UserId = request.UserId;
        employee.OrgUnitId = request.OrgUnitId;
        employee.PositionId = request.PositionId;
        employee.ManagerId = request.ManagerId;
        employee.Status = request.Status;
        employee.StartDate = request.StartDate;
        employee.EndDate = request.EndDate;
        employee.WorkEmail = request.WorkEmail;
        employee.InternalPhone = request.InternalPhone;

        _employeeRepository.Update(employee);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(await ToDtoAsync(employee, ct));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, ct);
        if (employee is null)
        {
            return Result.Failure("employee_not_found", "کارمند یافت نشد.");
        }

        // بررسی دامنه: حذف کارمندی خارج از دامنه ممکن نیست.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (scope.HasVisibleOrgScope)
        {
            if (!scope.CanAccess(employee.OrgUnitId, await GetUnitPathAsync(employee.OrgUnitId, ct)))
            {
                return Result.Failure("access_denied", "شما به این کارمند دسترسی ندارید.");
            }
        }
        else if (!scope.IsUnrestricted && employee.UserId != _currentUserService.UserId)
        {
            return Result.Failure("access_denied", "شما به این کارمند دسترسی ندارید.");
        }

        var subordinates = await _dbContext.Employees.CountAsync(e => e.ManagerId == id, ct);
        if (subordinates > 0)
        {
            return Result.Failure("employee_has_subordinates", "این کارمند دارای زیردست است و نمی‌تواند حذف شود.");
        }

        _employeeRepository.Remove(employee);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    private async Task<EmployeeDto> ToDtoAsync(Employee employee, CancellationToken ct)
    {
        var unitName = await GetUnitNameAsync(employee.OrgUnitId, ct);
        var positionTitle = await GetPositionTitleAsync(employee.PositionId, ct);
        var managerName = employee.ManagerId is { } managerId
            ? await GetEmployeeFullNameAsync(managerId, ct)
            : null;
        var userName = employee.UserId is { } userId
            ? await GetUserNameAsync(userId, ct)
            : null;

        return new EmployeeDto
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            NationalCode = employee.NationalCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            FullName = employee.FullName,
            FatherName = employee.FatherName,
            UserId = employee.UserId,
            UserName = userName,
            OrgUnitId = employee.OrgUnitId,
            OrgUnitName = unitName,
            PositionId = employee.PositionId,
            PositionTitle = positionTitle,
            ManagerId = employee.ManagerId,
            ManagerFullName = managerName,
            Status = employee.Status,
            StartDate = employee.StartDate,
            EndDate = employee.EndDate,
            WorkEmail = employee.WorkEmail,
            InternalPhone = employee.InternalPhone,
            IsCurrentlyEmployed = employee.IsCurrentlyEmployed
        };
    }

    private static EmployeeSummaryDto ToSummary(Employee e, string? unitName, string? positionTitle, string? managerName) => new()
    {
        Id = e.Id,
        EmployeeCode = e.EmployeeCode,
        FullName = e.FullName,
        OrgUnitId = e.OrgUnitId,
        OrgUnitName = unitName,
        PositionTitle = positionTitle,
        ManagerFullName = managerName,
        Status = e.Status,
        IsCurrentlyEmployed = e.IsCurrentlyEmployed
    };

    private async Task<Dictionary<Guid, string>> GetUnitNamesAsync(IReadOnlyList<Employee> employees, CancellationToken ct)
    {
        var ids = employees.Select(e => e.OrgUnitId).Distinct().ToList();
        return await _dbContext.OrgUnits
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    private async Task<Dictionary<Guid, string>> GetPositionTitlesAsync(IReadOnlyList<Employee> employees, CancellationToken ct)
    {
        var ids = employees.Select(e => e.PositionId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Positions
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);
    }

    private async Task<Dictionary<Guid, string>> GetManagerNamesAsync(IReadOnlyList<Employee> employees, CancellationToken ct)
    {
        var ids = employees.Select(e => e.ManagerId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Employees
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
    }

    private Task<string?> GetUnitNameAsync(Guid unitId, CancellationToken ct) =>
        _dbContext.OrgUnits.AsNoTracking().Where(u => u.Id == unitId).Select(u => (string?)u.Name).FirstOrDefaultAsync(ct);

    private Task<string?> GetUnitPathAsync(Guid unitId, CancellationToken ct) =>
        _dbContext.OrgUnits.AsNoTracking().Where(u => u.Id == unitId).Select(u => (string?)u.Path).FirstOrDefaultAsync(ct);

    private Task<string?> GetPositionTitleAsync(Guid? positionId, CancellationToken ct) =>
        positionId is { } id
            ? _dbContext.Positions.AsNoTracking().Where(p => p.Id == id).Select(p => (string?)p.Title).FirstOrDefaultAsync(ct)
            : Task.FromResult<string?>(null);

    private Task<string?> GetEmployeeFullNameAsync(Guid employeeId, CancellationToken ct) =>
        _dbContext.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => (string?)e.FullName).FirstOrDefaultAsync(ct);

    private Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct) =>
        _userLookupService.GetUserNameAsync(userId, ct);
}
