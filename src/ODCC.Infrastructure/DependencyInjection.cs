using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
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
using ODCC.Infrastructure.Modules.Campaign.Scheduled;
using ODCC.Infrastructure.Modules.Campaign.Services;
using ODCC.Infrastructure.Modules.Response.EventListeners;
using ODCC.Infrastructure.Modules.Response.Persistence;
using ODCC.Infrastructure.Modules.Response.Repositories;
using ODCC.Infrastructure.Modules.Response.Services;
using ODCC.Infrastructure.Modules.Analytics.EventListeners;
using ODCC.Infrastructure.Modules.Analytics.Persistence;
using ODCC.Infrastructure.Modules.Analytics.Repositories;
using ODCC.Infrastructure.Modules.Analytics.Services;
using ODCC.Infrastructure.Modules.Reporting.EventListeners;
using ODCC.Infrastructure.Modules.Reporting.Persistence;
using ODCC.Infrastructure.Modules.Reporting.Repositories;
using ODCC.Infrastructure.Modules.Reporting.Scheduled;
using ODCC.Infrastructure.Modules.Reporting.Services;
using ODCC.Infrastructure.Modules.Notification.EventListeners;
using ODCC.Infrastructure.Modules.Notification.Persistence;
using ODCC.Infrastructure.Modules.Notification.Repositories;
using ODCC.Infrastructure.Modules.Notification.Scheduled;
using ODCC.Infrastructure.Modules.Notification.Services;
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

        // شنونده‌ی تحلیلات: پس از هر ارسال پاسخ، عکس‌العمل تحلیلات نظرسنجی
        // به‌روزرسانی می‌شود تا داشبوردها به‌روز بمانند. این شنونده در ماژول
        // تحلیلات ثبت می‌شود چون تغییر روی موجودیت‌های تحلیلات است، ولی به
        // رویداد ماژول پاسخ‌ها گوش می‌دهد.
        services.AddScoped<IDomainEventListener<Domain.Modules.Response.Events.ResponseSubmittedEvent>, AnalyticsResponseEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای تحلیلات.
        services.AddScoped<IDomainEventListener<Domain.Modules.Analytics.Events.AnalyticsComputedEvent>, AnalyticsAuditEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای گزارش‌گیری: ایجاد، ویرایش، فعال‌سازی،
        // بایگانی و اجرای گزارش‌ها. این شنونده در ماژول گزارش‌گیری ثبت می‌شود
        // چون تغییر روی موجودیت‌های این ماژول است، ولی به رویدادهای خودش گوش می‌دهد.
        services.AddScoped<IDomainEventListener<Domain.Modules.Reporting.Events.ReportCreatedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Reporting.Events.ReportUpdatedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Reporting.Events.ReportActivatedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Reporting.Events.ReportArchivedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Reporting.Events.ReportExecutionStartedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Reporting.Events.ReportExecutionSucceededEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Reporting.Events.ReportExecutionFailedEvent>, ReportingAuditEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای اعلان‌ها: ایجاد، تحویل، شکست و خوانده‌شدن.
        // این شنونده در ماژول اعلان‌ها ثبت می‌شود چون تغییر روی موجودیت‌های همین
        // ماژول است، ولی به رویدادهای خودش گوش می‌دهد.
        services.AddScoped<IDomainEventListener<Domain.Modules.Notification.Events.NotificationCreatedEvent>, NotificationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Notification.Events.NotificationDeliveredEvent>, NotificationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Notification.Events.NotificationFailedEvent>, NotificationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Notification.Events.NotificationReadEvent>, NotificationAuditEventListener>();

        // شنونده‌ی اعلان‌ها: دعوت‌نامه‌ها و یادآورهای کمپین را به پیام واقعی
        // تبدیل می‌کند. این شنونده در ماژول اعلان‌ها ثبت می‌شود چون موجودیت‌های
        // ساخته‌شده (Notification) متعلق به این ماژول است، ولی به رویدادهای
        // ماژول کمپین گوش می‌دهد.
        services.AddScoped<IDomainEventListener<Domain.Modules.Campaign.Events.CampaignLaunchedEvent>, CampaignNotificationEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Campaign.Events.ReminderDueEvent>, CampaignNotificationEventListener>();

        ConfigureAudit(services, connectionString, configure);
        ConfigureIdentity(services, connectionString, configure);
        ConfigureOrganization(services, connectionString, configure);
        ConfigureQuestionBank(services, connectionString, configure);
        ConfigureQuestionnaire(services, connectionString, configure);
        ConfigureSurvey(services, connectionString, configure);
        ConfigureCampaign(services, connectionString, configure);
        ConfigureResponse(services, connectionString, configure);
        ConfigureAnalytics(services, connectionString, configure);
        ConfigureReporting(services, connectionString, configure);
        ConfigureNotification(services, connectionString, configure);

        return services;
    }

    /// <summary>
    /// راه‌اندازی بخش پیکربندی ماژول گزارش‌گیری: گزینه‌های انبار فایل،
    /// فونت فارسی رندر PDF و (در صورت تأیید) زمان‌بند اجرای خودکار.
    ///
    /// طبق سیاست پروژه، اجرای خودکار یک اثر جانبی است و فقط با تأیید صریح
    /// (<c>Reports:EnableScheduler</c>) فعال می‌شود.
    /// </summary>
    public static IServiceCollection AddOdccReporting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReportSchedulerOptions>(configuration.GetSection(ReportSchedulerOptions.SectionName));
        services.Configure<ReportArtifactOptions>(configuration.GetSection(ReportArtifactOptions.SectionName));

        // QuestPDF نیازمند اعلام نوع مجوز در زمان بوت است. بدون این پیکربندی،
        // اولین رندر PDF با استثنا شکست می‌خورد. مجوز Community برای استفاده‌ی
        // داخلی این سامانه کافی است؛ کلید مجوز تجاری (در صورت نیاز) از
        // پیکربندی و بدون ذخیره در کد خوانده می‌شود.
        var licenseKey = configuration[$"{ReportArtifactOptions.SectionName}:QuestPdfLicenseKey"];
        global::QuestPDF.Settings.License = string.IsNullOrWhiteSpace(licenseKey)
            ? global::QuestPDF.Infrastructure.LicenseType.Community
            : global::QuestPDF.Infrastructure.LicenseType.Professional;

        // فونت فارسی برای رندر PDF یک‌بار در زمان بوت بارگذاری می‌شود.
        // فونت همراه QuestPDF (Lato) گلیف‌های فارسی ندارد.
        var artifactOptions = configuration.GetSection(ReportArtifactOptions.SectionName)
            .Get<ReportArtifactOptions>() ?? new ReportArtifactOptions();

        var resolvedFamily = ReportFontLoader.Initialize(artifactOptions.PersianFontPath);

        if (ReportFontLoader.UsedFallback)
        {
            Console.WriteLine(
                $"هشدار: فونت فارسی برای گزارش‌های PDF یافت نشد (خانواده‌ی «{resolvedFamily}» استفاده می‌شود). " +
                "متن فارسی در PDF به‌درستی نمایش داده نمی‌شود. مسیر فونت را با Reports:PersianFontPath مشخص کنید.");
        }

        var enableScheduler = configuration.GetValue<bool?>($"{ReportSchedulerOptions.SectionName}:EnableScheduler") is true;

        if (enableScheduler)
        {
            services.AddHostedService<ReportSchedulerHostedService>();
        }

        return services;
    }

    /// <summary>
    /// راه‌انداز پایگاه داده در زمان بوت.
    ///
    /// سرویس همیشه ثبت می‌شود تا کاشت داده‌ی اولیه (خودتوان و افزودنی) اجرا
    /// شود. طبق سیاست امنیتی پروژه، <b>مهاجرت</b> خودکار به‌صورت پیش‌فرض
    /// غیرفعال است (<c>Database:AutoMigrate</c>) و فقط با تأیید صریح شما پیش
    /// از ایجاد/تغییر پایگاه داده انجام می‌شود.
    /// </summary>
    public static IServiceCollection AddOdccDatabaseInitializer(this IServiceCollection services, IConfiguration configuration)
    {
        // تنظیمات داده‌ی اولیه از بخش Seed پیکربندی (user secrets / env / appsettings).
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        var autoMigrate = configuration.GetValue<bool?>("Database:AutoMigrate") is true;

        services.AddHostedService(sp => new OdccDbInitializerHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            autoMigrate,
            sp.GetRequiredService<ILogger<OdccDbInitializerHostedService>>()));

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

        // AddIdentity (فراخوانی‌شده در AddOdccInfrastructure پیش از این متد) طرح
        // کوکی ASP.NET Identity را ثبت و DefaultAuthenticateScheme/DefaultChallengeScheme
        // را روی همان طرح کوکی تنظیم می‌کند. بدون تصحیح زیر، توکن‌های Bearer
        // معتبر نادیده گرفته می‌شوند و اندپوینت‌های محافظت‌شده به جای ۴۰۱/۴۰۳،
        // به صفحه‌ی ورود کوکی هدایت می‌شوند. اینجا دوباره طرح JWT را پیش‌فرض
        // همه‌ی نوع‌های چالش می‌کنیم.
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

        // AddIdentity، DefaultAuthenticateScheme و DefaultChallengeScheme را روی
        // طرح کوکی خود تنظیم کرده است. AddAuthentication(scheme) در بالا تنها
        // DefaultScheme را می‌نویسد و کافی نیست؛ طرح‌های پیش‌فرض باید صریحاً
        // دوباره روی Bearer تنظیم شوند.
        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = authenticationScheme;
            options.DefaultChallengeScheme = authenticationScheme;
            options.DefaultForbidScheme = authenticationScheme;
            options.DefaultSignInScheme = authenticationScheme;
            options.DefaultSignOutScheme = authenticationScheme;
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

    // --- ماژول تحلیلات -----------------------------------------------------

    private static void ConfigureAnalytics(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<AnalyticsDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IBenchmarkService, BenchmarkService>();
        services.AddScoped<AnalyticsComputationEngine>();
        services.AddScoped<IAnalyticsUnitOfWork, AnalyticsUnitOfWork>();
    }

    // --- ماژول گزارش‌گیری --------------------------------------------------

    private static void ConfigureReporting(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<ReportingDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IReportDefinitionRepository, ReportDefinitionRepository>();
        services.AddScoped<IReportExecutionRepository, ReportExecutionRepository>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IReportDataAssembler, ReportDataAssembler>();
        services.AddScoped<IReportRenderer, ExcelReportRenderer>();
        services.AddScoped<IReportRenderer, PdfReportRenderer>();
        services.AddSingleton<IReportArtifactStore, FileSystemReportArtifactStore>();
        services.AddScoped<IReportingUnitOfWork, ReportingUnitOfWork>();
    }

    // --- ماژول اعلان‌ها ----------------------------------------------------

    private static void ConfigureNotification(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<NotificationDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // ارائه‌دهنده‌های تحویل: یکی به ازای هر کانال. افزودن کانال جدید فقط
        // پیاده‌سازی INotificationDeliveryProvider و ثبت آن در DI است.
        services.AddScoped<INotificationDeliveryProvider, InAppNotificationProvider>();
        services.AddScoped<INotificationDeliveryProvider, EmailNotificationProvider>();
        services.AddScoped<INotificationDeliveryProvider, SmsNotificationProvider>();

        // پیاده‌سازی‌های پیش‌فرض ارسال: No-op تا سامانه بدون پیکربندی SMTP کار
        // کند. جایگزینی آن‌ها با ارائه‌دهنده‌ی واقعی فقط ثبت یک سرویس در DI است.
        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddScoped<ISmsSender, NoOpSmsSender>();

        services.AddScoped<INotificationUnitOfWork, NotificationUnitOfWork>();
    }

    /// <summary>
    /// راه‌اندازی بخش پیکربندی ماژول اعلان‌ها: گزینه‌های تحویل، ایمیل/پیامک و
    /// (در صورت تأیید) زمان‌بند تحویل پس‌زمینه.
    ///
    /// طبق سیاست پروژه، پردازش پس‌زمینه یک اثر جانبی است و فقط با تأیید صریح
    /// (<c>Notifications:Scheduler:EnableScheduler</c>) فعال می‌شود. تحویل
    /// بلافاصله‌ی <c>SendAsync</c> همیشه فعال است؛ زمان‌بند فقط برای امتحان
    /// مجدد و حجم بالا لازم است.
    /// </summary>
    public static IServiceCollection AddOdccNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationDeliveryOptions>(configuration.GetSection(NotificationDeliveryOptions.SectionName));
        services.Configure<NotificationEmailOptions>(configuration.GetSection(NotificationEmailOptions.SectionName));
        services.Configure<NotificationSmsOptions>(configuration.GetSection(NotificationSmsOptions.SectionName));
        services.Configure<NotificationSchedulerOptions>(configuration.GetSection(NotificationSchedulerOptions.SectionName));

        var enableScheduler = configuration.GetValue<bool?>($"{NotificationSchedulerOptions.SectionName}:EnableScheduler") is true;

        if (enableScheduler)
        {
            services.AddHostedService<NotificationSchedulerHostedService>();
        }

        return services;
    }

    /// <summary>
    /// راه‌اندازی زمان‌بند یادآورهای کمپین.
    ///
    /// این سرویس هر چند ثانیه یادآورهای سررسیده را علامت‌گذاری کرده و
    /// <c>ReminderDueEvent</c> منتشر می‌کند؛ ارسال واقعی پیام در ماژول اعلان‌ها
    /// انجام می‌شود. چون ارسال پیام یک اثر جانبی است، فقط با تأیید صریح
    /// (<c>Campaigns:EnableReminderScheduler</c>) فعال می‌شود.
    /// </summary>
    public static IServiceCollection AddOdccCampaignReminders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CampaignReminderOptions>(configuration.GetSection(CampaignReminderOptions.SectionName));

        var enableScheduler = configuration.GetValue<bool?>($"{CampaignReminderOptions.SectionName}:EnableReminderScheduler") is true;

        if (enableScheduler)
        {
            services.AddHostedService<CampaignReminderHostedService>();
        }

        return services;
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
