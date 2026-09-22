using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Domain.Common;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Persistence;
using ODCC.Infrastructure.Services;
using Xunit;

namespace ODCC.Infrastructure.Tests.Services;

/// <summary>
/// آزمون‌های داده‌ی اولیه (bootstrap): نقش مدیر کل، کاربر مدیر کل و
/// واحد سازمانی ریشه. همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند
/// و هیچ پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class DataSeederTests
{
    /// <summary>ساخت یک seeder با گزینه‌های دلخواه آزمون.</summary>
    private static DataSeeder CreateSeeder(TestEnvironment env, Action<SeedOptions>? configure = null)
    {
        var options = new SeedOptions
        {
            Enabled = true,
            AdminUserName = "admin",
            AdminEmail = "admin@test.local",
            AdminPassword = "Admin1234!",
            AdminRoleName = "Admin",
            RootOrgUnitCode = "HQ",
            RootOrgUnitName = "سازمان آزمون"
        };

        configure?.Invoke(options);

        return new DataSeeder(
            env.Services.GetRequiredService<RoleManager<ApplicationRole>>(),
            env.Services.GetRequiredService<UserManager<ApplicationUser>>(),
            env.Services.GetRequiredService<IOrgUnitRepository>(),
            env.Services.GetRequiredService<IOrganizationUnitOfWork>(),
            Options.Create(options),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSeeder>.Instance);
    }

    [Fact]
    public async Task Seed_Creates_Root_Org_Unit_Admin_Role_And_Admin_User()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var seeder = CreateSeeder(env);

        await seeder.SeedAsync();

        // واحد سازمانی ریشه.
        var unit = await env.OrganizationDbContext.OrgUnits.SingleAsync();
        unit.Code.Should().Be("HQ");
        unit.Path.Should().Be("/HQ");
        unit.Level.Should().Be(0);
        unit.ParentId.Should().BeNull();

        // نقش مدیر کل با تمام مجوزها.
        var roleManager = env.Services.GetRequiredService<RoleManager<ApplicationRole>>();
        var role = await roleManager.FindByNameAsync("Admin");
        role.Should().NotBeNull();
        role!.IsSystem.Should().BeTrue();

        var claims = await roleManager.GetClaimsAsync(role);
        var permissions = claims.Where(c => c.Type == Permissions.ClaimType).Select(c => c.Value).ToList();
        permissions.Should().BeEquivalentTo(Permissions.All);

        // کاربر مدیر کل.
        var userManager = env.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync("admin");
        user.Should().NotBeNull();
        user!.Email.Should().Be("admin@test.local");
        user.EmailConfirmed.Should().BeTrue();
        user.IsActive.Should().BeTrue();
        user.DataScope.Should().Be(DataScope.Company);
        user.OrgUnitId.Should().Be(unit.Id);

        var roles = await userManager.GetRolesAsync(user);
        roles.Should().ContainSingle().Which.Should().Be("Admin");
    }

    [Fact]
    public async Task Seed_Is_Idempotent_Running_Twice_Changes_Nothing()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var seeder = CreateSeeder(env);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var unitCount = await env.OrganizationDbContext.OrgUnits.CountAsync();
        unitCount.Should().Be(1, "واحد ریشه نباید دوباره ایجاد شود");

        var roleCount = await env.IdentityDbContext.Roles.CountAsync();
        roleCount.Should().Be(1, "نقش مدیر کل نباید دوباره ایجاد شود");

        var userCount = await env.IdentityDbContext.Users.CountAsync();
        userCount.Should().Be(1, "کاربر مدیر کل نباید دوباره ایجاد شود");

        // کلیم‌های مجوز نباید دو بار اضافه شده باشند.
        var roleManager = env.Services.GetRequiredService<RoleManager<ApplicationRole>>();
        var role = await roleManager.FindByNameAsync("Admin");
        var permissionClaims = (await roleManager.GetClaimsAsync(role!))
            .Where(c => c.Type == Permissions.ClaimType);
        permissionClaims.Should().HaveCount(Permissions.All.Count);
    }

    [Fact]
    public async Task Seed_Without_Admin_Password_Skips_Admin_User_But_Creates_Root_Unit()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var seeder = CreateSeeder(env, o => o.AdminPassword = string.Empty);

        await seeder.SeedAsync();

        // واحد ریشه و نقش همچنان ایجاد می‌شوند.
        (await env.OrganizationDbContext.OrgUnits.CountAsync()).Should().Be(1);
        (await env.IdentityDbContext.Roles.CountAsync()).Should().Be(1);

        // اما کاربر مدیر کل ایجاد نمی‌شود (رفتار ایمن).
        var userManager = env.Services.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByNameAsync("admin")).Should().BeNull();
    }

    [Fact]
    public async Task Seed_When_Disabled_Creates_Nothing()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var seeder = CreateSeeder(env, o => o.Enabled = false);

        await seeder.SeedAsync();

        (await env.OrganizationDbContext.OrgUnits.CountAsync()).Should().Be(0);
        (await env.IdentityDbContext.Roles.CountAsync()).Should().Be(0);
        (await env.IdentityDbContext.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Seed_Preserves_Existing_Root_Unit_With_Different_Code()
    {
        await using var env = await TestEnvironment.CreateAsync();

        // یک واحد ریشه از قبل وجود دارد.
        var existing = new ODCC.Domain.Modules.Organization.Entities.OrgUnit
        {
            Code = "ACME",
            Name = "شرکت نمونه",
            Type = ODCC.Domain.Modules.Organization.Enums.OrgUnitType.Company
        };
        existing.SetPath(parentPath: null);
        env.OrganizationDbContext.OrgUnits.Add(existing);
        await env.OrganizationDbContext.SaveChangesAsync();

        var seeder = CreateSeeder(env);
        await seeder.SeedAsync();

        // واحد جدیدی نباید اضافه شود.
        var units = await env.OrganizationDbContext.OrgUnits.ToListAsync();
        units.Should().ContainSingle().Which.Code.Should().Be("ACME");

        // اما کاربر مدیر کل همچنان به واحد موجود متصل می‌شود.
        var userManager = env.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByNameAsync("admin");
        admin!.OrgUnitId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Seed_Admin_User_Can_Login_With_Issued_Permissions()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var seeder = CreateSeeder(env);
        await seeder.SeedAsync();

        var authService = env.Services.GetRequiredService<IAuthService>();
        var result = await authService.LoginAsync(new LoginRequest
        {
            UserName = "admin",
            Password = "Admin1234!"
        }, "127.0.0.1");

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresMfa.Should().BeFalse();
        // کاربر مدیر کل تمام مجوزها را در توکن خود دارد.
        result.Value.Tokens!.Profile!.Permissions.Should().BeEquivalentTo(Permissions.All);
        result.Value.Tokens.Profile.Roles.Should().Contain("Admin");
    }
}
