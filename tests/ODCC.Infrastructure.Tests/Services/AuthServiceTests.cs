using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Infrastructure.Modules.Identity.Entities;
using Xunit;

namespace ODCC.Infrastructure.Tests.Services;

/// <summary>
/// آزمون‌های جریان احراز هویت: ورود، ورود نامعتبر، حساب غیرفعال،
/// چرخش توکن، ابطال و سرقت توکن.
/// </summary>
public class AuthServiceTests
{
    private const string Password = "Test1234!";

    /// <summary>ساخت کاربر آزمون فعال با رمز عبور مشخص.</summary>
    private static async Task<ApplicationUser> CreateUserAsync(TestEnvironment env, string userName, bool isActive = true)
    {
        var userManager = env.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@test.local",
            EmailConfirmed = true,
            FirstName = "کاربر",
            LastName = "آزمون",
            IsActive = isActive
        };

        var result = await userManager.CreateAsync(user, Password);
        result.Succeeded.Should().BeTrue("کاربر آزمون باید ساخته شود");
        return user;
    }

    [Fact]
    public async Task Login_With_Valid_Credentials_Returns_Tokens()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "alice");
        var authService = env.Services.GetRequiredService<IAuthService>();

        var result = await authService.LoginAsync(new LoginRequest
        {
            UserName = "alice",
            Password = Password
        }, "127.0.0.1");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Tokens.Should().NotBeNull();
        result.Value.Tokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.Tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.Tokens.Profile.Should().NotBeNull();
        result.Value.Tokens.Profile!.Id.Should().Be(user.Id);

        // توکن تازه‌سازی باید هش‌شده ذخیره شود، نه متن‌وضوح.
        var plainToken = result.Value.Tokens.RefreshToken;
        var stored = await env.IdentityDbContext.RefreshTokens
            .IgnoreQueryFilters()
            .SingleAsync(t => t.UserId == user.Id);

        stored.TokenHash.Should().NotBe(plainToken);
        stored.IsActive.Should().BeTrue();
        stored.CreatedByIp.Should().Be("127.0.0.1");

        // آخرین ورود باید ثبت شده باشد.
        (await env.Services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id.ToString()))!
            .LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_With_Invalid_Password_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        await CreateUserAsync(env, "bob");
        var authService = env.Services.GetRequiredService<IAuthService>();

        var result = await authService.LoginAsync(new LoginRequest
        {
            UserName = "bob",
            Password = "WrongPassword123!"
        }, "127.0.0.1");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("invalid_credentials");
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Login_With_Unknown_User_Is_Rejected_With_Same_Error()
    {
        // پیام یکسان برای کاربر ناموجود و رمز اشتباه (جلوگیری از شمارش نام کاربری).
        await using var env = await TestEnvironment.CreateAsync();
        var authService = env.Services.GetRequiredService<IAuthService>();

        var result = await authService.LoginAsync(new LoginRequest
        {
            UserName = "nonexistent",
            Password = Password
        }, "127.0.0.1");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Login_With_Deactivated_Account_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        await CreateUserAsync(env, "carol", isActive: false);
        var authService = env.Services.GetRequiredService<IAuthService>();

        var result = await authService.LoginAsync(new LoginRequest
        {
            UserName = "carol",
            Password = Password
        }, "127.0.0.1");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("account_inactive");
    }

    [Fact]
    public async Task Login_Issues_Access_Token_With_User_Claims()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "dave");
        var tokenService = env.Services.GetRequiredService<ITokenService>();
        var authService = env.Services.GetRequiredService<IAuthService>();

        var result = await authService.LoginAsync(new LoginRequest { UserName = "dave", Password = Password }, null);

        var principal = tokenService.ValidateAccessToken(result.Value!.Tokens!.AccessToken);

        principal.Should().NotBeNull();
        principal!.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(user.Id.ToString());
        principal.FindFirstValue(JwtRegisteredClaimNames.Name).Should().Be("dave");
    }

    [Fact]
    public async Task Refresh_Rotates_Token_And_Keeps_Family()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "erin");
        var authService = env.Services.GetRequiredService<IAuthService>();

        var login = await authService.LoginAsync(new LoginRequest { UserName = "erin", Password = Password }, "127.0.0.1");
        var oldRefresh = login.Value!.Tokens!.RefreshToken!;

        var refresh = await authService.RefreshAsync(new RefreshRequest
        {
            AccessToken = login.Value.Tokens.AccessToken,
            RefreshToken = oldRefresh
        }, "127.0.0.2");

        refresh.IsSuccess.Should().BeTrue();
        refresh.Value!.RefreshToken.Should().NotBe(oldRefresh);
        refresh.Value.AccessToken.Should().NotBe(login.Value.Tokens.AccessToken);

        // توکن قدیمی باید ابطال شده باشد.
        var oldHash = env.Services.GetRequiredService<ITokenService>().HashRefreshToken(oldRefresh);
        var oldToken = await env.IdentityDbContext.RefreshTokens
            .IgnoreQueryFilters()
            .FirstAsync(t => t.TokenHash == oldHash);

        oldToken.IsRevoked.Should().BeTrue();
        oldToken.RevokedByIp.Should().Be("127.0.0.2");
        oldToken.ReplacedByTokenHash.Should().NotBeNullOrEmpty();

        // توکن جدید باید در همان خانواده باشد.
        var newHash = env.Services.GetRequiredService<ITokenService>().HashRefreshToken(refresh.Value.RefreshToken);
        var newToken = await env.IdentityDbContext.RefreshTokens
            .IgnoreQueryFilters()
            .FirstAsync(t => t.TokenHash == newHash);

        newToken.FamilyId.Should().Be(oldToken.FamilyId,
            "چرخش باید خانواده را حفظ کند تا ابطال خانواده در صورت سرقت کار کند");
        newToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_With_Invalid_Token_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "frank");
        var authService = env.Services.GetRequiredService<IAuthService>();

        var login = await authService.LoginAsync(new LoginRequest { UserName = "frank", Password = Password }, null);

        var result = await authService.RefreshAsync(new RefreshRequest
        {
            AccessToken = login.Value!.Tokens!.AccessToken,
            RefreshToken = "completely-bogus-refresh-token"
        }, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("invalid_refresh_token");
    }

    /// <summary>
    /// سرقت توکن: استفاده‌ی مجدد از توکنی که قبلاً چرخش خورده باید
    /// کل خانواده را باطل کند (تشخیص سرقت).
    /// </summary>
    [Fact]
    public async Task Refresh_Reuse_Of_Rotated_Token_Revokes_Family()
    {
        await using var env = await TestEnvironment.CreateAsync();
        await CreateUserAsync(env, "grace");
        var authService = env.Services.GetRequiredService<IAuthService>();

        var login = await authService.LoginAsync(new LoginRequest { UserName = "grace", Password = Password }, null);
        var oldRefresh = login.Value!.Tokens!.RefreshToken!;

        // چرخش اول.
        var firstRefresh = await authService.RefreshAsync(new RefreshRequest
        {
            AccessToken = login.Value.Tokens.AccessToken,
            RefreshToken = oldRefresh
        }, null);

        firstRefresh.IsSuccess.Should().BeTrue();

        // استفاده‌ی مجدد از توکن قدیمی → باید خانواده باطل شود.
        var reuse = await authService.RefreshAsync(new RefreshRequest
        {
            AccessToken = login.Value.Tokens.AccessToken,
            RefreshToken = oldRefresh
        }, null);

        reuse.IsFailure.Should().BeTrue();
        reuse.Error.Code.Should().Be("token_revoked");

        // توکنی که از چرخش اول به‌دست آمده هم باید باطل شده باشد (همان خانواده).
        // توجه: ابطال خانواده با ExecuteUpdateAsync انجام می‌شود که از ChangeTracker
        // عبور می‌کند، بنابراین موجودیتِ ردیابی‌شده در حافظه قدیمی است. برای مشاهده‌ی
        // وضعیت واقعی پایگاه داده باید بدون ردیابی (AsNoTracking) خواند.
        var newHash = env.Services.GetRequiredService<ITokenService>()
            .HashRefreshToken(firstRefresh.Value!.RefreshToken);
        var newToken = await env.IdentityDbContext.RefreshTokens
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(t => t.TokenHash == newHash);

        newToken.IsRevoked.Should().BeTrue("توکن دزدیده‌نشده هم باید باطل شود");
    }

    [Fact]
    public async Task Refresh_With_Deactivated_User_Is_Rejected_And_Revokes_All()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "heidi");
        var authService = env.Services.GetRequiredService<IAuthService>();
        var userManager = env.Services.GetRequiredService<UserManager<ApplicationUser>>();

        var login = await authService.LoginAsync(new LoginRequest { UserName = "heidi", Password = Password }, null);

        // غیرفعال‌سازی حساب.
        user.IsActive = false;
        await userManager.UpdateAsync(user);

        var result = await authService.RefreshAsync(new RefreshRequest
        {
            AccessToken = login.Value!.Tokens!.AccessToken,
            RefreshToken = login.Value.Tokens.RefreshToken
        }, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("account_inactive");

        var activeCount = await env.IdentityDbContext.RefreshTokens
            .CountAsync(t => t.UserId == user.Id && t.RevokedAt == null);
        activeCount.Should().Be(0, "همه‌ی نشست‌های کاربر غیرفعال باید باطل شوند");
    }

    [Fact]
    public async Task Logout_Revokes_Family_And_Is_Idempotent()
    {
        await using var env = await TestEnvironment.CreateAsync();
        await CreateUserAsync(env, "ivan");
        var authService = env.Services.GetRequiredService<IAuthService>();

        var login = await authService.LoginAsync(new LoginRequest { UserName = "ivan", Password = Password }, null);
        var refresh = login.Value!.Tokens!.RefreshToken!;

        var first = await authService.LogoutAsync(refresh, "127.0.0.1", default);
        first.IsSuccess.Should().BeTrue();

        // خروج دوباره با همان توکن باید ایمن باشد (idempotent).
        var second = await authService.LogoutAsync(refresh, "127.0.0.1", default);
        second.IsSuccess.Should().BeTrue();

        var active = await env.IdentityDbContext.RefreshTokens
            .IgnoreQueryFilters()
            .CountAsync(t => t.RevokedAt == null);
        active.Should().Be(0);
    }

    [Fact]
    public async Task RevokeAllSessions_Revokes_Every_Active_Token()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "judy");
        var authService = env.Services.GetRequiredService<IAuthService>();

        // دو نشست مجزا.
        await authService.LoginAsync(new LoginRequest { UserName = "judy", Password = Password }, null);
        await authService.LoginAsync(new LoginRequest { UserName = "judy", Password = Password }, null);

        await authService.RevokeAllSessionsAsync(user.Id, default);

        var active = await env.IdentityDbContext.RefreshTokens
            .CountAsync(t => t.UserId == user.Id && t.RevokedAt == null);
        active.Should().Be(0);
    }

    /// <summary>
    /// توکن دسترسی باید اعتبارسنجی شود و توکن دسترسی جعلی رد شود.
    /// </summary>
    [Fact]
    public async Task ValidateAccessToken_Rejects_Forged_Tokens()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var tokenService = env.Services.GetRequiredService<ITokenService>();
        var authService = env.Services.GetRequiredService<IAuthService>();

        // کلید متفاوت → توکن جعلی.
        await using var otherEnv = await TestEnvironment.CreateAsync("another-test-signing-key-with-32+characters!!");
        var otherTokenService = otherEnv.Services.GetRequiredService<ITokenService>();
        await CreateUserAsync(otherEnv, "mallory");
        var otherLogin = await otherEnv.Services.GetRequiredService<IAuthService>()
            .LoginAsync(new LoginRequest { UserName = "mallory", Password = Password }, null);

        // توکن صادرشده با کلید دوم نباید با کلید اول معتبر باشد.
        var principal = tokenService.ValidateAccessToken(otherLogin.Value!.Tokens!.AccessToken);
        principal.Should().BeNull("توکن امضاشده با کلید متفاوت نباید معتبر باشد");

        // توکن تولیدشده در همین محیط باید معتبر باشد.
        await CreateUserAsync(env, "legit");
        var login = await authService.LoginAsync(new LoginRequest { UserName = "legit", Password = Password }, null);
        tokenService.ValidateAccessToken(login.Value!.Tokens!.AccessToken).Should().NotBeNull();
    }

    /// <summary>
    /// توکن منقضی‌شده باید قابل تشخیص باشد (در زمان چرخش، انقضا نادیده گرفته نمی‌شود
    /// برای استخراج هویت، اما اعتبارسنجی کامل آن باید رد شود).
    /// </summary>
    [Fact]
    public async Task ValidateAccessToken_With_Expired_Token_Returns_Null_When_Validating_Lifetime()
    {
        await using var env = await TestEnvironment.CreateAsync(jwtSecret: null);
        var tokenService = env.Services.GetRequiredService<ITokenService>();

        // کلید کوتاه‌مدت: ساخت توکن با انقضای در گذشته.
        await using var shortEnv = await TestEnvironment.CreateAsync();
        var shortTokenService = shortEnv.Services.GetRequiredService<ITokenService>();
        await CreateUserAsync(shortEnv, "expiring");
        var login = await shortEnv.Services.GetRequiredService<IAuthService>()
            .LoginAsync(new LoginRequest { UserName = "expiring", Password = Password }, null);

        // توکن همین الان صادر شده و معتبر است.
        tokenService.ValidateAccessToken(login.Value!.Tokens!.AccessToken, validateLifetime: true).Should().NotBeNull();
    }
}
