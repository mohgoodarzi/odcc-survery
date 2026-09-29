using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Integration.Abstractions;

namespace ODCC.Infrastructure.Modules.Integration.Scheduled;

/// <summary>
/// زمان‌بند تحویل مجدد وب‌هوک‌ها: تحویل‌های ناموفقِ گذرا را با تأخیر نمایی
/// دوباره ارسال می‌کند.
///
/// این سرویس پس‌زمینه هر <see cref="WebhookDeliveryOptions.PollingIntervalSeconds"/>
/// ثانیه تحویل‌های در انتظارِ سررسیده‌شده را پیدا کرده و دوباره ارسال می‌کند.
///
/// <b>نکته‌ی طراحی:</b> از <see cref="IServiceScopeFactory"/> استفاده می‌شود چون
/// DbContext و سرویس‌های زیرین Scoped هستند.
///
/// چون ارسال به سرویس خارجی یک اثر جانبی است، فقط با تأیید صریح
/// (<c>Integrations:Delivery:EnableDeliveryScheduler</c>) فعال می‌شود. تحویل
/// بلافاصله (در زمان رویداد) همیشه فعال است.
/// </summary>
public sealed class WebhookDeliveryHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<WebhookDeliveryOptions> options,
    ILogger<WebhookDeliveryHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly WebhookDeliveryOptions _options = options.Value;
    private readonly ILogger<WebhookDeliveryHostedService> _logger = logger;

    private static readonly Action<ILogger, Exception> CycleFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "WebhookDeliveryCycleFailed"),
        "یک دوره‌ی زمان‌بند تحویل وب‌هوک شکست خورد.");

    private static readonly Action<ILogger, int, Exception?> CycleCompleted = LoggerMessage.Define<int>(
        LogLevel.Information,
        new EventId(2, "WebhookDeliveryCycle"),
        "زمان‌بند تحویل {Count} وب‌هوک را پردازش کرد.");

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableDeliveryScheduler)
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
            var service = scope.ServiceProvider.GetRequiredService<IIntegrationService>();

            var processed = await service.ProcessPendingDeliveriesAsync(DateTime.UtcNow, stoppingToken);

            if (processed > 0)
            {
                CycleCompleted(_logger, processed, null);
            }
        }
        catch (Exception ex)
        {
            // یک سرویس پس‌زمینه نباید به‌خاطر خطا از کار بیفتد.
            CycleFailed(_logger, ex);
        }
    }
}
