using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ODCC.Infrastructure.Modules.Identity.Persistence;
using ODCC.Infrastructure.Modules.Organization.Persistence;
using ODCC.Infrastructure.Modules.QuestionBank.Persistence;
using ODCC.Infrastructure.Modules.Questionnaire.Persistence;
using ODCC.Infrastructure.Modules.Survey.Persistence;
using ODCC.Infrastructure.Modules.Campaign.Persistence;
using ODCC.Infrastructure.Modules.Response.Persistence;
using ODCC.Infrastructure.Modules.Reporting.Persistence;
using ODCC.Infrastructure.Modules.Notification.Persistence;
using ODCC.Infrastructure.Persistence.Audit;

namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// راه‌انداز پایگاه داده در زمان راه‌اندازی برنامه.
///
/// <b>مهاجرت</b> فقط زمانی اجرا می‌شود که <c>Database:AutoMigrate</c> فعال باشد
/// (طبق سیاست پروژه، تغییر ساختار پایگاه داده نیازمند تأیید صریح است).
///
/// <b>داده‌ی اولیه</b> همیشه اجرا می‌شود. برخلاف مهاجرت، کاشت داده افزودنی و
/// خودتوان است: گام‌ها پیش از اجرا وجود داده را بررسی می‌کنند و
/// <c>SyncAdminPermissionsAsync</c> فقط مجوزهای جدید را اضافه می‌کند و هرگز
/// مجوزهای موجود را حذف نمی‌کند. اگر کاشت همراه با مهاجرت مشروط می‌شد، با
/// <c>AutoMigrate=false</c> مجوزهای ماژول‌های جدید (مثلاً <c>notifications.view</c>)
/// هرگز به نقش مدیر کل موجود نمی‌رسیدند و کاربران قدیمی پس از ارتقا با خطای
/// ۴۰۳ مواجه می‌شدند، در حالی که پایگاه داده‌ی آن‌ها از قبل مهاجرت شده است.
///
/// <b>نکته‌ی طراحی:</b> این سرویس از <see cref="IServiceScopeFactory"/> برای
/// ساخت یک scope استفاده می‌کند چون DbContextها و <see cref="IDataSeeder"/>
/// سرویس‌های Scoped هستند و نباید در طول عمر singleton نگه‌داری شوند.
/// </summary>
public sealed class OdccDbInitializerHostedService(
    IServiceScopeFactory serviceScopeFactory,
    bool autoMigrate,
    ILogger<OdccDbInitializerHostedService> logger) : IHostedService
{
    // برای کارایی و رعایت CA1848 از LoggerMessage.Define استفاده می‌شود.
    private static readonly Action<ILogger, Exception> DatabaseInitFailed = LoggerMessage.Define(
        LogLevel.Critical,
        new EventId(1, "DatabaseInitFailed"),
        "شکست در راه‌اندازی پایگاه داده در زمان بوت.");

    private static readonly Action<ILogger, Exception> SeedFailed = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(2, "SeedFailed"),
        "ایجاد داده‌ی اولیه پس از مهاجرت ناموفق بود.");

    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly bool _autoMigrate = autoMigrate;
    private readonly ILogger<OdccDbInitializerHostedService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        if (_autoMigrate)
        {
            // هر ماژول DbContext و مهاجرت‌های مستقل خود را دارد. ترتیب اجرای
            // مهاجرت‌ها اهمیتی ندارد چون جداول ماژول‌ها مستقل هستند.
            try
            {
                await MigrateContextAsync<AuditDbContext>(provider, cancellationToken);
                await MigrateContextAsync<IdentityDbContext>(provider, cancellationToken);
                await MigrateContextAsync<OrganizationDbContext>(provider, cancellationToken);
                await MigrateContextAsync<QuestionBankDbContext>(provider, cancellationToken);
                await MigrateContextAsync<QuestionnaireDbContext>(provider, cancellationToken);
                await MigrateContextAsync<SurveyDbContext>(provider, cancellationToken);
                await MigrateContextAsync<CampaignDbContext>(provider, cancellationToken);
                await MigrateContextAsync<ResponseDbContext>(provider, cancellationToken);
                await MigrateContextAsync<ReportingDbContext>(provider, cancellationToken);
                await MigrateContextAsync<NotificationDbContext>(provider, cancellationToken);
            }
            catch (Exception ex)
            {
                // یک سامانه‌ی داخلی نباید به‌خاطر مشکل پایگاه داده از کار بیفتد؛
                // وضعیت تخریب از طریق /health گزارش می‌شود.
                DatabaseInitFailed(_logger, ex);
                return;
            }
        }

        // داده‌ی اولیه همیشه اجرا می‌شود (حتی با AutoMigrate=false) چون پایگاه
        // داده‌ی موجود از قبل مهاجرت شده است و کاشت فقط داده‌ی غایب را پر می‌کند.
        try
        {
            var seeder = provider.GetRequiredService<IDataSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            SeedFailed(_logger, ex);
        }
    }

    private static async Task MigrateContextAsync<TContext>(IServiceProvider provider, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var context = provider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
