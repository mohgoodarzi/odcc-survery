using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Infrastructure.Modules.Organization.Persistence;

namespace ODCC.Infrastructure.Modules.Organization.Services;

/// <summary>
/// پیاده‌سازی سرویس موقعیت‌های شغلی.
///
/// <b>داده‌ای جداگانه:</b> موقعیت‌ها از طریق واحد سازمانی‌شان به دامنه‌ی قابل‌مشاهده
/// توسط کاربر جاری محدود می‌شوند.
/// </summary>
public sealed class PositionService(
    IPositionRepository positionRepository,
    IOrgUnitRepository orgUnitRepository,
    IOrgScopeProvider orgScopeProvider,
    IOrganizationUnitOfWork unitOfWork,
    OrganizationDbContext dbContext) : IPositionService
{
    private readonly IPositionRepository _positionRepository = positionRepository;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly IOrgScopeProvider _orgScopeProvider = orgScopeProvider;
    private readonly IOrganizationUnitOfWork _unitOfWork = unitOfWork;
    private readonly OrganizationDbContext _dbContext = dbContext;

    public async Task<Result<IReadOnlyList<PositionDto>>> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        // fail-closed: دامنه بدون Sight سازمانی قابل‌مشاهده → لیست خالی.
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Success<IReadOnlyList<PositionDto>>([]);
        }

        var query = _dbContext.Positions.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!scope.IsUnrestricted)
        {
            var prefix = scope.VisiblePathPrefix!;
            query = query
                .Join(_dbContext.OrgUnits.AsNoTracking(),
                    p => p.OrgUnitId, u => u.Id,
                    (p, u) => new { Position = p, Unit = u })
                .Where(x => x.Unit.Path == prefix || x.Unit.Path.StartsWith(prefix + "/"))
                .Select(x => x.Position);
        }

        var positions = await query.OrderBy(p => p.Title).ToListAsync(ct);

        var unitNames = await GetUnitNamesAsync(positions, ct);
        var reportTitles = await GetReportTitlesAsync(positions, ct);

        var dtos = positions.Select(p => ToDto(p,
            unitNames.GetValueOrDefault(p.OrgUnitId),
            reportTitles.GetValueOrDefault(p.ReportsToPositionId ?? Guid.Empty))).ToList();

        return Result.Success<IReadOnlyList<PositionDto>>(dtos);
    }

    public async Task<Result<PositionDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var position = await _positionRepository.GetByIdAsync(id, ct);
        if (position is null)
        {
            return Result.Failure<PositionDto>("position_not_found", "موقعیت شغلی یافت نشد.");
        }

        // بررسی دامنه از طریق مسیر واحد متعلق به موقعیت.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var unitPath = await GetUnitPathAsync(position.OrgUnitId, ct);
        if (!scope.CanAccess(position.OrgUnitId, unitPath))
        {
            return Result.Failure<PositionDto>("access_denied", "شما به این موقعیت شغلی دسترسی ندارید.");
        }

        return Result.Success(ToDto(position, null, null));
    }

    public async Task<Result<PositionDto>> CreateAsync(SavePositionRequest request, CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
        {
            return Result.Failure<PositionDto>("access_denied", "شما اجازه‌ی ایجاد موقعیت در این دامنه را ندارید.");
        }

        var existing = await _positionRepository.FindByCodeAsync(request.Code, ct);
        if (existing is not null)
        {
            return Result.Failure<PositionDto>("position_code_taken", "این کد موقعیت قبلاً استفاده شده است.");
        }

        var unit = await _orgUnitRepository.GetByIdAsync(request.OrgUnitId, ct);
        if (unit is null)
        {
            return Result.Failure<PositionDto>("org_unit_not_found", "واحد سازمانی یافت نشد.");
        }

        // واحد مقصد باید داخل دامنه‌ی کاربر باشد.
        if (!scope.CanAccess(unit.Id, unit.Path))
        {
            return Result.Failure<PositionDto>("access_denied", "شما به واحد سازمانی انتخاب‌شده دسترسی ندارید.");
        }

        if (request.ReportsToPositionId is { } reportsToId)
        {
            var reportsTo = await _positionRepository.GetByIdAsync(reportsToId, ct);
            if (reportsTo is null)
            {
                return Result.Failure<PositionDto>("position_not_found", "موقعیت بالاسری یافت نشد.");
            }

            var reportsToUnitPath = await GetUnitPathAsync(reportsTo.OrgUnitId, ct);
            if (!scope.CanAccess(reportsTo.OrgUnitId, reportsToUnitPath))
            {
                return Result.Failure<PositionDto>("access_denied", "شما به موقعیت بالاسری انتخاب‌شده دسترسی ندارید.");
            }
        }

        var position = new Position
        {
            Code = request.Code,
            Title = request.Title,
            OrgUnitId = request.OrgUnitId,
            ReportsToPositionId = request.ReportsToPositionId,
            Grade = request.Grade,
            IsActive = request.IsActive,
            Headcount = request.Headcount,
            Description = request.Description
        };

        await _positionRepository.AddAsync(position, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(position, unit.Name, null));
    }

    public async Task<Result<PositionDto>> UpdateAsync(Guid id, SavePositionRequest request, CancellationToken ct = default)
    {
        var position = await _positionRepository.GetByIdAsync(id, ct);
        if (position is null)
        {
            return Result.Failure<PositionDto>("position_not_found", "موقعیت شغلی یافت نشد.");
        }

        // بررسی دامنه: ویرایش موقعیتی خارج از دامنه ممکن نیست.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var unitPath = await GetUnitPathAsync(position.OrgUnitId, ct);
        if (!scope.CanAccess(position.OrgUnitId, unitPath))
        {
            return Result.Failure<PositionDto>("access_denied", "شما به این موقعیت شغلی دسترسی ندارید.");
        }

        // واحد مقصد جدید هم باید داخل دامنه باشد.
        var newUnitPath = await GetUnitPathAsync(request.OrgUnitId, ct);
        if (!scope.CanAccess(request.OrgUnitId, newUnitPath))
        {
            return Result.Failure<PositionDto>("access_denied", "شما به واحد سازمانی انتخاب‌شده دسترسی ندارید.");
        }

        if (request.ReportsToPositionId is { } reportsToId && reportsToId != id)
        {
            var reportsTo = await _positionRepository.GetByIdAsync(reportsToId, ct);
            if (reportsTo is null)
            {
                return Result.Failure<PositionDto>("position_not_found", "موقعیت بالاسری یافت نشد.");
            }

            var reportsToUnitPath = await GetUnitPathAsync(reportsTo.OrgUnitId, ct);
            if (!scope.CanAccess(reportsTo.OrgUnitId, reportsToUnitPath))
            {
                return Result.Failure<PositionDto>("access_denied", "شما به موقعیت بالاسری انتخاب‌شده دسترسی ندارید.");
            }
        }

        if (!string.Equals(position.Code, request.Code, StringComparison.Ordinal))
        {
            var existing = await _positionRepository.FindByCodeAsync(request.Code, ct);
            if (existing is not null && existing.Id != id)
            {
                return Result.Failure<PositionDto>("position_code_taken", "این کد موقعیت قبلاً استفاده شده است.");
            }
        }

        position.Code = request.Code;
        position.Title = request.Title;
        position.OrgUnitId = request.OrgUnitId;
        position.ReportsToPositionId = request.ReportsToPositionId;
        position.Grade = request.Grade;
        position.IsActive = request.IsActive;
        position.Headcount = request.Headcount;
        position.Description = request.Description;

        _positionRepository.Update(position);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(position, null, null));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var position = await _positionRepository.GetByIdAsync(id, ct);
        if (position is null)
        {
            return Result.Failure("position_not_found", "موقعیت شغلی یافت نشد.");
        }

        // بررسی دامنه: حذف موقعیتی خارج از دامنه ممکن نیست.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var unitPath = await GetUnitPathAsync(position.OrgUnitId, ct);
        if (!scope.CanAccess(position.OrgUnitId, unitPath))
        {
            return Result.Failure("access_denied", "شما به این موقعیت شغلی دسترسی ندارید.");
        }

        var reportsCount = await _positionRepository.CountReportsToAsync(id, ct);
        if (reportsCount > 0)
        {
            return Result.Failure("position_has_reports", "این موقعیت دارای موقعیت‌های زیردست است و نمی‌تواند حذف شود.");
        }

        var employeeCount = await _positionRepository.CountActiveEmployeesAsync(id, ct);
        if (employeeCount > 0)
        {
            return Result.Failure("position_has_employees", "این موقعیت دارای کارمند فعال است و نمی‌تواند حذف شود.");
        }

        _positionRepository.Remove(position);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    private async Task<Dictionary<Guid, string>> GetUnitNamesAsync(IReadOnlyList<Position> positions, CancellationToken ct)
    {
        var ids = positions.Select(p => p.OrgUnitId).Distinct().ToList();
        return await _dbContext.OrgUnits
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    /// <summary>مسیر مادی یک واحد سازمانی (برای بررسی دامنه).</summary>
    private Task<string?> GetUnitPathAsync(Guid orgUnitId, CancellationToken ct) =>
        _dbContext.OrgUnits
            .AsNoTracking()
            .Where(u => u.Id == orgUnitId)
            .Select(u => (string?)u.Path)
            .FirstOrDefaultAsync(ct);

    private async Task<Dictionary<Guid, string>> GetReportTitlesAsync(IReadOnlyList<Position> positions, CancellationToken ct)
    {
        var ids = positions.Select(p => p.ReportsToPositionId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Positions
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);
    }

    private static PositionDto ToDto(Position position, string? unitName, string? reportsToTitle) => new()
    {
        Id = position.Id,
        Code = position.Code,
        Title = position.Title,
        OrgUnitId = position.OrgUnitId,
        OrgUnitName = unitName,
        ReportsToPositionId = position.ReportsToPositionId,
        ReportsToTitle = reportsToTitle,
        Grade = position.Grade,
        IsActive = position.IsActive,
        Headcount = position.Headcount,
        Description = position.Description
    };
}
