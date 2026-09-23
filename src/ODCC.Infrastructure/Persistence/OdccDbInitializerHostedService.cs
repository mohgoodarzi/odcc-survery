using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ODCC.Infrastructure.Modules.Identity.Persistence;
using ODCC.Infrastructure.Modules.Organization.Persistence;
using ODCC.Infrastructure.Modules.QuestionBank.Persistence;
using ODCC.Infrastructure.Modules.Questionnaire.Persistence;
using ODCC.Infrastructure.Persistence.Audit;

namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// راه‌انداز پایگاه داده در زمان راه‌اندازی برنامه.
///
/// این سرویس فقط زمانی ثبت می‌شود که <c>Database:AutoMigrate</c> فعال باشد.
/// اجرای مهاجرت و داده‌ی اولیه یک عملیات حساس روی پایگاه داده است و بر اساس
/// سیاست پروژه بدون تأیید صریح انجام نمی‌شود.
///
/// <b>نکته‌ی طراحی:</b> این سرویس از <see cref="IServiceScopeFactory"/> برای
/// ساخت یک scope استفاده می‌کند چون DbContextها و <see cref="IDataSeeder"/>
/// سرویس‌های Scoped هستند و نباید در طول عمر singleton نگه‌داری شوند.
/// </summary>
public sealed class OdccDbInitializerHostedService(
    IServiceScopeFactory serviceScopeFactory,
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
    private readonly ILogger<OdccDbInitializerHostedService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        try
        {
            // هر ماژول DbContext و مهاجرت‌های مستقل خود را دارد. ترتیب اجرای
            // مهاجرت‌ها اهمیتی ندارد چون جداول ماژول‌ها مستقل هستند.
            await MigrateContextAsync<AuditDbContext>(provider, cancellationToken);
            await MigrateContextAsync<IdentityDbContext>(provider, cancellationToken);
            await MigrateContextAsync<OrganizationDbContext>(provider, cancellationToken);
            await MigrateContextAsync<QuestionBankDbContext>(provider, cancellationToken);
            await MigrateContextAsync<QuestionnaireDbContext>(provider, cancellationToken);
        }
        catch (Exception ex)
        {
            // یک سامانه‌ی داخلی نباید به‌خاطر مشکل پایگاه داده از کار بیفتد؛
            // وضعیت تخریب از طریق /health گزارش می‌شود.
            DatabaseInitFailed(_logger, ex);
            return;
        }

        // داده‌ی اولیه فقط پس از موفقیت کامل مهاجرت‌ها اجرا می‌شود.
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
