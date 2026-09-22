using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Domain.Common;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;

namespace ODCC.Infrastructure.Modules.Identity.Services;

/// <summary>
/// پیاده‌سازی سرویس مدیریت کاربران.
/// رمزهای عبور هرگز به‌صورت متن‌وضوح پردازش یا لاگ نمی‌شوند.
/// </summary>
public sealed class UserService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IRefreshTokenStore refreshTokenStore,
    IdentityDbContext identityDbContext) : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
    private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
    private readonly IdentityDbContext _identityDbContext = identityDbContext;

    public async Task<Result<PagedResult<UserSummaryDto>>> SearchAsync(UserSearchRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _userManager.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();

            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(text)) ||
                (u.Email != null && u.Email.Contains(text)) ||
                u.FirstName.Contains(text) ||
                u.LastName.Contains(text));
        }

        if (request.IsActive is { } isActive)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        if (request.RoleId is { } roleId)
        {
            query = query.Where(u => _identityDbContext.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        }

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // استخراج دسته‌ای نقش‌ها برای جلوگیری از پرس‌وجوی N+1.
        var userIds = users.Select(u => u.Id).ToList();
        var roleNamesByUser = await GetRoleNamesByUserAsync(userIds, ct);

        var dtos = users
            .Select(u => ToSummary(u, roleNamesByUser.TryGetValue(u.Id, out var names) ? names : []))
            .ToList();

        return Result.Success(new PagedResult<UserSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>نقش‌های چند کاربر در یک پرس‌وجو (به جای N+1).</summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> GetRoleNamesByUserAsync(List<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var pairs = await (
            from ur in _identityDbContext.UserRoles
            join role in _identityDbContext.Roles on ur.RoleId equals role.Id
            where userIds.Contains(ur.UserId) && !role.IsDeleted
            select new { ur.UserId, RoleName = role.Name }
        ).ToListAsync(ct);

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.RoleName ?? string.Empty).ToList());
    }

    public async Task<Result<UserSummaryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<UserSummaryDto>("user_not_found", "کاربر یافت نشد.");
        }

        var roleNames = await GetRoleNamesByUserAsync([user.Id], ct);
        return Result.Success(ToSummary(user, roleNames.TryGetValue(user.Id, out var names) ? names : []));
    }

    public async Task<Result<UserSummaryDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByNameAsync(request.UserName);
        if (existing is not null)
        {
            return Result.Failure<UserSummaryDto>("username_taken", "این نام کاربری قبلاً استفاده شده است.");
        }

        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail is not null)
        {
            return Result.Failure<UserSummaryDto>("email_taken", "این ایمیل قبلاً استفاده شده است.");
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            NationalCode = request.NationalCode,
            OrgUnitId = request.OrgUnitId,
            DataScope = request.DataScope,
            IsActive = request.IsActive
        };

        // Identity رمز عبور را هش می‌کند؛ هرگز متن‌وضوح ذخیره نمی‌شود.
        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var code = createResult.Errors.First().Code;
            return Result.Failure<UserSummaryDto>(code, "ایجاد کاربر ناموفق بود.");
        }

        if (request.RoleIds.Count > 0)
        {
            await AssignRolesInternalAsync(user, request.RoleIds, ct);
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Result.Success(ToSummary(user, roles.AsReadOnly()));
    }

    public async Task<Result<UserSummaryDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<UserSummaryDto>("user_not_found", "کاربر یافت نشد.");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.OrgUnitId = request.OrgUnitId;
        user.DataScope = request.DataScope;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(user, request.Email);
            if (!emailResult.Succeeded)
            {
                return Result.Failure<UserSummaryDto>(emailResult.Errors.First().Code, "به‌روزرسانی ایمیل ناموفق بود.");
            }
            await _userManager.UpdateNormalizedEmailAsync(user);
        }

        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return Result.Success(ToSummary(user, roles.AsReadOnly()));
    }

    public async Task<Result> SetActiveAsync(SetUserActiveRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        user.IsActive = request.IsActive;
        user.DeactivatedAt = request.IsActive ? null : DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (!request.IsActive)
        {
            // غیرفعال‌سازی حساب: تمام نشست‌های فعال او باطل می‌شود.
            await _refreshTokenStore.RevokeAllForUserAsync(user.Id, revokedByIp: null, ct);
        }

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Failure("password_change_failed", "تغییر رمز عبور ناموفق بود. رمز فعلی را بررسی کنید.");
        }

        // تغییر رمز: تمام نشست‌های دیگر باید باطل شوند.
        await _refreshTokenStore.RevokeAllForUserAsync(userId, revokedByIp: null, ct);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Failure("password_reset_failed", "بازنشانی رمز عبور ناموفق بود.");
        }

        await _refreshTokenStore.RevokeAllForUserAsync(user.Id, revokedByIp: null, ct);

        return Result.Success();
    }

    public async Task<Result> AssignRolesAsync(AssignRolesRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        await AssignRolesInternalAsync(user, request.RoleIds, ct);
        return Result.Success();
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<UserProfileDto>("user_not_found", "کاربر یافت نشد.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsAsync(user, ct);

        return Result.Success(new UserProfileDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            OrgUnitId = user.OrgUnitId,
            DataScope = user.DataScope,
            Roles = roles.AsReadOnly(),
            Permissions = permissions
        });
    }

    /// <summary>انتصاب نقش‌ها به کاربر (جایگزینی کامل).</summary>
    private async Task AssignRolesInternalAsync(ApplicationUser user, IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
    {
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        if (roleIds.Count == 0)
        {
            return;
        }

        var roleNames = await _identityDbContext.Roles
            .Where(r => roleIds.Contains(r.Id) && !r.IsDeleted)
            .Select(r => r.Name!)
            .ToListAsync(ct);

        if (roleNames.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, roleNames);
        }
    }

    /// <summary>مجوزهای کاربر از کلیم نقش‌ها.</summary>
    private async Task<IReadOnlyCollection<string>> GetPermissionsAsync(ApplicationUser user, CancellationToken ct)
    {
        var roleIds = await _identityDbContext.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        return await _identityDbContext.RoleClaims
            .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == Permissions.ClaimType)
            .Select(rc => rc.ClaimValue!)
            .Distinct()
            .ToListAsync(ct);
    }

    private static UserSummaryDto ToSummary(ApplicationUser user, IReadOnlyList<string> roles) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        DisplayName = user.DisplayName,
        IsActive = user.IsActive,
        EmailConfirmed = user.EmailConfirmed,
        OrgUnitId = user.OrgUnitId,
        CreatedAt = user.CreatedAt,
        Roles = roles
    };
}
