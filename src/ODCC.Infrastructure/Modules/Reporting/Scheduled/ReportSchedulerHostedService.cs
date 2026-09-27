using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Reporting.Abstractions;

namespace ODCC.Infrastructure.Modules.Reporting.Scheduled;

/// <summary>
/// زمان‌بند اجرای خودکار گزارش‌ها.
///
/// این سرویس پس‌زمینه هر <see cref="ReportSchedulerOptions.PollingIntervalSeconds"/>
/// ثانیه تعاریف فعال زمان‌بندی‌شده‌ای را که زمان اجرایشان رسیده پیدا کرده و
/// آن‌ها را اجرا می‌کند. اجرای خودکار یک اثر جانبی است و فقط زمانی فعال می‌شود
/// که <see cref="ReportSchedulerOptions.EnableScheduler"/> در پیکربندی صحیح باشد.
///
/// <b>نکته‌ی طراحی:</b> این سرویس از <see cref="IServiceScopeFactory"/> استفاده
/// می‌کند چون <c>DbContext</c>ها و سرویس‌های زیرین Scoped هستند و نباید در طول
/// عمر singleton نگه‌داری شوند. در هر scope، <c>IReportingService</c> بدون
/// <c>HttpContext</c> کار می‌کند؛ در نتیجه کاربر راه‌انداز <c>null</c> است و
/// دامنه‌ی تثبیت‌شده در تعریف گزارش معیار است.
/// </summary>
public sealed class ReportSchedulerHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ReportSchedulerOptions> options,
    ILogger<ReportSchedulerHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly ReportSchedulerOptions _options = options.Value;
    private readonly ILogger<ReportSchedulerHostedService> _logger = logger;

    private static readonly Action<ILogger, Exception> SchedulerFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "ReportSchedulerFailed"),
        "یک دوره‌ی زمان‌بند گزارش‌ها شکست خورد.");

    private static readonly Action<ILogger, int, int, Exception?> CycleCompleted = LoggerMessage.Define<int, int>(
        LogLevel.Information,
        new EventId(2, "ReportSchedulerCycle"),
        "زمان‌بند {Processed} گزارش از {Total} گزارش رسیده را اجرا کرد.");

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableScheduler)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(10, _options.PollingIntervalSeconds));

        using var timer = new PeriodicTimer(interval);

        try
        {
            do
            {
                await RunCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // خاموشی طبیعی برنامه.
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();

            // محدود کردن تعداد اجراها در هر دوره برای جلوگیری از انفجار بار.
            var maxPerCycle = Math.Max(1, _options.MaxReportsPerCycle);
            var processed = 0;

            for (var i = 0; i < maxPerCycle; i++)
            {
                var executed = await reportingService.ProcessDueReportsAsync(DateTime.UtcNow, stoppingToken);

                if (executed == 0)
                {
                    break;
                }

                processed += executed;
            }

            if (processed > 0)
            {
                CycleCompleted(_logger, processed, processed, null);
            }
        }
        catch (Exception ex)
        {
            // یک سرویس پس‌زمینه نباید به‌خاطر خطا از کار بیفتد.
            SchedulerFailed(_logger, ex);
        }
    }
}
