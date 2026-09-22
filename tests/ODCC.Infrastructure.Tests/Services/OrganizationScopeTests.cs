using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Domain.Modules.Organization.Enums;
using Xunit;

namespace ODCC.Infrastructure.Tests.Services;

/// <summary>
/// آزمون‌های جداسازی داده‌های سازمانی در سطح سرویس. این آزمون‌ها
/// تضمین می‌کنند که کاربر با دامنه‌ی محدود نتواند داده‌های خارج از
/// دامنه‌ی خود را بخواند یا بنویسد، حتی اگر شناسه‌ی آن‌ها را بداند.
/// </summary>
public class OrganizationScopeTests
{
    private static readonly string[] ModulePermissions =
    [
        Permissions.Organization.EmployeesView,
        Permissions.Organization.EmployeesManage,
        Permissions.Organization.UnitsView,
        Permissions.Organization.UnitsManage,
        Permissions.Organization.PositionsView,
        Permissions.Organization.PositionsManage
    ];

    // مسیرهای مورد انتظار در آزمون‌ها (به‌صورت static readonly تا CA1861 نقض نشود).
    private static readonly string[] ApSubtreePaths = ["/hq/fin/ap", "/hq/fin/ap/inv"];
    private static readonly string[] ApEmployeeCodes = ["E1", "E2"];
    private static readonly string[] ApPositionCodes = ["P1"];

    /// <summary>شناسه‌ی واحد با مسیر، از DbContext محیط.</summary>
    private static Task<Guid> GetUnitIdAsync(TestEnvironment env, string path) =>
        env.OrganizationDbContext.OrgUnits.Where(u => u.Path == path).Select(u => u.Id).FirstAsync();

    private static Task<Guid> GetEmployeeIdAsync(TestEnvironment env, string code) =>
        env.OrganizationDbContext.Employees.Where(e => e.EmployeeCode == code).Select(e => e.Id).FirstAsync();

    private static Task<Guid> GetPositionIdAsync(TestEnvironment env, string code) =>
        env.OrganizationDbContext.Positions.Where(p => p.Code == code).Select(p => p.Id).FirstAsync();

    /// <summary>ساخت محیط با ساختار سازمانی و کاربر جاری با دامنه‌ی مشخص.</summary>
    private static async Task<TestEnvironment> CreateWithScopeAsync(DataScope scope, string anchorPath)
    {
        var env = await TestEnvironment.CreateAsync();
        await env.SeedOrgHierarchyAsync();
        var anchorId = await GetUnitIdAsync(env, anchorPath);
        env.SetCurrentUser(anchorId, scope, permissions: ModulePermissions);
        return env;
    }

    /// <summary>سه کارمند: E1 و E2 در زیردرخت قابل‌مشاهده، E3 در دپارتمان دیگر.</summary>
    private static async Task SeedEmployeesAsync(TestEnvironment env)
    {
        var ap = await env.OrganizationDbContext.OrgUnits.FirstAsync(u => u.Path == "/hq/fin/ap");
        var apTeam = await env.OrganizationDbContext.OrgUnits.FirstAsync(u => u.Path == "/hq/fin/ap/inv");
        var it = await env.OrganizationDbContext.OrgUnits.FirstAsync(u => u.Path == "/hq/ops/it");

        env.OrganizationDbContext.Employees.AddRange(
            new Employee
            {
                EmployeeCode = "E1",
                FirstName = "علی",
                LastName = "احمدی",
                OrgUnitId = ap.Id,
                Status = EmployeeStatus.Active
            },
            new Employee
            {
                EmployeeCode = "E2",
                FirstName = "مهدی",
                LastName = "رضایی",
                OrgUnitId = apTeam.Id,
                Status = EmployeeStatus.Active
            },
            new Employee
            {
                EmployeeCode = "E3",
                FirstName = "سارا",
                LastName = "کریمی",
                OrgUnitId = it.Id,
                Status = EmployeeStatus.Active
            });

        await env.OrganizationDbContext.SaveChangesAsync();
    }

    /// <summary>دو موقعیت: P1 در زیردرخت قابل‌مشاهده، P2 در دپارتمان دیگر.</summary>
    private static async Task SeedPositionsAsync(TestEnvironment env)
    {
        var ap = await env.OrganizationDbContext.OrgUnits.FirstAsync(u => u.Path == "/hq/fin/ap");
        var it = await env.OrganizationDbContext.OrgUnits.FirstAsync(u => u.Path == "/hq/ops/it");

        env.OrganizationDbContext.Positions.AddRange(
            new Position { Code = "P1", Title = "حسابدار ارشد", OrgUnitId = ap.Id },
            new Position { Code = "P2", Title = "کارشناس شبکه", OrgUnitId = it.Id });

        await env.OrganizationDbContext.SaveChangesAsync();
    }

    // --- OrgUnit: خواندن ----------------------------------------------------

