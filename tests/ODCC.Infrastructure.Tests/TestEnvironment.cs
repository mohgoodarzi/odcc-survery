using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Enums;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Services;
using ODCC.Infrastructure.Modules.Audit.EventListeners;
using ODCC.Infrastructure.Repositories.Audit;
using ODCC.Infrastructure.Modules.Identity;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;
using ODCC.Infrastructure.Modules.Identity.Repositories;
using ODCC.Infrastructure.Modules.Identity.Services;
using ODCC.Infrastructure.Modules.Organization.Persistence;
using ODCC.Infrastructure.Modules.Organization.Repositories;
using ODCC.Infrastructure.Modules.Organization.Services;
using ODCC.Infrastructure.Persistence;
using ODCC.Infrastructure.Persistence.Audit;
using ODCC.Infrastructure.Services;

namespace ODCC.Infrastructure.Tests;

/// <summary>
/// سازنده‌ی محیط آزمون. تمام وابستگی‌های واقعی را با SQLite درون‌حافظه‌ای
/// سرو می‌دهد که در پایان آزمون دور ریخته می‌شود.
///
/// <b>توجه:</b> این کلاس هیچ پایگاه‌داده‌ی واقعی ایجاد نمی‌کند. SQLite
/// درون‌حافظه‌ای فقط در طول عمر اتصال باز می‌ماند و با Dispose آن نابود می‌شود.
/// از <c>EnsureCreatedAsync</c> به‌جای مهاجرت‌ها استفاده می‌شود تا هیچ
/// فایل یا پایگاه‌داده‌ی واقعی لمس نشود.
/// </summary>
public sealed class TestEnvironment : IAsyncDisposable
{
    public IServiceProvider Services { get; }
    public IdentityDbContext IdentityDbContext { get; }
    public OrganizationDbContext OrganizationDbContext { get; }

    private readonly SqliteConnection _identityConnection;
    private readonly SqliteConnection _organizationConnection;
    private readonly SqliteConnection _auditConnection;

    private TestEnvironment(
        IServiceProvider services,
        IdentityDbContext identityDbContext,
        OrganizationDbContext organizationDbContext,
        SqliteConnection identityConnection,
        SqliteConnection organizationConnection,
        SqliteConnection auditConnection)
    {
        Services = services;
        IdentityDbContext = identityDbContext;
        OrganizationDbContext = organizationDbContext;
        _identityConnection = identityConnection;
        _organizationConnection = organizationConnection;
        _auditConnection = auditConnection;
    }

    /// <summary>
    /// ساخت یک محیط آزمون با کاربر پیش‌فرض اختیاری.
    /// </summary>
    /// <param name="jwtSecret">کلید امضای توکن (پیش‌فرض: کلید آزمون).</param>
    public static async Task<TestEnvironment> CreateAsync(string? jwtSecret = null)
    {
        var identityConnection = new SqliteConnection("DataSource=:memory:");
        await identityConnection.OpenAsync();

        var organizationConnection = new SqliteConnection("DataSource=:memory:");
        await organizationConnection.OpenAsync();

        var auditConnection = new SqliteConnection("DataSource=:memory:");
        await auditConnection.OpenAsync();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlite(identityConnection));

        services.AddDbContext<OrganizationDbContext>(options =>
            options.UseSqlite(organizationConnection));

        services.AddDbContext<AuditDbContext>(options =>
            options.UseSqlite(auditConnection));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        // کلید امضای آزمون: طول آن حداقل ۳۲ کاراکتر است و هرگز در production نیست.
        var jwtOptions = Options.Create(new JwtOptions
        {
            Secret = jwtSecret ?? "test-signing-key-for-unit-tests-only-32+chars!",
            Issuer = "odcc-survey-test",
            Audience = "odcc-survey-test-web",
            AccessExpirationMinutes = 15,
            RefreshExpirationDays = 7
        });

        services.AddSingleton(jwtOptions);
        services.AddScoped<ITokenService, JwtTokenService>();

