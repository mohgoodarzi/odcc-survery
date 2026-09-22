using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;

namespace ODCC.Infrastructure.Modules.Identity.Services;

/// <summary>
/// پیاده‌سازی سرویس احراز هویت.
///
/// جریان ورود:
/// ۱. یافتن کاربر با نام کاربری نرمالایز شده
/// ۲. بررسی فعال بودن حساب و عدم حذف نرم
/// ۳. بررسی رمز عبور با قفل‌شدگی حساب (Identity)
/// ۴. استخراج نقش‌ها و مجوزها
/// ۵. صدور جفت توکن
///
/// جریان تازه‌سازی توکن‌ها از چرخش + تشخیص استفاده‌ی مجدد استفاده می‌کند:
/// توکن مصرف‌شده باطل می‌شود و یک توکن جدید در همان خانواده صادر می‌گردد.
/// اگر یک توکن قبلاً مصرف‌شده دوباره ارائه شود، کل خانواده باطل می‌شود
/// (نشانه‌ی سرقت توکن).
///
/// <b>مرز تراکنش:</b> تمام تغییرات (به‌روزرسانی کاربر، چرخش توکن) در یک عملیات
/// از طریق <c>IIdentityUnitOfWork</c> ذخیره می‌شوند. سرویس هرگز مستقیماً
/// <c>SaveChanges</c> نمی‌زند.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    IRefreshTokenStore refreshTokenStore,
    IdentityDbContext identityDbContext,
    IIdentityUnitOfWork unitOfWork) : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
    private readonly IdentityDbContext _identityDbContext = identityDbContext;
    private readonly IIdentityUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result<LoginResult>> LoginAsync(LoginRequest request, string? clientIp, CancellationToken ct = default)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);

        // پیام یکسان برای «کاربر وجود ندارد» و «رمز اشتباه است» تا حملات شمارش نام کاربری ممکن نشود.
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<LoginResult>("invalid_credentials", "نام کاربری یا رمز عبور نامعتبر است.");
        }

        if (!user.IsActive)
        {
            return Result.Failure<LoginResult>("account_inactive", "حساب کاربری شما غیرفعال است.");
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return Result.Failure<LoginResult>("account_locked", "حساب کاربری به دلیل تلاش‌های ناموفق متعدد قفل شده است.");
        }

        if (!signInResult.Succeeded)
        {
            return Result.Failure<LoginResult>("invalid_credentials", "نام کاربری یا رمز عبور نامعتبر است.");
        }

        // TODO (فاز بعدی): بررسی MFA. در صورت فعال بودن، یک چالش MFA صادر می‌شود
        // و توکن صادر نمی‌شود. ساختار فعلی این امکان را بدون بازطراحی حفظ می‌کند.
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user, ct);

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var issued = await _tokenService.IssueAsync(new TokenIssueContext
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            DisplayName = user.DisplayName,
            OrgUnitId = user.OrgUnitId,
            DataScope = user.DataScope,
            Roles = roles.AsReadOnly(),
            Permissions = permissions,
            DeviceInfo = request.DeviceInfo,
            ClientIp = clientIp
        }, ct);

        // به‌روزرسانی کاربر و صدور توکن تازه‌سازی در یک تراکنش.
        await _unitOfWork.SaveChangesAsync(ct);

        var profile = await BuildProfileAsync(user, roles, permissions, ct);

        return Result.Success(new LoginResult
        {
            Tokens = issued.Response with { Profile = profile }
        });
    }

    public async Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, string? clientIp, CancellationToken ct = default)
    {
        // توکن دسترسی را بدون بررسی انقضا اعتبارسنجی می‌کنیم تا فقط هویت کاربر را استخراج کنیم.
        var principal = _tokenService.ValidateAccessToken(request.AccessToken, validateLifetime: false);
        if (principal is null)
        {
            return Result.Failure<TokenResponse>("invalid_token", "توکن دسترسی نامعتبر است.");
        }

        // هویت کاربر از کلیم شناسه‌ی او استخراج می‌شود.
        // توجه: جفت‌کننده‌ی JWT ورودی، کلیم «sub» را به ClaimTypes.NameIdentifier
        // نگاشت می‌کند، بنابراین باید هر دو نام را بررسی کنیم تا روی پیکربندی‌های
        // مختلف (با نگاشت پیش‌فرض یا بدون آن) کار کند.
        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure<TokenResponse>("invalid_token", "توکن دسترسی نامعتبر است.");
        }

        var storedToken = await _refreshTokenStore.FindByTokenHashAsync(_tokenService.HashRefreshToken(request.RefreshToken), ct);

        // توکن وجود ندارد → ممکن است جعل باشد.
        if (storedToken is null)
        {
            return Result.Failure<TokenResponse>("invalid_refresh_token", "توکن تازه‌سازی نامعتبر است.");
        }

        // توکن متعلق به کاربر دیگری است.
        if (storedToken.UserId != userId)
        {
            await _refreshTokenStore.RevokeFamilyAsync(storedToken.FamilyId, clientIp, ct);
            return Result.Failure<TokenResponse>("invalid_refresh_token", "توکن تازه‌سازی نامعتبر است.");
        }

        // استفاده‌ی مجدد از توکن مصرف‌شده: کل خانواده را باطل کن (سرقت توکن).
        if (storedToken.IsRevoked)
        {
            await _refreshTokenStore.RevokeFamilyAsync(storedToken.FamilyId, clientIp, ct);
            return Result.Failure<TokenResponse>("token_revoked", "توکن تازه‌سازی قبلاً استفاده شده است. تمام نشست‌های این خانواده باطل شدند.");
        }

        if (storedToken.IsExpired)
        {
            return Result.Failure<TokenResponse>("token_expired", "توکن تازه‌سازی منقضی شده است. لطفاً دوباره وارد شوید.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted || !user.IsActive)
        {
            await _refreshTokenStore.RevokeAllForUserAsync(userId, clientIp, ct);
            return Result.Failure<TokenResponse>("account_inactive", "حساب کاربری در دسترس نیست.");
        }

        // چرخش: توکن قدیمی باطل و توکن جدید در همان خانواده صادر می‌شود.
        // هر دو تغییر در یک تراکنش ذخیره می‌شوند تا در صورت شکست، توکن جدید
        // بدون ابطال توکن قدیمی commit نشود (یا برعکس).
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user, ct);

        var issued = await _tokenService.IssueAsync(new TokenIssueContext
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            DisplayName = user.DisplayName,
            OrgUnitId = user.OrgUnitId,
            DataScope = user.DataScope,
            Roles = roles.AsReadOnly(),
            Permissions = permissions,
            DeviceInfo = storedToken.DeviceInfo,
            ClientIp = clientIp,
            // حفظ خانواده: زنجیره‌ی چرخش در یک خانواده می‌ماند تا ابطال خانواده
            // در صورت استفاده‌ی مجدد از توکن مصرف‌شده، کل زنجیره را باطل کند.
            FamilyId = storedToken.FamilyId
        }, ct);

        // توکن مصرف‌شده به هش توکن جدیدی که جایگزینش شده ارجاع می‌دهد.
        storedToken.Revoke(revokedByIp: clientIp, replacedByTokenHash: issued.RefreshTokenHash);
        _identityDbContext.RefreshTokens.Update(storedToken);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(issued.Response);
    }

    public async Task<Result> LogoutAsync(string refreshToken, string? clientIp, CancellationToken ct = default)
    {
        var storedToken = await _refreshTokenStore.FindByTokenHashAsync(_tokenService.HashRefreshToken(refreshToken), ct);

        if (storedToken is null)
        {
            // خروج باید ایمن (idempotent) باشد.
            return Result.Success();
        }

        await _refreshTokenStore.RevokeFamilyAsync(storedToken.FamilyId, clientIp, ct);
        return Result.Success();
    }

    public async Task<Result> RevokeAllSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        await _refreshTokenStore.RevokeAllForUserAsync(userId, revokedByIp: null, ct);
        return Result.Success();
    }

    /// <summary>
    /// استخراج تمام مجوزهای کاربر از کلیم نقش‌های او.
    /// </summary>
    private async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(ApplicationUser user, CancellationToken ct)
    {
        var roleIds = await _identityDbContext.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        var permissions = await _identityDbContext.RoleClaims
            .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == Permissions.ClaimType)
            .Select(rc => rc.ClaimValue!)
            .Distinct()
            .ToListAsync(ct);

        return permissions;
    }

    /// <summary>ساخت پروفایل کاربر برای پاسخ ورود.</summary>
    private static Task<UserProfileDto> BuildProfileAsync(
        ApplicationUser user, IList<string> roles, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        return Task.FromResult(new UserProfileDto
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
}