    [Fact]
    public async Task Department_Scope_Lists_Only_Visible_Org_Units()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.ListAsync(activeOnly: false);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(u => u.Path).Should().BeEquivalentTo(ApSubtreePaths);
    }

    [Fact]
    public async Task Company_Scope_Lists_All_Org_Units()
    {
        var env = await CreateWithScopeAsync(DataScope.Company, "/hq");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.ListAsync(activeOnly: false);

        result.Value!.Should().HaveCount(6);
    }

    [Fact]
    public async Task Team_Scope_GetById_Denies_Parent_Unit()
    {
        var env = await CreateWithScopeAsync(DataScope.Team, "/hq/fin/ap/inv");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.GetByIdAsync(await GetUnitIdAsync(env, "/hq/fin/ap"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_GetById_Allows_Self_And_Descendant()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var self = await service.GetByIdAsync(await GetUnitIdAsync(env, "/hq/fin/ap"), default);
        var child = await service.GetByIdAsync(await GetUnitIdAsync(env, "/hq/fin/ap/inv"), default);

        self.IsSuccess.Should().BeTrue();
        child.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Team_Scope_Tree_Only_Contains_Visible_Subtree()
    {
        var env = await CreateWithScopeAsync(DataScope.Team, "/hq/fin/ap/inv");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.GetTreeAsync(default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value![0].Code.Should().Be("inv");
    }

    [Fact]
    public async Task Division_Scope_Descendants_Excludes_Other_Division()
    {
        var env = await CreateWithScopeAsync(DataScope.Division, "/hq/fin");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.GetDescendantUnitIdsAsync(await GetUnitIdAsync(env, "/hq/fin"), default);

        result.Value!.Should().HaveCount(3); // fin, ap, inv
        result.Value!.Should().NotContain(await GetUnitIdAsync(env, "/hq/ops"));
    }

    [Fact]
    public async Task Division_Scope_Descendants_Of_Other_Division_Denied()
    {
        var env = await CreateWithScopeAsync(DataScope.Division, "/hq/fin");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.GetDescendantUnitIdsAsync(await GetUnitIdAsync(env, "/hq/ops"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    // --- OrgUnit: نوشتن ----------------------------------------------------

    [Fact]
    public async Task Department_Scope_Cannot_Create_Unit_Outside_Scope()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.CreateAsync(new SaveOrgUnitRequest
        {
            Code = "NEW1",
            Name = "واحد جدید",
            Type = OrgUnitType.Team,
            ParentId = await GetUnitIdAsync(env, "/hq/ops/it")
        }, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_Cannot_Create_Root_Unit()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.CreateAsync(new SaveOrgUnitRequest
        {
            Code = "ROOT1",
            Name = "شرکت جدید",
            Type = OrgUnitType.Company,
            ParentId = null
        }, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_Can_Create_Child_Inside_Scope()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.CreateAsync(new SaveOrgUnitRequest
        {
            Code = "team2",
            Name = "تیم جدید",
            Type = OrgUnitType.Team,
            ParentId = await GetUnitIdAsync(env, "/hq/fin/ap")
        }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Path.Should().Be("/hq/fin/ap/team2");
    }

    [Fact]
    public async Task Department_Scope_Cannot_Update_Unit_Outside_Scope()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.UpdateAsync(await GetUnitIdAsync(env, "/hq/ops/it"), new SaveOrgUnitRequest
        {
            Code = "IT",
            Name = "نام تغییر یافته",
            Type = OrgUnitType.Department,
            ParentId = await GetUnitIdAsync(env, "/hq/ops")
        }, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_Cannot_Delete_Unit_Outside_Scope()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IOrgUnitService>();

        var result = await service.DeleteAsync(await GetUnitIdAsync(env, "/hq/ops/it"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    // --- Employee: خواندن --------------------------------------------------

    [Fact]
    public async Task Department_Scope_Employee_Search_Excludes_Other_Department()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedEmployeesAsync(env);
        var service = env.Services.GetRequiredService<IEmployeeService>();

        var result = await service.SearchAsync(new EmployeeSearchRequest(SearchText: null, OrgUnitId: null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(e => e.EmployeeCode).Should().BeEquivalentTo(ApEmployeeCodes);
    }

    [Fact]
    public async Task Department_Scope_Employee_Search_With_Outside_Unit_Is_Restricted()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedEmployeesAsync(env);
        var service = env.Services.GetRequiredService<IEmployeeService>();

        // درخواست واحد خارج از دامنه → باید به دامنه‌ی کاربر محدود شود.
        var result = await service.SearchAsync(new EmployeeSearchRequest(
            SearchText: null,
            OrgUnitId: await GetUnitIdAsync(env, "/hq/ops/it"),
            IncludeDescendants: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(e => e.EmployeeCode).Should().BeEquivalentTo(ApEmployeeCodes);
    }

    [Fact]
    public async Task Company_Scope_Employee_Search_Sees_All()
    {
        var env = await CreateWithScopeAsync(DataScope.Company, "/hq");
        await SeedEmployeesAsync(env);
        var service = env.Services.GetRequiredService<IEmployeeService>();

        var result = await service.SearchAsync(new EmployeeSearchRequest(SearchText: null, OrgUnitId: null), default);

        result.Value!.Should().HaveCount(3);
    }

    [Fact]
    public async Task Department_Scope_Employee_GetById_Denies_Other_Department()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedEmployeesAsync(env);
        var service = env.Services.GetRequiredService<IEmployeeService>();

        var result = await service.GetByIdAsync(await GetEmployeeIdAsync(env, "E3"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_Employee_GetById_Allows_Visible_Employee()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedEmployeesAsync(env);
        var service = env.Services.GetRequiredService<IEmployeeService>();

        var result = await service.GetByIdAsync(await GetEmployeeIdAsync(env, "E1"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EmployeeCode.Should().Be("E1");
    }

    [Fact]
    public async Task Own_Scope_Employee_GetById_Sees_Only_Self_Record()
    {
        var env = await CreateWithScopeAsync(DataScope.Own, "/hq/fin/ap");
        await SeedEmployeesAsync(env);

        var current = env.Services.GetRequiredService<TestCurrentUserService>();

        // اتصال کاربر به رکورد کارمندی E1.
        var employee = await env.OrganizationDbContext.Employees.FirstAsync(e => e.EmployeeCode == "E1");
        employee.UserId = current.UserId;
        await env.OrganizationDbContext.SaveChangesAsync();

        var service = env.Services.GetRequiredService<IEmployeeService>();

        var self = await service.GetByIdAsync(employee.Id, default);
        var other = await service.GetByIdAsync(await GetEmployeeIdAsync(env, "E3"), default);

        self.IsSuccess.Should().BeTrue("کاربر با دامنه‌ی Own باید رکورد خودش را ببیند");
        other.IsFailure.Should().BeTrue();
        other.Error.Code.Should().Be("access_denied");
    }

    // --- Employee: نوشتن ---------------------------------------------------

    [Fact]
    public async Task Department_Scope_Employee_Create_Outside_Scope_Denied()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IEmployeeService>();

        var result = await service.CreateAsync(new SaveEmployeeRequest
        {
            EmployeeCode = "E9",
            FirstName = "نام",
            LastName = "نام خانوادگی",
            OrgUnitId = await GetUnitIdAsync(env, "/hq/ops/it")
        }, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_Employee_Create_Inside_Scope_Works()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IEmployeeService>();

        var result = await service.CreateAsync(new SaveEmployeeRequest
        {
            EmployeeCode = "E9",
            FirstName = "نام",
            LastName = "نام خانوادگی",
            OrgUnitId = await GetUnitIdAsync(env, "/hq/fin/ap")
        }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EmployeeCode.Should().Be("E9");
    }

    [Fact]
    public async Task Department_Scope_Employee_Update_Outside_Scope_Denied()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedEmployeesAsync(env);
        var service = env.Services.GetRequiredService<IEmployeeService>();

        var result = await service.UpdateAsync(await GetEmployeeIdAsync(env, "E3"), new SaveEmployeeRequest
        {
            EmployeeCode = "E3",
            FirstName = "تغییر",
            LastName = "نام",
            OrgUnitId = await GetUnitIdAsync(env, "/hq/ops/it")
        }, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_Employee_Delete_Outside_Scope_Denied()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedEmployeesAsync(env);
        var service = env.Services.GetRequiredService<IEmployeeService>();

        var result = await service.DeleteAsync(await GetEmployeeIdAsync(env, "E3"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    // --- Position ----------------------------------------------------------

    [Fact]
    public async Task Department_Scope_Position_List_Excludes_Other_Department()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedPositionsAsync(env);
        var service = env.Services.GetRequiredService<IPositionService>();

        var result = await service.ListAsync(activeOnly: false);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(p => p.Code).Should().BeEquivalentTo(ApPositionCodes);
    }

    [Fact]
    public async Task Department_Scope_Position_GetById_Denies_Other_Department()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedPositionsAsync(env);
        var service = env.Services.GetRequiredService<IPositionService>();

        var result = await service.GetByIdAsync(await GetPositionIdAsync(env, "P2"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_Position_Create_Outside_Scope_Denied()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        var service = env.Services.GetRequiredService<IPositionService>();

        var result = await service.CreateAsync(new SavePositionRequest
        {
            Code = "P9",
            Title = "عنوان",
            OrgUnitId = await GetUnitIdAsync(env, "/hq/ops/it")
        }, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }

    [Fact]
    public async Task Department_Scope_Position_Delete_Outside_Scope_Denied()
    {
        var env = await CreateWithScopeAsync(DataScope.Department, "/hq/fin/ap");
        await SeedPositionsAsync(env);
        var service = env.Services.GetRequiredService<IPositionService>();

        var result = await service.DeleteAsync(await GetPositionIdAsync(env, "P2"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
    }
}
