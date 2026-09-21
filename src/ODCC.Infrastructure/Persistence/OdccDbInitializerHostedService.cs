using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ODCC.Infrastructure.Persistence.Audit;

namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// راه‌انداز پایگاه داده در زمان راه‌اندازی برنامه.
///
/// این سرویس فقط زمانی ثبت می‌شود که <c>Database:AutoMigrate</c> فعال باشد.
/// اجرای مهاجرت یک عملیات حساس روی پایگاه داده است و بر اساس سیاست پروژه
/// بدون تأیید صریح انجام نمی‌شود.
/// </summary>
public sealed class OdccDbInitializerHostedService(
    AuditDbContext auditDbContext,
    ILogger<OdccDbInitializerHostedService> logger) : IHostedService
{
    // برای کارایی و رعایت CA1848 از LoggerMessage.Define استفاده می‌شود.
    private static readonly Action<ILogger, Exception> DatabaseInitFailed = LoggerMessage.Define(
        LogLevel.Critical,
        new EventId(1, "DatabaseInitFailed"),
        "شکست در راه‌اندازی پایگاه داده در زمان بوت.");

    private readonly AuditDbContext _auditDbContext = auditDbContext;
    private readonly ILogger<OdccDbInitializerHostedService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _auditDbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // یک سامانه‌ی داخلی نباید به‌خاطر مشکل پایگاه داده از کار بیفتد؛
            // وضعیت تخریب از طریق /health گزارش می‌شود.
            DatabaseInitFailed(_logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
