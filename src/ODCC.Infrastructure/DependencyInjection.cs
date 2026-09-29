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
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.Workflow.Abstractions;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.SystemConfiguration.Abstractions;
using ODCC.Infrastructure.Modules.ActionManagement.EventListeners;
using ODCC.Infrastructure.Modules.ActionManagement.Persistence;
using ODCC.Infrastructure.Modules.ActionManagement.Repositories;
using ODCC.Infrastructure.Modules.ActionManagement.Scheduled;
using ODCC.Infrastructure.Modules.ActionManagement.Services;
using ODCC.Infrastructure.Modules.Workflow.EventListeners;
using ODCC.Infrastructure.Modules.Workflow.Persistence;
using ODCC.Infrastructure.Modules.Workflow.Repositories;
using ODCC.Infrastructure.Modules.Workflow.Scheduled;
using ODCC.Infrastructure.Modules.Workflow.Services;
using ODCC.Infrastructure.Modules.Integration.EventListeners;
using ODCC.Infrastructure.Modules.Integration.Persistence;
using ODCC.Infrastructure.Modules.Integration.Repositories;
using ODCC.Infrastructure.Modules.Integration.Scheduled;
using ODCC.Infrastructure.Modules.Integration.Services;
using ODCC.Infrastructure.Modules.SystemConfiguration.EventListeners;
using ODCC.Infrastructure.Modules.SystemConfiguration.Persistence;
using ODCC.Infrastructure.Modules.SystemConfiguration.Repositories;
using ODCC.Infrastructure.Modules.SystemConfiguration.Services;
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
using ODCC.Application.Modules.FileStorage.Abstractions;
using ODCC.Infrastructure.Modules.FileStorage.Services;

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

        // شنونده‌ی اعلان‌ها: انتصاب، یادآور، تشدید و تکمیل آیتم‌های اقدام را به
        // پیام تبدیل می‌کند. این شنونده در ماژول اعلان‌ها ثبت می‌شود چون
        // موجودیت‌های ساخته‌شده (Notification) متعلق به این ماژول است، ولی به
        // رویدادهای ماژول مدیریت اقدامات گوش می‌دهد.
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionItemCreatedEvent>, ActionNotificationEventListener>();
        // نکته: تغییر وضعیت آیتم عمداً اعلان نمی‌شود (فقط ممیزی می‌شود) تا
        // صندوق ورودی کاربران با اعلان‌های کم‌اهمیت پر نشود.
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionItemCompletedEvent>, ActionNotificationEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionItemReminderDueEvent>, ActionNotificationEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionItemEscalatedEvent>, ActionNotificationEventListener>();

        // شنونده‌ی مدیریت اقدامات: هشدارهای تحلیلات (افت NPS/CSAT/CES) را به
        // برنامه‌ی اقدام خودکار تبدیل می‌کند. این شنونده در ماژول مدیریت
        // اقدامات ثبت می‌شود چون موجودیت‌های ساخته‌شده (ActionPlan) متعلق به
        // همین ماژول است، ولی به رویدادهای ماژول تحلیلات گوش می‌دهد.
        services.AddScoped<IDomainEventListener<Domain.Modules.Analytics.Events.AnalyticsComputedEvent>, AnalyticsActionEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای مدیریت اقدامات.
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionPlanCreatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionPlanUpdatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionPlanActivatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionPlanCompletedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionPlanCancelledEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionPlanArchivedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionItemCreatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionItemUpdatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionItemStatusChangedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionItemEscalatedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionCommentAddedEvent>, ActionManagementAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.ActionManagement.Events.ActionEvidenceUploadedEvent>, ActionManagementAuditEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای گردش کار: ایجاد/ویرایش/فعال‌سازی/بایگانی
        // تعاریف، شروع/گذار/تکمیل/لغو نمونه‌ها و درخواست‌های تأیید.
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowCreatedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowUpdatedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowActivatedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowArchivedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowInstanceStartedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowInstanceTransitionedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowInstanceCompletedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowInstanceCancelledEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowApprovalRequestedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowApprovalDecidedEvent>, WorkflowAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowApprovalExpiredEvent>, WorkflowAuditEventListener>();

        // شنونده‌ی اعلان‌ها: درخواست‌های تأیید گردش کار را به کاربرانی که مجوز
        // تأیید دارند تبدیل به پیام می‌کند. این شنونده در ماژول اعلان‌ها ثبت
        // می‌شود چون موجودیت‌های ساخته‌شده (Notification) متعلق به آن ماژول است.
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowApprovalRequestedEvent>, WorkflowNotificationEventListener>();

        // شنونده‌ی یکپارچه‌سازی: رویدادهای قابل‌اشتراک‌گذاری را به وب‌هوک‌های
        // فعال تحویل می‌دهد. این شنونده در ماژول یکپارچه‌سازی ثبت می‌شود چون
        // موجودیت‌های ساخته‌شده (WebhookDelivery) متعلق به آن ماژول است.
        services.AddScoped<IDomainEventListener<Domain.Modules.Analytics.Events.AnalyticsComputedEvent>, IntegrationWebhookEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Response.Events.ResponseSubmittedEvent>, IntegrationWebhookEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Workflow.Events.WorkflowInstanceTransitionedEvent>, IntegrationWebhookEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای یکپارچه‌سازی: ایجاد/ویرایش/فعال‌سازی
        // اندپوینت‌ها و تحویل/شکست وب‌هوک.
        services.AddScoped<IDomainEventListener<Domain.Modules.Integration.Events.IntegrationEndpointCreatedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Integration.Events.IntegrationEndpointUpdatedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Integration.Events.IntegrationEndpointActivatedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Integration.Events.IntegrationEndpointArchivedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Integration.Events.WebhookDeliveredEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Integration.Events.WebhookDeliveryFailedEvent>, IntegrationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.Integration.Events.InboundWebhookReceivedEvent>, IntegrationAuditEventListener>();

        // شنونده‌ی ممیزی برای رویدادهای پیکربندی سامانه: تغییر تنظیمات، پرچم‌های
        // ویژگی و سیاست‌های سیستمی.
        services.AddScoped<IDomainEventListener<Domain.Modules.SystemConfiguration.Events.SettingChangedEvent>, SystemConfigurationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.SystemConfiguration.Events.FeatureFlagChangedEvent>, SystemConfigurationAuditEventListener>();
        services.AddScoped<IDomainEventListener<Domain.Modules.SystemConfiguration.Events.SystemPolicyChangedEvent>, SystemConfigurationAuditEventListener>();

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
        ConfigureActionManagement(services, connectionString, configure);
        ConfigureWorkflow(services, connectionString, configure);
        ConfigureIntegration(services, connectionString, configure);
        ConfigureSystemConfiguration(services, connectionString, configure);

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

    // --- ماژول مدیریت اقدامات ----------------------------------------------

    private static void ConfigureActionManagement(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<ActionManagementDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IActionPlanRepository, ActionPlanRepository>();
        services.AddScoped<IActionItemRepository, ActionItemRepository>();
        services.AddScoped<IActionManagementService, ActionManagementService>();

        // انبار پیوست‌ها: شاخه‌ی ریشه‌ی پیکربندی‌شده با جلوگیری از path traversal.
        services.AddSingleton<IActionEvidenceStore, FileSystemActionEvidenceStore>();

        services.AddScoped<IActionManagementUnitOfWork, ActionManagementUnitOfWork>();
    }

    /// <summary>
    /// راه‌اندازی بخش پیکربندی ماژول مدیریت اقدامات: تنظیمات پیگیری خودکار،
    /// هشدارهای تحلیلات و انبار پیوست‌ها.
    ///
    /// طبق سیاست پروژه، پردازش پس‌زمینه (یادآور/تشدید) یک اثر جانبی است و فقط
    /// با تأیید صریح (<c>Actions:FollowUp:EnableFollowUpScheduler</c>) فعال می‌شود.
    /// </summary>
    public static IServiceCollection AddOdccActions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ActionFollowUpOptions>(configuration.GetSection(ActionFollowUpOptions.SectionName));
        services.Configure<ActionEvidenceOptions>(configuration.GetSection(ActionEvidenceOptions.SectionName));
        services.Configure<ActionAnalyticsOptions>(configuration.GetSection(ActionAnalyticsOptions.SectionName));

        var enableFollowUp = configuration.GetValue<bool?>($"{ActionFollowUpOptions.SectionName}:EnableFollowUpScheduler") is true;

        if (enableFollowUp)
        {
            services.AddHostedService<ActionFollowUpHostedService>();
        }

        return services;
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

    /// <summary>
    /// راه‌اندازی بخش پیکربندی ماژول گردش کار: زمان‌بند انقضای درخواست‌های تأیید.
    ///
    /// طبق سیاست پروژه، پردازش پس‌زمینه یک اثر جانبی است و فقط با تأیید صریح
    /// (<c>Workflows:EnableApprovalExpiryScheduler</c>) فعال می‌شود.
    /// </summary>
    public static IServiceCollection AddOdccWorkflow(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkflowSchedulerOptions>(configuration.GetSection(WorkflowSchedulerOptions.SectionName));

        var enableScheduler = configuration.GetValue<bool?>($"{WorkflowSchedulerOptions.SectionName}:EnableApprovalExpiryScheduler") is true;

        if (enableScheduler)
        {
            services.AddHostedService<WorkflowApprovalExpiryHostedService>();
        }

        return services;
    }

    /// <summary>
    /// راه‌اندازی بخش پیکربندی ماژول یکپارچه‌سازی: وب‌هوک‌های خروجی،
    /// زمان‌بند تحویل مجدد و رمزنگاری/تأیید امضا.
    ///
    /// طبق سیاست پروژه، پردازش پس‌زمینه یک اثر جانبی است و فقط با تأیید صریح
    /// (<c>Integrations:EnableDeliveryScheduler</c>) فعال می‌شود. تحویل
    /// بلافاصله (در همان رویداد) همیشه فعال است.
    /// </summary>
    public static IServiceCollection AddOdccIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IntegrationOptions>(configuration.GetSection(IntegrationOptions.SectionName));
        services.Configure<WebhookDeliveryOptions>(configuration.GetSection(WebhookDeliveryOptions.SectionName));

        // کلاینت HTTP اختصاصی برای تماس با اندپوینت‌های خارجی: مهلت زمانی
        // قابل پیکربندی و بدون بازگرداندن استثنا در شکست (پاسخ برمی‌گردد).
        services.AddHttpClient(IntegrationHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // امضای وب‌هوک با HMAC-SHA256.
        services.AddSingleton<IWebhookSigner, HmacWebhookSigner>();

        // خواندن رازها از پیکربندی ایمن (user secrets / متغیرهای محیطی).
        services.AddSingleton<ISecretResolver, ConfigurationSecretResolver>();

        var enableScheduler = configuration.GetValue<bool?>($"{WebhookDeliveryOptions.SectionName}:EnableDeliveryScheduler") is true;

        if (enableScheduler)
        {
            services.AddHostedService<WebhookDeliveryHostedService>();
        }

        return services;
    }

    /// <summary>
    /// راه‌اندازی بخش پیکربندی ماژول پیکربندی سامانه: کش پرچم‌های ویژگی.
    ///
    /// <b>توجه:</b> پیاده‌سازی‌های کش‌شده‌ی <see cref="IFeatureFlagService"/> و
    /// <see cref="ISettingService"/> در <see cref="ConfigureSystemConfiguration"/> به‌صورت
    /// Scoped ثبت می‌شوند چون به مخازن Scoped (و در نتیجه DbContext Scoped) وابسته‌اند.
    /// ثبت مجدد آن‌ها به‌صورت Singleton در اینجا باعث وابستگی اسیر (captive dependency)
    /// می‌شود: یک DbContext کهScoped است در یک Singleton اسیر شده، هرگز dispose نمی‌شود
    /// و به‌صورت همزمان بین درخواست‌ها استفاده می‌شود که EF Core از آن پشتیبانی
    /// نمی‌کند. بنابراین در اینجا فقط گزینه‌ها پیکربندی می‌شوند.
    /// </summary>
    public static IServiceCollection AddOdccSystemConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SystemConfigurationOptions>(configuration.GetSection(SystemConfigurationOptions.SectionName));

        return services;
    }

    /// <summary>نام کلاینت HTTP ماژول یکپارچه‌سازی (برای تزریق <c>IHttpClientFactory</c>).</summary>
    public const string IntegrationHttpClientName = "odcc-integrations";

    private static void ConfigureWorkflow(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<WorkflowDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<IWorkflowApprovalRepository, WorkflowApprovalRepository>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IWorkflowUnitOfWork, WorkflowUnitOfWork>();
    }

    private static void ConfigureIntegration(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<IntegrationDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<IIntegrationEndpointRepository, IntegrationEndpointRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IIntegrationService, IntegrationService>();
        services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
        services.AddScoped<IIntegrationUnitOfWork, IntegrationUnitOfWork>();
    }

    private static void ConfigureSystemConfiguration(
        IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<SystemConfigurationDbContext>(options =>
        {
            ConfigureSql(options, connectionString);
            configure?.Invoke(options);
        });

        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<ISystemPolicyRepository, SystemPolicyRepository>();
        services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
        services.AddScoped<ISystemConfigurationUnitOfWork, SystemConfigurationUnitOfWork>();
        services.AddScoped<IFeatureFlagService, CachedFeatureFlagService>();
        services.AddScoped<ISettingService, CachedSettingService>();

        services.AddMemoryCache();
    }

    private static void ConfigureSql(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
    }

    /// <summary>
    /// انبار امن فایل: بارگذاری، دانلود و حذف با اعتبارسنجی پسوند/اندازه و
    /// جلوگیری از path traversal.
    /// </summary>
    public static IServiceCollection AddOdccFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        services.AddSingleton<IFileStorage, FileSystemFileStorage>();
        services.AddSingleton<IFileUploadService, FileUploadService>();

        return services;
    }

    /// <summary>
    /// صف کارهای پس‌زمینه.
    ///
    /// طبق سیاست پروژه، پردازش پس‌زمینه یک اثر جانبی است و فقط با تأیید صریح
    /// (<c>BackgroundJobs:EnableProcessor</c>) فعال می‌شود. صف همیشه قابل
    /// استفاده است (کارها داخل آن قرار می‌گیرند) ولی اجرای آن‌ها وابسته به
    /// این پرچم است.
    /// </summary>
    public static IServiceCollection AddOdccBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BackgroundJobQueueOptions>(configuration.GetSection(BackgroundJobQueueOptions.SectionName));

        services.AddSingleton<IBackgroundJobRunner, BackgroundJobRunner>();

        var enableProcessor = configuration.GetValue<bool?>($"{BackgroundJobQueueOptions.SectionName}:EnableProcessor") is true;

        if (enableProcessor)
        {
            services.AddHostedService<BackgroundJobRunnerHostedService>();
        }

        return services;
    }
}
