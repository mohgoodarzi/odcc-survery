using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Infrastructure.Modules.Audit.EventListeners;
using ODCC.Infrastructure.Modules.Identity;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;
using ODCC.Infrastructure.Modules.Identity.Repositories;
using ODCC.Infrastructure.Modules.Identity.Services;
using ODCC.Infrastructure.Modules.Organization.Persistence;
using ODCC.Infrastructure.Modules.Organization.Repositories;
using ODCC.Infrastructure.Modules.Organization.Services;
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
using ODCC.Infrastructure.Persistence;
using ODCC.Infrastructure.Persistence.Audit;
using ODCC.Infrastructure.Repositories.Audit;
using ODCC.Infrastructure.Services;

namespace ODCC.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// ثبت لایه‌ی زیرساخت: DbContext ماژول‌ها، مخازن و سرویس‌های مشترک.
    /// </summary>
    public static IServiceCollection AddOdccInfrastructure(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "رشته‌ی اتصال 'Default' یافت نشد. آن را با متغیر محیطی ConnectionStrings__Default، " +
                "user secret یا appsettings.json تامین کنید. هرگز اعتبارات واقعی را در مخزن کد قرار ندهید.");
        }

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddSingleton<IAnalyticsAiService, NoOpAnalyticsAiService>();
        services.AddScoped<IOrgScopeProvider, OrgScopeProvider>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // داده‌ی اولیه (نقش مدیر کل، کاربر مدیر کل، واحد سازمانی ریشه).
        // فقط توسط OdccDbInitializerHostedService و با تأیید Database:AutoMigrate اجرا می‌شود.
        services.AddScoped<IDataSeeder, DataSeeder>();

        // شنونده‌های رویدادهای دامنه: هر شنونده با قرارداد نوع رویداد ثبت می‌شود
        // تا DomainEventDispatcher بتواند آن‌ها را بدون اسکن اسمبلی پیدا کند.
        // این تنها نقطه‌ای است که ماژول ممیزی به رویدادهای سایر ماژول‌ها وصل می‌شود.
        services.AddScoped<IDomainEventListener<Domain.Modules.Organization.Events.OrgUnitCreatedEvent>, OrganizationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Organization.Events.OrgUnitUpdatedEvent>, OrganizationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Organization.Events.OrgUnitDeletedEvent>, OrganizationAuditEventListener>();

        // شنونده‌ی رویدادهای ماژول کتابخانه‌ی سؤالات.
        services.AddScoped<IDomainEventListener<Domain.Modules.QuestionBank.Events.QuestionCreatedEvent>, QuestionBankAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.QuestionBank.Events.QuestionUpdatedEvent>, QuestionBankAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.QuestionBank.Events.QuestionArchivedEvent>, QuestionBankAuditEventListener>();

        // شنونده‌ی رویدادهای ماژول پرسشنامه‌ها.
        services.AddScoped<IDomainEventListener<Domain.Modules.Questionnaire.Events.QuestionnaireCreatedEvent>, QuestionnaireAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Questionnaire.Events.QuestionnaireUpdatedEvent>, QuestionnaireAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Questionnaire.Events.QuestionnairePublishedEvent>, QuestionnaireAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Questionnaire.Events.QuestionnaireArchivedEvent>, QuestionnaireAuditEventListener>();

        // شنونده‌ی رویدادهای ماژول نظرسنجی‌ها.
        services.AddScoped<IDomainEventListener<Domain.Modules.Survey.Events.SurveyCreatedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Survey.Events.SurveyUpdatedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Survey.Events.SurveyPublishedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Survey.Events.SurveyStartedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Survey.Events.SurveyClosedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Survey.Events.SurveyArchivedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Survey.Events.SurveyTemplateCreatedEvent>, SurveyAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Survey.Events.SurveyTemplateArchivedEvent>, SurveyAuditEventListener>();

        // شنونده‌ی رویدادهای ماژول کمپین‌ها.
        services.AddScoped<IDomainEventListener<Domain.Modules.Campaign.Events.CampaignCreatedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Campaign.Events.CampaignUpdatedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Campaign.Events.CampaignScheduledEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Campaign.Events.CampaignLaunchedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Campaign.Events.CampaignCompletedEvent>, CampaignAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Campaign.Events.CampaignArchivedEvent>, CampaignAuditEventListener>();

        // شنونده‌ی رویدادهای ماژول پاسخ‌ها.
        services.AddScoped<IDomainEventListener<Domain.Modules.Response.Events.ResponseStartedEvent>, ResponseAuditEventListener>();

        // شنونده‌ی کمپین: دعوت‌نامه‌ای که از طریق آن پاسخی ثبت شده را
        // «پاسخ‌داده» علامت می‌زند. این شنونده در ماژول کمپین ثبت می‌شود
        // چون تغییر روی موجودیت‌های کمپین است، ولی به رویداد ماژول پاسخ گوش می‌دهد.
        services.AddScoped<IDomainEventListener<Domain.Modules.Response.Events.ResponseSubmittedEvent>, ResponseAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Response.Events.ResponseSubmittedEvent>, CampaignResponseEventListener>();

        ConfigureAudit(services, connectionString, configure);
        ConfigureIdentity(services, connectionString, configure);
        ConfigureOrganization(services, connectionString, configure);
        ConfigureQuestionBank(services, connectionString, configure);
        ConfigureQuestionnaire(services, connectionString, configure);
        ConfigureSurvey(services, connectionString, configure);
        ConfigureCampaign(services, connectionString, configure);
        ConfigureResponse(services, connectionString, configure);

        return services;
    }

    /// <summary>
    /// راه‌اندازی پایگاه داده در زمان بوت.
    ///
    /// توجه: طبق سیاست امنیتی پروژه، اجرای خودکار مهاجرت‌ها به‌صورت پیش‌فرض
    /// <b>غیرفعال</b> است (<c>Database:AutoMigrate</c>). فعال‌سازی آن نیازمند
    /// تأیید صریح شما پیش از ایجاد/تغییر پایگاه داده است.
    /// </summary>
    public static IServiceCollection AddOdccDatabaseInitializer(this IServiceCollection services, IConfiguration configuration)
    {
        // تنظیمات داده‌ی اولیه از بخش Seed پیکربندی (user secrets / env / appsettings).
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        var autoMigrate = configuration.GetValue<bool?>("Database:AutoMigrate") is true;

        if (autoMigrate)
        {
            services.AddHostedService<OdccDbInitializerHostedService>();
        }

        return services;
    }

    /// <summary>
    /// پیکربندی احراز هویت JWT.
    /// <paramref name="jwt"/> تنظیمات توکن؛ در صورت نبودن کلید معتبر، استثنا پرتاب می‌شود
    /// (fail-fast) تا هرگز با کلید امضای نامعتغر سرویس شروع به کار نکند.
    /// </summary>
    public static IServiceCollection AddOdccJwtAuthentication(
        this IServiceCollection services,
        JwtOptions jwt,
        string authenticationScheme = "Bearer")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jwt.Secret);

        if (jwt.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "کلید امضای JWT حداقل باید ۳۲ کاراکتر باشد. آن را با متغیر محیطی Jwt__Secret " +
                "یا user secret تامین کنید. هرگز کلید واقعی را در مخزن کد قرار ندهید.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));

        services.AddAuthentication(authenticationScheme)
            .AddJwtBearer(authenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        return services;
    }

    // --- ماژول ممیزی ---------------------------------------------------------

    private static void ConfigureAudit(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<AuditDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork<AuditDbContext>>();
    }

    // --- ماژول هویت -----------------------------------------------------------

    private static void ConfigureIdentity(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<IdentityDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // سیاست رمز عبور: مطابق با اعتبارسنجی‌های لایه‌ی کاربرد.
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                // کلیم نوع permission روی نقش‌ها ذخیره می‌شود.
                options.ClaimsIdentity.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
            })
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
    }

    // --- ماژول سازمان ---------------------------------------------------------

    private static void ConfigureOrganization(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<OrganizationDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IOrgUnitRepository, OrgUnitRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();

        services.AddScoped<IOrgUnitService, OrgUnitService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IOrganizationUnitOfWork, OrganizationUnitOfWork>();
    }

    // --- ماژول کتابخانه‌ی سؤالات -------------------------------------------

    private static void ConfigureQuestionBank(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<QuestionBankDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IQuestionBankUnitOfWork, QuestionBankUnitOfWork>();
    }

    // --- ماژول پرسشنامه‌ها -------------------------------------------------

    private static void ConfigureQuestionnaire(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<QuestionnaireDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IQuestionnaireRepository, QuestionnaireRepository>();
        services.AddScoped<IQuestionnaireService, QuestionnaireService>();
        services.AddScoped<IQuestionnaireUnitOfWork, QuestionnaireUnitOfWork>();
    }

    // --- ماژول نظرسنجی‌ها ---------------------------------------------------

    private static void ConfigureSurvey(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<SurveyDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<ISurveyRepository, SurveyRepository>();
        services.AddScoped<ISurveyTemplateRepository, SurveyTemplateRepository>();
        services.AddScoped<ISurveyService, SurveyService>();
        services.AddScoped<ISurveyTemplateService, SurveyTemplateService>();
        services.AddScoped<ISurveyUnitOfWork, SurveyUnitOfWork>();
    }

    // --- ماژول کمپین‌ها -----------------------------------------------------

    private static void ConfigureCampaign(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<CampaignDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IDistributionRepository, DistributionRepository>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<ICampaignUnitOfWork, CampaignUnitOfWork>();
    }

    // --- ماژول پاسخ‌ها -----------------------------------------------------

    private static void ConfigureResponse(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<ResponseDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IResponseRepository, ResponseRepository>();
        services.AddScoped<IResponseService, ResponseService>();
        services.AddScoped<IResponseUnitOfWork, ResponseUnitOfWork>();
    }

    private static void ConfigureSql(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
    }
}
