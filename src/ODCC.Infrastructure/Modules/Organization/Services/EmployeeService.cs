using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Infrastructure.Modules.Organization.ExcelImport;
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
    IPositionRepository positionRepository,
    IOrgScopeProvider orgScopeProvider,
    ICurrentUserService currentUserService,
    IUserLookupService userLookupService,
    IOrganizationUnitOfWork unitOfWork,
    OrganizationDbContext dbContext) : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly IPositionRepository _positionRepository = positionRepository;
    private readonly IOrgScopeProvider _orgScopeProvider = orgScopeProvider;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IUserLookupService _userLookupService = userLookupService;
    private readonly IOrganizationUnitOfWork _unitOfWork = unitOfWork;
    private readonly OrganizationDbContext _dbContext = dbContext;

    public async Task<Result<PagedResult<EmployeeSummaryDto>>> SearchAsync(EmployeeSearchRequest request, CancellationToken ct = default)
    {
        // محدودسازی دامنه: مسیر قابل‌مشاهده توسط کاربر جاری.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var effectiveRequest = request;

        // fail-closed: دامنه‌ای که داده‌ی سازمانی قابل‌مشاهده ندارد، لیست خالی برمی‌گرداند.
        // (کاربر ناشناس، دامنه‌ی Own، یا لنگر نامعتبر.) داده‌ی شخصی کاربر از مسیر
        // GetByUserIdAsync قابل دسترسی است، نه از جستجوی سازمانی.
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Success(new PagedResult<EmployeeSummaryDto>
            {
                Items = [],
                TotalCount = 0,
                Page = Math.Max(request.Page, 1),
                PageSize = request.PageSize
            });
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
        var totalCount = await _employeeRepository.SearchCountAsync(effectiveRequest, scope.VisiblePathPrefix, ct);

        var unitNames = await GetUnitNamesAsync(employees, ct);
        var positionTitles = await GetPositionTitlesAsync(employees, ct);
        var managerNames = await GetManagerNamesAsync(employees, ct);

        var dtos = employees.Select(e => ToSummary(
            e,
            unitNames.GetValueOrDefault(e.OrgUnitId),
            positionTitles.GetValueOrDefault(e.PositionId ?? Guid.Empty),
            managerNames.GetValueOrDefault(e.ManagerId ?? Guid.Empty))).ToList();

        return Result.Success(new PagedResult<EmployeeSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = effectiveRequest.Page,
            PageSize = effectiveRequest.PageSize
        });
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

    public async Task<Result<EmployeeImportResultDto>> ImportAsync(Stream excelStream, CancellationToken ct = default)
    {
        // 1. کنترل دامنه‌ی دسترسی (fail-closed)
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Failure<EmployeeImportResultDto>(
                "access_denied", "شما اجازه‌ی ورود اطلاعات کارمندان در این دامنه را ندارید.");
        }

        // 2. تجزیه‌ی فایل اکسل به ردیف‌های خام
        List<EmployeeImportRow> rows;
        try
        {
            rows = EmployeeExcelParser.Parse(excelStream);
        }
        catch (EmployeeImportException ex)
        {
            return Result.Failure<EmployeeImportResultDto>(ex.Code, ex.Message);
        }
        catch (Exception)
        {
            return Result.Failure<EmployeeImportResultDto>(
                "import_parse_failed", "فایل قابل خواندن نیست. مطمئن شوید یک فایل اکسل معتبر (.xlsx) است.");
        }

        // 3. بارگذاری یکباره‌ی ارجاعات لازم (دامنه، از کار افتادگی، موقعیت‌ها و مدیران)

        var codes = rows
            .Select(r => r.EmployeeCode?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        var managerCodes = rows
            .Select(r => r.ManagerCode?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        var orgCodes = rows
            .Select(r => r.OrgUnitCode?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        var positionCodes = rows
            .Select(r => r.PositionCode?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        // کدهای پرسنلی موجود در پایگاه داده. فیلتر حذف نرم نادیده گرفته می‌شود
        // چون اندیس یکتا ردیف‌های حذف‌شده را هم پوشش می‌دهد و باید از نقض آن جلوگیری کنیم.
        var existingCodes = await _dbContext.Employees
            .IgnoreQueryFilters()
            .Where(e => codes.Contains(e.EmployeeCode))
            .Select(e => e.EmployeeCode)
            .ToListAsync(ct);

        var managers = new List<Employee>();
        if (managerCodes.Count > 0)
        {
            managers = await _dbContext.Employees
                .Where(e => managerCodes.Contains(e.EmployeeCode))
                .ToListAsync(ct);
        }

        var units = await _dbContext.OrgUnits
            .Where(u => orgCodes.Contains(u.Code))
            .ToListAsync(ct);

        var positions = await _dbContext.Positions
            .Where(p => positionCodes.Contains(p.Code))
            .ToListAsync(ct);

        // 4.process rows
        var validator = new SaveEmployeeRequestValidator();
        var results = new List<EmployeeImportRowResultDto>();
        var created = new List<Employee>(rows.Count);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // کارمندانی که در همین فایل ایجاد می‌شوند (برای ارجاع به عنوان مدیر).
        var creatingByCode = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var code = row.EmployeeCode?.Trim() ?? string.Empty;
            var firstName = row.FirstName?.Trim() ?? string.Empty;
            var lastName = row.LastName?.Trim() ?? string.Empty;

            var result = new EmployeeImportRowResultDto
            {
                RowNumber = row.RowNumber,
                EmployeeCode = code,
                FullName = $"{firstName} {lastName}".Trim()
            };

            // (a) کد پرسنلی تکراری در فایل یا پایگاه داده -> نادیده‌گیری
            if (code.Length > 0 && (seenCodes.Contains(code) || existingCodes.Contains(code)))
            {
                result.Outcome = "skipped";
                result.Message = "کد پرسنلی تکراری است؛ این ردیف نادیده گرفته شد.";
                results.Add(result);
                continue;
            }

            // (b) ساخت درخواست
            DateOnly? startDate = EmployeeExcelParser.ParseDate(row.StartDateText) ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var request = new SaveEmployeeRequest
            {
                EmployeeCode = code,
                NationalCode = Trimmed(row.NationalCode),
                FirstName = firstName,
                LastName = lastName,
                FatherName = Trimmed(row.FatherName),
                // OrgUnitId در ادامه تنظیم می‌شود
                Status = EmployeeExcelParser.ParseStatus(row.StatusText),
                StartDate = startDate.Value,
                EndDate = EmployeeExcelParser.ParseDate(row.EndDateText),
                WorkEmail = Trimmed(row.WorkEmail),
                InternalPhone = Trimmed(row.InternalPhone),
                PositionId = null,
                ManagerId = null
            };

            var messages = new List<string>();

            // (c) اعتبارسنجی

            // واحد سازمانی
            var unit = units.FirstOrDefault(u =>
                string.Equals(u.Code, row.OrgUnitCode?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (unit is null)
            {
                messages.Add($"کد واحد سازمانی «{row.OrgUnitCode?.Trim() ?? "—"}» پیدا نشد.");
            }
            else
            {
                request = request with { OrgUnitId = unit.Id };

                if (!scope.CanAccess(unit.Id, unit.Path))
                {
                    messages.Add("واحد سازمانی خارج از دامنه‌ی دسترسی شماست.");
                }
            }

            // موقعیت شغلی
            if (!string.IsNullOrWhiteSpace(row.PositionCode))
            {
                var position = positions.FirstOrDefault(p =>
                    string.Equals(p.Code, row.PositionCode!.Trim(), StringComparison.OrdinalIgnoreCase));

                if (position is null)
                {
                    messages.Add($"کد موقعیت شغلی «{row.PositionCode!.Trim()}» پیدا نشد.");
                }
                else
                {
                    request = request with { PositionId = position.Id };
                }
            }

            // مدیر (موجود در پایگاه داده یا ایجادشده در همین فایل)
            if (!string.IsNullOrWhiteSpace(row.ManagerCode))
            {
                var managerCode = row.ManagerCode!.Trim();
                var manager = managers.FirstOrDefault(m =>
                    string.Equals(m.EmployeeCode, managerCode, StringComparison.OrdinalIgnoreCase));

                manager ??= creatingByCode.TryGetValue(managerCode, out var creating) ? creating : null;

                if (manager is null)
                {
                    messages.Add($"کد پرسنلی مدیر «{managerCode}» پیدا نشد.");
                }
                else
                {
                    request = request with { ManagerId = manager.Id };
                }
            }

            // اعتبارسنج استاندارد (دقیقاً همان قوانین فرم ایجاد کارمند)
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                messages.AddRange(validation.Errors.Select(e => e.ErrorMessage));
            }

            if (messages.Count > 0)
            {
                result.Outcome = "failed";
                result.Message = string.Join(" ", messages.Distinct());
                results.Add(result);
                continue;
            }

            // (d) ایجاد موجودیت
            var employee = new Employee
            {
                EmployeeCode = request.EmployeeCode,
                NationalCode = request.NationalCode,
                FirstName = request.FirstName,
                LastName = request.LastName,
                FatherName = request.FatherName,
                UserId = null,
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

            seenCodes.Add(request.EmployeeCode);
            creatingByCode[request.EmployeeCode] = employee;
            created.Add(employee);

            result.Outcome = "created";
            results.Add(result);
        }

        // 5. ذخیره‌سازی اتمیک همه‌ی ردیف‌های ایجادشده
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<EmployeeImportResultDto>(
                "import_save_failed",
                "ذخیره‌سازی ردیف‌ها شکست خورد (احتمالاً کد پرسنلی تکراری). عملیات لغو شد.");
        }

        return Result.Success(new EmployeeImportResultDto
        {
            TotalRows = rows.Count,
            CreatedCount = created.Count,
            SkippedCount = results.Count(r => r.Outcome == "skipped"),
            FailedCount = results.Count(r => r.Outcome == "failed"),
            Rows = results
        });
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

    /// <summary>
    /// هرس کردن فضاهای خالی؛ مقدار خالی یا فقط‌فضا به <c>null</c> تبدیل می‌شود
    /// تا فیلدهای اختیاری در ورود گروهی یکدست باشند.
    /// </summary>
    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
