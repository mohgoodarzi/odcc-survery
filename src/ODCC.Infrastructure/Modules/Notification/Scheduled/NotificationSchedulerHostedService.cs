using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Notification.Abstractions;

namespace ODCC.Infrastructure.Modules.Notification.Scheduled;

/// <summary>
/// زمان‌بند تحویل به‌تعویق‌افتاده‌ی اعلان‌ها.
///
/// <c>SendAsync</c> تحویل بلافاصله را انجام می‌دهد، ولی دو حالت وجود دارد که
/// اعلان در صف می‌ماند و باید بعداً پردازش شود:
/// <list type="bullet">
///   <item>شکست موقت ارائه‌دهنده: <c>ScheduleRetry</c> تلاش بعدی را با تأخیر
///   نمایی برنامه‌ریزی می‌کند.</item>
///   <item>تحویل حجم بالا: هزاران دعوت‌نامه‌ی کمپین بهتر است در پس‌زمینه و در
///   دسته‌های کوچک پردازش شود تا درخواست کاربر مسدود نشود.</item>
/// </list>
///
/// چون این کار یک اثر جانبی است (ارسال پیام واقعی)، فقط زمانی فعال می‌شود که
/// <see cref="NotificationSchedulerOptions.EnableScheduler"/> در پیکربندی صحیح باشد.
/// </summary>
public sealed class NotificationSchedulerHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<NotificationSchedulerOptions> options,
    ILogger<NotificationSchedulerHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly NotificationSchedulerOptions _options = options.Value;
    private readonly ILogger<NotificationSchedulerHostedService> _logger = logger;

    private static readonly Action<ILogger, Exception> SchedulerFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "NotificationSchedulerFailed"),
        "یک دوره‌ی زمان‌بند تحویل اعلان‌ها شکست خورد.");

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
            var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

            await dispatcher.ProcessPendingAsync(_options.MaxPerCycle, stoppingToken);
        }
        catch (Exception ex)
        {
            // یک سرویس پس‌زمینه نباید به‌خاطر خطا از کار بیفتد.
            SchedulerFailed(_logger, ex);
        }
    }
}

/// <summary>
/// گزینه‌های زمان‌بند تحویل اعلان از بخش <c>Notifications:Scheduler</c> پیکربندی.
/// </summary>
public sealed class NotificationSchedulerOptions
{
    public const string SectionName = "Notifications:Scheduler";

    /// <summary>
    /// فعال‌سازی زمان‌بند تحویل. این یک اثر جانبی است (ارسال پیام) و طبق
    /// سیاست پروژه باید صریحاً فعال شود. پیش‌فرض <c>false</c> است.
    /// </summary>
    public bool EnableScheduler { get; set; }

    /// <summary>فاصله‌ی بررسی اعلان‌های آماده‌ی تحویل (ثانیه).</summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>حداکثر تعداد اعلان پردازش‌شده در هر دوره.</summary>
    public int MaxPerCycle { get; set; } = 100;
}
