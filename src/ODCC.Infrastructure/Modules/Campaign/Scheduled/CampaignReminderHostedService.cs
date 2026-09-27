using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Campaign.Abstractions;

namespace ODCC.Infrastructure.Modules.Campaign.Scheduled;

/// <summary>
/// زمان‌بند پردازش خودکار یادآورهای سررسیده‌ی کمپین‌ها.
///
/// این سرویس پس‌زمینه هر <see cref="CampaignReminderOptions.ReminderPollingIntervalSeconds"/>
/// ثانیه یادآورهای رسیده را پیدا کرده، آن‌ها را «ارسال‌شده» علامت می‌زند و
/// <c>ReminderDueEvent</c> منتشر می‌کند. ارسال واقعی پیام در ماژول اعلان‌ها انجام
/// می‌شود. چون این کار یک اثر جانبی است، فقط زمانی فعال می‌شود که
/// <see cref="CampaignReminderOptions.EnableReminderScheduler"/> در پیکربندی صحیح باشد.
///
/// <b>نکته‌ی طراحی:</b> از <see cref="IServiceScopeFactory"/> استفاده می‌شود چون
/// سرویس‌های زیرین Scoped هستند و نباید در طول عمر singleton نگه‌داری شوند.
/// </summary>
public sealed class CampaignReminderHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<CampaignReminderOptions> options,
    ILogger<CampaignReminderHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly CampaignReminderOptions _options = options.Value;
    private readonly ILogger<CampaignReminderHostedService> _logger = logger;

    private static readonly Action<ILogger, Exception> CycleFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "CampaignReminderCycleFailed"),
        "یک دوره‌ی زمان‌بند یادآورهای کمپین شکست خورد.");

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableReminderScheduler)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(10, _options.ReminderPollingIntervalSeconds));

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
            var campaignService = scope.ServiceProvider.GetRequiredService<ICampaignService>();

            await campaignService.ProcessDueRemindersAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            // یک سرویس پس‌زمینه نباید به‌خاطر خطا از کار بیفتد.
            CycleFailed(_logger, ex);
        }
    }
}