        // سرویس‌های جاری کاربر با پیاده‌سازی قابل‌تنظیم آزمون.
        services.AddScoped<TestCurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<TestCurrentUserService>());

        services.AddScoped<IOrgScopeProvider, OrgScopeProvider>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Organization.Events.OrgUnitCreatedEvent>, OrganizationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Organization.Events.OrgUnitUpdatedEvent>, OrganizationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Organization.Events.OrgUnitDeletedEvent>, OrganizationAuditEventListener>();
        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IOrganizationUnitOfWork, OrganizationUnitOfWork>();

        services.AddScoped<IOrgUnitRepository, OrgUnitRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();

        services.AddScoped<IOrgUnitService, OrgUnitService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IAuthService, AuthService>();

        // داده‌ی اولیه (bootstrap). رمز عبور آزمون هرگز در production نیست.
        services.AddSingleton(Options.Create(new SeedOptions
        {
            Enabled = true,
            AdminUserName = "admin",
            AdminEmail = "admin@test.local",
            AdminPassword = "Admin1234!",
            AdminFirstName = "مدیر",
            AdminLastName = "آزمون",
            RootOrgUnitCode = "HQ",
            RootOrgUnitName = "سازمان آزمون"
        }));
        services.AddScoped<IDataSeeder, DataSeeder>();

        var provider = services.BuildServiceProvider();

        var identityDbContext = provider.GetRequiredService<IdentityDbContext>();
        var organizationDbContext = provider.GetRequiredService<OrganizationDbContext>();
        var auditDbContext = provider.GetRequiredService<AuditDbContext>();

        await identityDbContext.Database.EnsureCreatedAsync();
        await organizationDbContext.Database.EnsureCreatedAsync();
        await auditDbContext.Database.EnsureCreatedAsync();

        return new TestEnvironment(provider, identityDbContext, organizationDbContext,
            identityConnection, organizationConnection, auditConnection);
    }

    /// <summary>
    /// تعیین کاربر جاری برای درخواست‌های بعدی (بدون نیاز به HTTP واقعی).
    /// </summary>
    public (TestCurrentUserService current, Guid userId) SetCurrentUser(
        Guid? orgUnitId,
        DataScope dataScope,
        string userName = "testuser",
        params string[] permissions)
    {
        var current = Services.GetRequiredService<TestCurrentUserService>();
        var userId = Guid.NewGuid();
        current.UserId = userId;
        current.UserName = userName;
        current.IsAuthenticated = true;
        current.OrgUnitId = orgUnitId;
        current.DataScope = dataScope;
        current.Permissions = permissions;
        return (current, userId);
    }

    /// <summary>ساخت ساختار سازمانی نمونه: شرکت → دایرکتی → دپارتمان → تیم.</summary>
    public async Task<(Guid companyId, Guid divisionId, Guid departmentId, Guid teamId, Guid otherDivisionId, Guid otherDepartmentId)>
        SeedOrgHierarchyAsync()
    {
        var company = NewUnit("hq", "شرکت اصلی", OrgUnitType.Company, null);
        var division = NewUnit("fin", "امور مالی", OrgUnitType.Division, company);
        var department = NewUnit("ap", "حساب‌های پرداختنی", OrgUnitType.Department, division);
        var team = NewUnit("inv", "فاکتورها", OrgUnitType.Team, department);
        var otherDivision = NewUnit("ops", "عملیات", OrgUnitType.Division, company);
        var otherDepartment = NewUnit("it", "فناوری اطلاعات", OrgUnitType.Department, otherDivision);

        OrganizationDbContext.OrgUnits.AddRange(company, division, department, team, otherDivision, otherDepartment);
        await OrganizationDbContext.SaveChangesAsync();

        return (company.Id, division.Id, department.Id, team.Id, otherDivision.Id, otherDepartment.Id);
    }

    private static ODCC.Domain.Modules.Organization.Entities.OrgUnit NewUnit(
        string code, string name, OrgUnitType type,
        ODCC.Domain.Modules.Organization.Entities.OrgUnit? parent)
    {
        var unit = new ODCC.Domain.Modules.Organization.Entities.OrgUnit
        {
            Code = code,
            Name = name,
            Type = type,
            ParentId = parent?.Id
        };
        unit.SetPath(parent?.Path);
        return unit;
    }

    public async ValueTask DisposeAsync()
    {
        await IdentityDbContext.DisposeAsync();
        await OrganizationDbContext.DisposeAsync();
        await _identityConnection.DisposeAsync();
        await _organizationConnection.DisposeAsync();
        await _auditConnection.DisposeAsync();
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// پیاده‌سازی قابل‌تنظیم ICurrentUserService برای آزمون. مقادیر به‌صورت مستقیم
/// تنظیم می‌شوند تا نیازی به HttpContext واقعی نباشد.
/// </summary>
public sealed class TestCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public bool IsAuthenticated { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public IReadOnlyCollection<string> Permissions { get; set; } = [];
    public Guid? OrgUnitId { get; set; }
    public DataScope DataScope { get; set; } = DataScope.Own;
    public string? ClientIpAddress { get; set; } = "127.0.0.1";

    public bool IsInRole(params string[] roles) => roles.Any(r => Roles.Contains(r));
    public bool HasPermission(string permission) => Permissions.Contains(permission);
    public bool HasPermission(Permissions.PermissionKey permission) => HasPermission(permission.Value);
}
