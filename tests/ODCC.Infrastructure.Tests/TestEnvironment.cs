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
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Infrastructure.Modules.Audit.EventListeners;
using ODCC.Infrastructure.Repositories.Audit;
using ODCC.Infrastructure.Modules.Identity;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;
using ODCC.Infrastructure.Modules.Identity.Repositories;
using ODCC.Infrastructure.Modules.Identity.Services;
using ODCC.Infrastructure.Modules.QuestionBank.EventListeners;
using ODCC.Infrastructure.Modules.QuestionBank.Persistence;
using ODCC.Infrastructure.Modules.QuestionBank.Repositories;
using ODCC.Infrastructure.Modules.QuestionBank.Services;
using ODCC.Infrastructure.Modules.Questionnaire.EventListeners;
using ODCC.Infrastructure.Modules.Questionnaire.Persistence;
using ODCC.Infrastructure.Modules.Questionnaire.Repositories;
using ODCC.Infrastructure.Modules.Questionnaire.Services;
using ODCC.Infrastructure.Modules.Survey.EventListeners;
using ODCC.Infrastructure.Modules.Survey.Persistence;
using ODCC.Infrastructure.Modules.Survey.Repositories;
using ODCC.Infrastructure.Modules.Survey.Services;
using ODCC.Infrastructure.Modules.Campaign.EventListeners;
using ODCC.Infrastructure.Modules.Campaign.Persistence;
using ODCC.Infrastructure.Modules.Campaign.Repositories;
using ODCC.Infrastructure.Modules.Campaign.Services;
using ODCC.Infrastructure.Modules.Response.EventListeners;
using ODCC.Infrastructure.Modules.Response.Persistence;
using ODCC.Infrastructure.Modules.Response.Repositories;
using ODCC.Infrastructure.Modules.Response.Services;
using ODCC.Infrastructure.Modules.Analytics.EventListeners;
using ODCC.Infrastructure.Modules.Analytics.Persistence;
using ODCC.Infrastructure.Modules.Analytics.Repositories;
using ODCC.Infrastructure.Modules.Analytics.Services;
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
    public QuestionBankDbContext QuestionBankDbContext { get; }
    public QuestionnaireDbContext QuestionnaireDbContext { get; }
    public SurveyDbContext SurveyDbContext { get; }
    public CampaignDbContext CampaignDbContext { get; }
    public ResponseDbContext ResponseDbContext { get; }
    public AnalyticsDbContext AnalyticsDbContext { get; }

    private readonly SqliteConnection _identityConnection;
    private readonly SqliteConnection _organizationConnection;
    private readonly SqliteConnection _auditConnection;
    private readonly SqliteConnection _questionBankConnection;
    private readonly SqliteConnection _questionnaireConnection;
    private readonly SqliteConnection _surveyConnection;
    private readonly SqliteConnection _campaignConnection;
    private readonly SqliteConnection _responseConnection;
    private readonly SqliteConnection _analyticsConnection;

    private TestEnvironment(
        IServiceProvider services,
        IdentityDbContext identityDbContext,
        OrganizationDbContext organizationDbContext,
        QuestionBankDbContext questionBankDbContext,
        QuestionnaireDbContext questionnaireDbContext,
        SurveyDbContext surveyDbContext,
        CampaignDbContext campaignDbContext,
        ResponseDbContext responseDbContext,
        AnalyticsDbContext analyticsDbContext,
        SqliteConnection identityConnection,
        SqliteConnection organizationConnection,
        SqliteConnection auditConnection,
        SqliteConnection questionBankConnection,
        SqliteConnection questionnaireConnection,
        SqliteConnection surveyConnection,
        SqliteConnection campaignConnection,
        SqliteConnection responseConnection,
        SqliteConnection analyticsConnection)
    {
        Services = services;
        IdentityDbContext = identityDbContext;
        OrganizationDbContext = organizationDbContext;
        QuestionBankDbContext = questionBankDbContext;
        QuestionnaireDbContext = questionnaireDbContext;
        SurveyDbContext = surveyDbContext;
        CampaignDbContext = campaignDbContext;
        ResponseDbContext = responseDbContext;
        AnalyticsDbContext = analyticsDbContext;
        _identityConnection = identityConnection;
        _organizationConnection = organizationConnection;
        _auditConnection = auditConnection;
        _questionBankConnection = questionBankConnection;
        _questionnaireConnection = questionnaireConnection;
        _surveyConnection = surveyConnection;
        _campaignConnection = campaignConnection;
        _responseConnection = responseConnection;
        _analyticsConnection = analyticsConnection;
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

        var questionBankConnection = new SqliteConnection("DataSource=:memory:");
        await questionBankConnection.OpenAsync();

        var questionnaireConnection = new SqliteConnection("DataSource=:memory:");
        await questionnaireConnection.OpenAsync();

        var surveyConnection = new SqliteConnection("DataSource=:memory:");
        await surveyConnection.OpenAsync();

        var campaignConnection = new SqliteConnection("DataSource=:memory:");
        await campaignConnection.OpenAsync();

        var responseConnection = new SqliteConnection("DataSource=:memory:");
        await responseConnection.OpenAsync();

        var analyticsConnection = new SqliteConnection("DataSource=:memory:");
        await analyticsConnection.OpenAsync();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlite(identityConnection));

        services.AddDbContext<OrganizationDbContext>(options =>
            options.UseSqlite(organizationConnection));

        services.AddDbContext<AuditDbContext>(options =>
            options.UseSqlite(auditConnection));

        services.AddDbContext<QuestionBankDbContext>(options =>
            options.UseSqlite(questionBankConnection));

        services.AddDbContext<QuestionnaireDbContext>(options =>
            options.UseSqlite(questionnaireConnection));

        services.AddDbContext<SurveyDbContext>(options =>
            options.UseSqlite(surveyConnection));

        services.AddDbContext<CampaignDbContext>(options =>
            options.UseSqlite(campaignConnection));

        services.AddDbContext<ResponseDbContext>(options =>
            options.UseSqlite(responseConnection));

        services.AddDbContext<AnalyticsDbContext>(options =>
            options.UseSqlite(analyticsConnection));

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
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.QuestionBank.Events.QuestionCreatedEvent>, QuestionBankAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.QuestionBank.Events.QuestionUpdatedEvent>, QuestionBankAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.QuestionBank.Events.QuestionArchivedEvent>, QuestionBankAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Questionnaire.Events.QuestionnaireCreatedEvent>, QuestionnaireAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Questionnaire.Events.QuestionnaireUpdatedEvent>, QuestionnaireAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Questionnaire.Events.QuestionnairePublishedEvent>, QuestionnaireAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Questionnaire.Events.QuestionnaireArchivedEvent>, QuestionnaireAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Survey.Events.SurveyCreatedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Survey.Events.SurveyUpdatedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Survey.Events.SurveyPublishedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Survey.Events.SurveyStartedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Survey.Events.SurveyClosedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Survey.Events.SurveyArchivedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Survey.Events.SurveyTemplateCreatedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Survey.Events.SurveyTemplateArchivedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Campaign.Events.CampaignCreatedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Campaign.Events.CampaignUpdatedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Campaign.Events.CampaignScheduledEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Campaign.Events.CampaignLaunchedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Campaign.Events.CampaignCompletedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Campaign.Events.CampaignArchivedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Response.Events.ResponseStartedEvent>, ResponseAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Response.Events.ResponseSubmittedEvent>, ResponseAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Response.Events.ResponseSubmittedEvent>, CampaignResponseEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Response.Events.ResponseSubmittedEvent>, AnalyticsResponseEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Analytics.Events.AnalyticsComputedEvent>, AnalyticsAuditEventListener>();
        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IOrganizationUnitOfWork, OrganizationUnitOfWork>();
        services.AddScoped<IQuestionBankUnitOfWork, QuestionBankUnitOfWork>();
        services.AddScoped<IQuestionnaireUnitOfWork, QuestionnaireUnitOfWork>();
        services.AddScoped<ISurveyUnitOfWork, SurveyUnitOfWork>();
        services.AddScoped<ICampaignUnitOfWork, CampaignUnitOfWork>();
        services.AddScoped<IResponseUnitOfWork, ResponseUnitOfWork>();
        services.AddScoped<IAnalyticsUnitOfWork, AnalyticsUnitOfWork>();

        services.AddScoped<IOrgUnitRepository, OrgUnitRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IQuestionnaireRepository, QuestionnaireRepository>();
        services.AddScoped<ISurveyRepository, SurveyRepository>();
        services.AddScoped<ISurveyTemplateRepository, SurveyTemplateRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IDistributionRepository, DistributionRepository>();
        services.AddScoped<IResponseRepository, ResponseRepository>();

        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
        services.AddScoped<AnalyticsComputationEngine>();

        services.AddScoped<IOrgUnitService, OrgUnitService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IQuestionnaireService, QuestionnaireService>();
        services.AddScoped<ISurveyService, SurveyService>();
        services.AddScoped<ISurveyTemplateService, SurveyTemplateService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<IResponseService, ResponseService>();

        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IBenchmarkService, BenchmarkService>();

        // پیاده‌سازی پیش‌فرض و بدون اثر هوش مصنوعی — هیچ داده‌ای از سامانه خارج نمی‌شود.
        services.AddSingleton<IAnalyticsAiService, NoOpAnalyticsAiService>();

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
        var questionBankDbContext = provider.GetRequiredService<QuestionBankDbContext>();
        var questionnaireDbContext = provider.GetRequiredService<QuestionnaireDbContext>();
        var surveyDbContext = provider.GetRequiredService<SurveyDbContext>();
        var campaignDbContext = provider.GetRequiredService<CampaignDbContext>();
        var responseDbContext = provider.GetRequiredService<ResponseDbContext>();
        var analyticsDbContext = provider.GetRequiredService<AnalyticsDbContext>();

        await identityDbContext.Database.EnsureCreatedAsync();
        await organizationDbContext.Database.EnsureCreatedAsync();
        await auditDbContext.Database.EnsureCreatedAsync();
        await questionBankDbContext.Database.EnsureCreatedAsync();
        await questionnaireDbContext.Database.EnsureCreatedAsync();
        await surveyDbContext.Database.EnsureCreatedAsync();
        await campaignDbContext.Database.EnsureCreatedAsync();
        await responseDbContext.Database.EnsureCreatedAsync();
        await analyticsDbContext.Database.EnsureCreatedAsync();

        return new TestEnvironment(provider, identityDbContext, organizationDbContext,
            questionBankDbContext, questionnaireDbContext, surveyDbContext, campaignDbContext,
            responseDbContext, analyticsDbContext,
            identityConnection, organizationConnection, auditConnection,
            questionBankConnection, questionnaireConnection, surveyConnection, campaignConnection,
            responseConnection, analyticsConnection);
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

    /// <summary>
    /// ساخت کارمندان نمونه در واحدهای سازمانی داده‌شده (برای آزمون‌های جمعیت هدف کمپین).
    /// وضعیت کارمندان چرخشی است: اولی «فعال»، دومی «مرخصی»، بقیه «ترک‌کار».
    /// </summary>
    /// <param name="unitEmployeeCounts">تعداد کارمند برای هر شناسه‌ی واحد.</param>
    public async Task<List<ODCC.Domain.Modules.Organization.Entities.Employee>> SeedEmployeesAsync(
        IReadOnlyDictionary<Guid, int> unitEmployeeCounts)
    {
        var employees = new List<ODCC.Domain.Modules.Organization.Entities.Employee>();
        var counter = 0;

        foreach (var (unitId, count) in unitEmployeeCounts)
        {
            for (var i = 0; i < count; i++)
            {
                counter++;
                employees.Add(new ODCC.Domain.Modules.Organization.Entities.Employee
                {
                    EmployeeCode = $"EMP-{counter:D3}",
                    FirstName = $"نام{counter}",
                    LastName = $"خانوادگی{counter}",
                    OrgUnitId = unitId,
                    Status = i switch
                    {
                        0 => ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.Active,
                        1 => ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.OnLeave,
                        _ => ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.Terminated
                    },
                    StartDate = new DateOnly(2020, 1, 1),
                    WorkEmail = $"emp{counter}@test.local"
                });
            }
        }

        OrganizationDbContext.Employees.AddRange(employees);
        await OrganizationDbContext.SaveChangesAsync();

        return employees;
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
        await QuestionBankDbContext.DisposeAsync();
        await QuestionnaireDbContext.DisposeAsync();
        await SurveyDbContext.DisposeAsync();
        await CampaignDbContext.DisposeAsync();
        await ResponseDbContext.DisposeAsync();
        await AnalyticsDbContext.DisposeAsync();
        await _identityConnection.DisposeAsync();
        await _organizationConnection.DisposeAsync();
        await _auditConnection.DisposeAsync();
        await _questionBankConnection.DisposeAsync();
        await _questionnaireConnection.DisposeAsync();
        await _surveyConnection.DisposeAsync();
        await _campaignConnection.DisposeAsync();
        await _responseConnection.DisposeAsync();
        await _analyticsConnection.DisposeAsync();
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
