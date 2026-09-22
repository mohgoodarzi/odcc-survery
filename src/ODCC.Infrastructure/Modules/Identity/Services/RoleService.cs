using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Domain.Common;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;

namespace ODCC.Infrastructure.Modules.Identity.Services;

/// <summary>
/// پیاده‌سازی سرویس مدیریت نقش‌ها و مجوزها.
/// مجوزها به‌صورت کلیم با نوع <c>permission</c> روی نقش ذخیره می‌شوند.
/// </summary>
public sealed class RoleService(
    RoleManager<ApplicationRole> roleManager,
    IdentityDbContext identityDbContext) : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
    private readonly IdentityDbContext _identityDbContext = identityDbContext;

    public async Task<Result<IReadOnlyList<RoleSummaryDto>>> ListAsync(CancellationToken ct = default)
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var dtos = new List<RoleSummaryDto>(roles.Count);
        foreach (var role in roles)
        {
            dtos.Add(await ToSummaryAsync(role, ct));
        }

        return Result.Success<IReadOnlyList<RoleSummaryDto>>(dtos);
    }

    public async Task<Result<RoleSummaryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null || role.IsDeleted)
        {
            return Result.Failure<RoleSummaryDto>("role_not_found", "نقش یافت نشد.");
        }

        return Result.Success(await ToSummaryAsync(role, ct));
    }

    public async Task<Result<RoleSummaryDto>> CreateAsync(SaveRoleRequest request, CancellationToken ct = default)
    {
        var existing = await _roleManager.FindByNameAsync(request.Name);
        if (existing is not null)
        {
            return Result.Failure<RoleSummaryDto>("role_taken", "این نام نقش قبلاً استفاده شده است.");
        }

        var role = new ApplicationRole
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            IsSystem = false
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return Result.Failure<RoleSummaryDto>(result.Errors.First().Code, "ایجاد نقش ناموفق بود.");
        }

        await SetPermissionsAsync(role, request.Permissions, ct);

        return Result.Success(await ToSummaryAsync(role, ct));
    }

    public async Task<Result<RoleSummaryDto>> UpdateAsync(Guid id, SaveRoleRequest request, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null || role.IsDeleted)
        {
            return Result.Failure<RoleSummaryDto>("role_not_found", "نقش یافت نشد.");
        }

        if (role.IsSystem && !string.Equals(role.Name, request.Name, StringComparison.Ordinal))
        {
            return Result.Failure<RoleSummaryDto>("role_is_system", "نقش‌های سیستمی نمی‌توانند تغییر نام دهند.");
        }

        role.Name = request.Name;
        role.DisplayName = request.DisplayName;
        role.Description = request.Description;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            return Result.Failure<RoleSummaryDto>(result.Errors.First().Code, "به‌روزرسانی نقش ناموفق بود.");
        }

        await SetPermissionsAsync(role, request.Permissions, ct);

        return Result.Success(await ToSummaryAsync(role, ct));
    }

    public async Task<Result> AssignPermissionsAsync(AssignRolePermissionsRequest request, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role is null || role.IsDeleted)
        {
            return Result.Failure("role_not_found", "نقش یافت نشد.");
        }

        await SetPermissionsAsync(role, request.Permissions, ct);
        return Result.Success();
    }

    /// <summary>
    /// جایگزینی کامل مجوزهای یک نقش (حذف کلیم‌های قدیمی و درج جدید).
    /// </summary>
    private async Task SetPermissionsAsync(ApplicationRole role, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        var existingClaims = await _roleManager.GetClaimsAsync(role);
        foreach (var claim in existingClaims.Where(c => c.Type == Permissions.ClaimType))
        {
            await _roleManager.RemoveClaimAsync(role, claim);
        }

        foreach (var permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(Permissions.ClaimType, permission));
        }
    }

    private async Task<RoleSummaryDto> ToSummaryAsync(ApplicationRole role, CancellationToken ct)
    {
        var permissions = await _identityDbContext.RoleClaims
            .AsNoTracking()
            .Where(rc => rc.RoleId == role.Id && rc.ClaimType == Permissions.ClaimType)
            .Select(rc => rc.ClaimValue!)
            .ToListAsync(ct);

        return new RoleSummaryDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            DisplayName = role.DisplayName,
            Description = role.Description,
            IsSystem = role.IsSystem,
            Permissions = permissions
        };
    }
}
