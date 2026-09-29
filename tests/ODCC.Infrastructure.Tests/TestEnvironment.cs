using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.Workflow.Abstractions;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.SystemConfiguration.Abstractions;
using ODCC.Application.Modules.FileStorage.Abstractions;
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
using ODCC.Infrastructure.Modules.Reporting.EventListeners;
using ODCC.Infrastructure.Modules.Reporting.Persistence;
using ODCC.Infrastructure.Modules.Reporting.Repositories;
using ODCC.Infrastructure.Modules.Reporting.Services;
using ODCC.Infrastructure.Modules.Notification.EventListeners;
using ODCC.Infrastructure.Modules.Notification.Persistence;
using ODCC.Infrastructure.Modules.Notification.Repositories;
using ODCC.Infrastructure.Modules.Notification.Services;
using ODCC.Infrastructure.Modules.ActionManagement.EventListeners;
using ODCC.Infrastructure.Modules.ActionManagement.Persistence;
using ODCC.Infrastructure.Modules.ActionManagement.Repositories;
using ODCC.Infrastructure.Modules.ActionManagement.Services;
using ODCC.Infrastructure.Modules.Workflow.EventListeners;
using ODCC.Infrastructure.Modules.Workflow.Persistence;
using ODCC.Infrastructure.Modules.Workflow.Repositories;
using ODCC.Infrastructure.Modules.Workflow.Services;
using ODCC.Infrastructure.Modules.Integration.EventListeners;
using ODCC.Infrastructure.Modules.Integration.Persistence;
using ODCC.Infrastructure.Modules.Integration.Repositories;
using ODCC.Infrastructure.Modules.Integration.Services;
using ODCC.Infrastructure.Modules.Integration.Scheduled;
using ODCC.Infrastructure.Modules.SystemConfiguration.EventListeners;
using ODCC.Infrastructure.Modules.SystemConfiguration.Persistence;
using ODCC.Infrastructure.Modules.SystemConfiguration.Repositories;
using ODCC.Infrastructure.Modules.SystemConfiguration.Services;
using ODCC.Infrastructure.Modules.FileStorage.Services;
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
    public ReportingDbContext ReportingDbContext { get; }
    public NotificationDbContext NotificationDbContext { get; }
    public ActionManagementDbContext ActionManagementDbContext { get; }
    public WorkflowDbContext WorkflowDbContext { get; }
    public IntegrationDbContext IntegrationDbContext { get; }
    public SystemConfigurationDbContext SystemConfigurationDbContext { get; }

    private readonly SqliteConnection _identityConnection;
    private readonly SqliteConnection _organizationConnection;
    private readonly SqliteConnection _auditConnection;
    private readonly SqliteConnection _questionBankConnection;
    private readonly SqliteConnection _questionnaireConnection;
    private readonly SqliteConnection _surveyConnection;
    private readonly SqliteConnection _campaignConnection;
    private readonly SqliteConnection _responseConnection;
    private readonly SqliteConnection _analyticsConnection;
    private readonly SqliteConnection _reportingConnection;
    private readonly SqliteConnection _notificationConnection;
    private readonly SqliteConnection _actionManagementConnection;
    private readonly SqliteConnection _workflowConnection;
    private readonly SqliteConnection _integrationConnection;
    private readonly SqliteConnection _systemConfigurationConnection;
    private readonly string _evidenceRoot;

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
        ReportingDbContext reportingDbContext,
        NotificationDbContext notificationDbContext,
        ActionManagementDbContext actionManagementDbContext,
        WorkflowDbContext workflowDbContext,
        IntegrationDbContext integrationDbContext,
        SystemConfigurationDbContext systemConfigurationDbContext,
        SqliteConnection identityConnection,
        SqliteConnection organizationConnection,
        SqliteConnection auditConnection,
        SqliteConnection questionBankConnection,
        SqliteConnection questionnaireConnection,
        SqliteConnection surveyConnection,
        SqliteConnection campaignConnection,
        SqliteConnection responseConnection,
        SqliteConnection analyticsConnection,
        SqliteConnection reportingConnection,
        SqliteConnection notificationConnection,
        SqliteConnection actionManagementConnection,
        SqliteConnection workflowConnection,
        SqliteConnection integrationConnection,
        SqliteConnection systemConfigurationConnection,
        string evidenceRoot)
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
        ReportingDbContext = reportingDbContext;
        NotificationDbContext = notificationDbContext;
        ActionManagementDbContext = actionManagementDbContext;
        WorkflowDbContext = workflowDbContext;
        IntegrationDbContext = integrationDbContext;
        SystemConfigurationDbContext = systemConfigurationDbContext;
        _identityConnection = identityConnection;
        _organizationConnection = organizationConnection;
        _auditConnection = auditConnection;
        _questionBankConnection = questionBankConnection;
        _questionnaireConnection = questionnaireConnection;
        _surveyConnection = surveyConnection;
        _campaignConnection = campaignConnection;
        _responseConnection = responseConnection;
        _analyticsConnection = analyticsConnection;
        _reportingConnection = reportingConnection;
        _notificationConnection = notificationConnection;
        _actionManagementConnection = actionManagementConnection;
        _workflowConnection = workflowConnection;
        _integrationConnection = integrationConnection;
        _systemConfigurationConnection = systemConfigurationConnection;
        _evidenceRoot = evidenceRoot;
    }

    /// <summary>
    /// ساخت یک محیط آزمون با کاربر پیش‌فرض اختیاری.
    /// </summary>
    /// <param name="jwtSecret">کلید امضای توکن (پیش‌فرض: کلید آزمون).</param>
    public static async Task<TestEnvironment> CreateAsync(string? jwtSecret = null)
    {
        var identityConnection = new SqliteConnection($"DataSource=:memory:");
        await identityConnection.OpenAsync();

        var organizationConnection = new SqliteConnection($"DataSource=:memory:");
        await organizationConnection.OpenAsync();

        var auditConnection = new SqliteConnection($"DataSource=:memory:");
        await auditConnection.OpenAsync();

        var questionBankConnection = new SqliteConnection($"DataSource=:memory:");
        await questionBankConnection.OpenAsync();

        var questionnaireConnection = new SqliteConnection($"DataSource=:memory:");
        await questionnaireConnection.OpenAsync();

        var surveyConnection = new SqliteConnection($"DataSource=:memory:");
        await surveyConnection.OpenAsync();

        var campaignConnection = new SqliteConnection($"DataSource=:memory:");
        await campaignConnection.OpenAsync();

        var responseConnection = new SqliteConnection($"DataSource=:memory:");
        await responseConnection.OpenAsync();

        var analyticsConnection = new SqliteConnection($"DataSource=:memory:");
        await analyticsConnection.OpenAsync();

        var reportingConnection = new SqliteConnection($"DataSource=:memory:");
        await reportingConnection.OpenAsync();

        var notificationConnection = new SqliteConnection($"DataSource=:memory:");
        await notificationConnection.OpenAsync();

        var actionManagementConnection = new SqliteConnection($"DataSource=:memory:");
        await actionManagementConnection.OpenAsync();

        var workflowConnection = new SqliteConnection($"DataSource=:memory:");
        await workflowConnection.OpenAsync();

        var integrationConnection = new SqliteConnection($"DataSource=:memory:");
        await integrationConnection.OpenAsync();

        var systemConfigurationConnection = new SqliteConnection($"DataSource=:memory:");
        await systemConfigurationConnection.OpenAsync();

        var services = new ServiceCollection();

        // شاخه‌ی موقت برای انبار پیوست‌های اقدامات (در پایان آزمون پاک می‌شود).
        var evidenceRoot = Path.Combine(Path.GetTempPath(), "odcc-action-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidenceRoot);

        // QuestPDF نیازمند اعلام نوع مجوز در زمان شروع است. در آزمون‌ها از
        // مجوز Community استفاده می‌شود (محیط غیرتولیدی).
        global::QuestPDF.Settings.License = global::QuestPDF.Infrastructure.LicenseType.Community;

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

        services.AddDbContext<ReportingDbContext>(options =>
            options.UseSqlite(reportingConnection));

        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlite(notificationConnection));

        services.AddDbContext<ActionManagementDbContext>(options =>
            options.UseSqlite(actionManagementConnection));

        services.AddDbContext<WorkflowDbContext>(options =>
            options.UseSqlite(workflowConnection));

        services.AddDbContext<IntegrationDbContext>(options =>
            options.UseSqlite(integrationConnection));

        services.AddDbContext<SystemConfigurationDbContext>(options =>
            options.UseSqlite(systemConfigurationConnection));

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
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Reporting.Events.ReportCreatedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Reporting.Events.ReportUpdatedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Reporting.Events.ReportActivatedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Reporting.Events.ReportArchivedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Reporting.Events.ReportExecutionStartedEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Reporting.Events.ReportExecutionSucceededEvent>, ReportingAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Reporting.Events.ReportExecutionFailedEvent>, ReportingAuditEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای اعلان‌ها.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Notification.Events.NotificationCreatedEvent>, NotificationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Notification.Events.NotificationDeliveredEvent>, NotificationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Notification.Events.NotificationFailedEvent>, NotificationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Notification.Events.NotificationReadEvent>, NotificationAuditEventListener>();

        // شنونده‌ی اعلان‌ها: دعوت‌نامه و یادآور کمپین.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Campaign.Events.CampaignLaunchedEvent>, CampaignNotificationEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Campaign.Events.ReminderDueEvent>, CampaignNotificationEventListener>();

        // شنونده‌ی اعلان‌ها: انتصاب، یادآور، تشدید و تکمیل آیتم‌های اقدام.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionItemCreatedEvent>, ActionNotificationEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionItemCompletedEvent>, ActionNotificationEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionItemReminderDueEvent>, ActionNotificationEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionItemEscalatedEvent>, ActionNotificationEventListener>();

        // شنونده‌ی مدیریت اقدامات: هشدارهای تحلیلات را به برنامه‌ی اقدام تبدیل می‌کند.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Analytics.Events.AnalyticsComputedEvent>, AnalyticsActionEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای مدیریت اقدامات.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionPlanCreatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionPlanUpdatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionPlanActivatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionPlanCompletedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionPlanCancelledEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionPlanArchivedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionItemCreatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionItemUpdatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionItemStatusChangedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionItemEscalatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionCommentAddedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.ActionManagement.Events.ActionEvidenceUploadedEvent>, ActionManagementAuditEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای گردش کار.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowCreatedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowUpdatedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowActivatedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowArchivedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowInstanceStartedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowInstanceTransitionedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowInstanceCompletedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowInstanceCancelledEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowApprovalRequestedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowApprovalDecidedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowApprovalExpiredEvent>, WorkflowAuditEventListener>();

        // شنونده‌ی اعلان‌ها: درخواست تأیید گردش کار.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowApprovalRequestedEvent>, WorkflowNotificationEventListener>();

        // شنونده‌ی یکپارچه‌سازی: رویدادها را به وب‌هوک‌های خروجی تحویل می‌دهد.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Analytics.Events.AnalyticsComputedEvent>, IntegrationWebhookEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Response.Events.ResponseSubmittedEvent>, IntegrationWebhookEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Workflow.Events.WorkflowInstanceTransitionedEvent>, IntegrationWebhookEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای یکپارچه‌سازی.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Integration.Events.IntegrationEndpointCreatedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Integration.Events.IntegrationEndpointUpdatedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Integration.Events.IntegrationEndpointActivatedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Integration.Events.IntegrationEndpointArchivedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Integration.Events.WebhookDeliveredEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Integration.Events.WebhookDeliveryFailedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.Integration.Events.InboundWebhookReceivedEvent>, IntegrationAuditEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای پیکربندی سامانه.
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.SystemConfiguration.Events.SettingChangedEvent>, SystemConfigurationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.SystemConfiguration.Events.FeatureFlagChangedEvent>, SystemConfigurationAuditEventListener>();
        services.AddScoped<IDomainEventListener<ODCC.Domain.Modules.SystemConfiguration.Events.SystemPolicyChangedEvent>, SystemConfigurationAuditEventListener>();

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
        services.AddScoped<IReportingUnitOfWork, ReportingUnitOfWork>();
        services.AddScoped<INotificationUnitOfWork, NotificationUnitOfWork>();
        services.AddScoped<IActionManagementUnitOfWork, ActionManagementUnitOfWork>();

        // ماژول گردش کار: مخازن، سرویس و مرز تراکنشی.
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<IWorkflowApprovalRepository, WorkflowApprovalRepository>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IWorkflowUnitOfWork, WorkflowUnitOfWork>();

        // ماژول یکپارچه‌سازی: مخازن، سرویس، ارسال وب‌هوک و امضا.
        services.AddScoped<IIntegrationEndpointRepository, IntegrationEndpointRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IIntegrationService, IntegrationService>();
        services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
        services.AddScoped<IIntegrationUnitOfWork, IntegrationUnitOfWork>();
        services.AddSingleton<IWebhookSigner, HmacWebhookSigner>();
        services.AddSingleton<ISecretResolver, ConfigurationSecretResolver>();
        services.AddHttpClient();
        // ConfigurationSecretResolver به IConfiguration وابسته است که در محیط
        // واقعی همیشه موجود است. یک پیکربندی خالی کافی است — رازها در آزمون
        // استفاده نمی‌شوند و resolving نباید شکست بخورد.
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Options.Create(new IntegrationOptions()));
        services.AddSingleton(Options.Create(new WebhookDeliveryOptions()));

        // ماژول پیکربندی سامانه: مخازن، سرویس و کش.
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<ISystemPolicyRepository, SystemPolicyRepository>();
        services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
        services.AddScoped<ISystemConfigurationUnitOfWork, SystemConfigurationUnitOfWork>();
        services.AddScoped<IFeatureFlagService, CachedFeatureFlagService>();
        services.AddScoped<ISettingService, CachedSettingService>();
        services.AddMemoryCache();
        services.AddSingleton(Options.Create(new SystemConfigurationOptions()));

        // انبار فایل مشترک (شاخه‌ی موقت، در پایان آزمون پاک می‌شود).
        services.AddSingleton(Options.Create(new FileStorageOptions { RootPath = evidenceRoot, MaxFileSizeMb = 25 }));
        services.AddSingleton<IFileStorage, FileSystemFileStorage>();
        services.AddSingleton<IFileUploadService, FileUploadService>();

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

        // ماژول گزارش‌گیری: رندرها، انبار فایل موقت و جمع‌آوری داده.
        services.AddScoped<IReportDefinitionRepository, ReportDefinitionRepository>();
        services.AddScoped<IReportExecutionRepository, ReportExecutionRepository>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IReportDataAssembler, ReportDataAssembler>();
        services.AddScoped<IReportRenderer, ExcelReportRenderer>();
        services.AddScoped<IReportRenderer, PdfReportRenderer>();

        // انبار فایل گزارش در آزمون: شاخه‌ی موقت که در پایان آزمون پاک می‌شود.
        services.AddSingleton<IReportArtifactStore, TempDirectoryReportArtifactStore>();

        // ماژول اعلان‌ها: مخازن، سرویس‌ها، رندر قالب، حل‌کننده‌ی گیرنده و
        // ارائه‌دهنده‌های تحویل (درون‌برنامه‌ای/ایمیل/پیامک).
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationDeliveryProvider, InAppNotificationProvider>();
        services.AddScoped<INotificationDeliveryProvider, EmailNotificationProvider>();
        services.AddScoped<INotificationDeliveryProvider, SmsNotificationProvider>();
        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddScoped<ISmsSender, NoOpSmsSender>();

        // پیاده‌سازی پیش‌فرض و بدون اثر هوش مصنوعی — هیچ داده‌ای از سامانه خارج نمی‌شود.
        services.AddSingleton<IAnalyticsAiService, NoOpAnalyticsAiService>();

        // ماژول مدیریت اقدامات: مخازن، سرویس و انبار پیوست‌ها (شاخه‌ی موقت).
        services.AddScoped<IActionPlanRepository, ActionPlanRepository>();
        services.AddScoped<IActionItemRepository, ActionItemRepository>();
        services.AddScoped<IActionManagementService, ActionManagementService>();
        services.AddSingleton(Options.Create(new ActionEvidenceOptions { RootPath = evidenceRoot }));
        services.AddSingleton<IActionEvidenceStore, FileSystemActionEvidenceStore>();
        services.AddSingleton(Options.Create(new ActionAnalyticsOptions()));

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
        var reportingDbContext = provider.GetRequiredService<ReportingDbContext>();
        var notificationDbContext = provider.GetRequiredService<NotificationDbContext>();
        var actionManagementDbContext = provider.GetRequiredService<ActionManagementDbContext>();
        var workflowDbContext = provider.GetRequiredService<WorkflowDbContext>();
        var integrationDbContext = provider.GetRequiredService<IntegrationDbContext>();
        var systemConfigurationDbContext = provider.GetRequiredService<SystemConfigurationDbContext>();

        await identityDbContext.Database.EnsureCreatedAsync();
        await organizationDbContext.Database.EnsureCreatedAsync();
        await auditDbContext.Database.EnsureCreatedAsync();
        await questionBankDbContext.Database.EnsureCreatedAsync();
        await questionnaireDbContext.Database.EnsureCreatedAsync();
        await surveyDbContext.Database.EnsureCreatedAsync();
        await campaignDbContext.Database.EnsureCreatedAsync();
        await responseDbContext.Database.EnsureCreatedAsync();
        await analyticsDbContext.Database.EnsureCreatedAsync();
        await reportingDbContext.Database.EnsureCreatedAsync();
        await notificationDbContext.Database.EnsureCreatedAsync();
        await actionManagementDbContext.Database.EnsureCreatedAsync();
        await workflowDbContext.Database.EnsureCreatedAsync();
        await integrationDbContext.Database.EnsureCreatedAsync();
        await systemConfigurationDbContext.Database.EnsureCreatedAsync();

        return new TestEnvironment(provider, identityDbContext, organizationDbContext,
            questionBankDbContext, questionnaireDbContext, surveyDbContext, campaignDbContext,
            responseDbContext, analyticsDbContext, reportingDbContext, notificationDbContext,
            actionManagementDbContext,
            workflowDbContext, integrationDbContext, systemConfigurationDbContext,
            identityConnection, organizationConnection, auditConnection,
            questionBankConnection, questionnaireConnection, surveyConnection, campaignConnection,
            responseConnection, analyticsConnection, reportingConnection, notificationConnection,
            actionManagementConnection, workflowConnection, integrationConnection,
            systemConfigurationConnection, evidenceRoot);
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

    /// <summary>
    /// تامین یک راز یکپارچه‌سازی برای آزمون. رازها در پیکربندی (نه پایگاه داده)
    /// نگه داشته می‌شوند؛ این متد مقدار را در گزینه‌های از پیش ثبت‌شده قرار
    /// می‌دهد تا <see cref="ISecretResolver"/> بتواند آن را بخواند.
    /// </summary>
    public void SetIntegrationsSecret(string secretRef, string secretValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretValue);

        var options = Services.GetRequiredService<IOptions<IntegrationOptions>>().Value;
        options.Secrets[secretRef] = secretValue;
    }

    /// <summary>
    /// تغییر تنظیمات ماژول یکپارچه‌سازی برای آزمون (مثلاً تلارانس برچسب زمانی
    /// وب‌هوک ورودی). گزینه‌ها از پیش به‌صورت singleton ثبت شده‌اند.
    /// </summary>
    public void SetIntegrationOptions(Action<IntegrationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(Services.GetRequiredService<IOptions<IntegrationOptions>>().Value);
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
        await ReportingDbContext.DisposeAsync();
        await NotificationDbContext.DisposeAsync();
        await ActionManagementDbContext.DisposeAsync();
        await WorkflowDbContext.DisposeAsync();
        await IntegrationDbContext.DisposeAsync();
        await SystemConfigurationDbContext.DisposeAsync();
        await _identityConnection.DisposeAsync();
        await _organizationConnection.DisposeAsync();
        await _auditConnection.DisposeAsync();
        await _questionBankConnection.DisposeAsync();
        await _questionnaireConnection.DisposeAsync();
        await _surveyConnection.DisposeAsync();
        await _campaignConnection.DisposeAsync();
        await _responseConnection.DisposeAsync();
        await _analyticsConnection.DisposeAsync();
        await _reportingConnection.DisposeAsync();
        await _notificationConnection.DisposeAsync();
        await _actionManagementConnection.DisposeAsync();
        await _workflowConnection.DisposeAsync();
        await _integrationConnection.DisposeAsync();
        await _systemConfigurationConnection.DisposeAsync();

        // پاک کردن شاخه‌ی موقت پیوست‌های اقدامات.
        try
        {
            if (Directory.Exists(_evidenceRoot))
            {
                Directory.Delete(_evidenceRoot, recursive: true);
            }
        }
        catch
        {
            // پاک‌سازی بهترین‌حالت است؛ نباید آزمون را شکست دهد.
        }

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

/// <summary>
/// انبار فایل خروجی گزارش برای آزمون‌ها: یک شاخسه‌ی موقت اختصاصی می‌سازد که
/// در زمان Dispose از روی دیسک پاک می‌شود. هیچ فایلی خارج از مسیر موقت
/// نوشته نمی‌شود.
/// </summary>
internal sealed class TempDirectoryReportArtifactStore : IReportArtifactStore
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "odcc-report-artifacts-" + Guid.NewGuid().ToString("N"));

    public Task<StoredArtifact> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var relativePath = Path.Combine(DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), fileName);
        var absolutePath = ToAbsolutePath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        using var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        content.CopyTo(fileStream);

        return Task.FromResult(new StoredArtifact(Normalize(relativePath), fileStream.Length));
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        Stream stream = new FileStream(ToAbsolutePath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var absolutePath = ToAbsolutePath(relativePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public bool Exists(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return File.Exists(ToAbsolutePath(relativePath));
    }

    private string ToAbsolutePath(string relativePath)
    {
        var absolute = Path.Combine(_root, relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        // جلوگیری از خروج مسیر از ریشه (path traversal).
        var fullRoot = Path.GetFullPath(_root);
        var fullPath = Path.GetFullPath(absolute);

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("مسیر فایل گزارش خارج از شاخسه‌ی مجاز است.");
        }

        return fullPath;
    }

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/');
}
