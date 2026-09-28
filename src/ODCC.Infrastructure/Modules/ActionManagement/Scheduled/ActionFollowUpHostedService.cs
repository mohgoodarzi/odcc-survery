using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.ActionManagement.Abstractions;

namespace ODCC.Infrastructure.Modules.ActionManagement.Scheduled;

/// <summary>
/// زمان‌بند پیگیری خودکار آیتم‌های اقدام: یادآورهای سررسیده و تشدید
/// آیتم‌های سررسیده‌شده.
///
/// این سرویس پس‌زمینه هر <see cref="ActionFollowUpOptions.PollingIntervalSeconds"/>
/// ثانیه آیتم‌های بازی که زمان یادآوری‌شان رسیده را پیدا کرده، رویداد
/// <c>ActionItemReminderDueEvent</c> را منتشر می‌کند (ارسال پیام در ماژول
/// اعلان‌ها انجام می‌شود) و آیتم‌های سررسیده‌شده را یک درجه تشدید می‌کند.
///
/// <b>نکته‌ی طراحی:</b> از <see cref="IServiceScopeFactory"/> استفاده می‌شود چون
/// DbContext و سرویس‌های زیرین Scoped هستند و نباید در طول عمر singleton
/// نگه‌داری شوند. در هر scope، سرویس بدون HttpContext کار می‌کند؛ در نتیجه
/// کاربر جاری <c>null</c> است و مرز دامنه در سطح کلیدی (نه کاربر) اعمال می‌شود.
///
/// چون ارسال پیام یک اثر جانبی است، فقط با تأیید صریح
/// (<c>Actions:FollowUp:EnableFollowUpScheduler</c>) فعال می‌شود.
/// </summary>
public sealed class ActionFollowUpHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ActionFollowUpOptions> options,
    ILogger<ActionFollowUpHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly ActionFollowUpOptions _options = options.Value;
    private readonly ILogger<ActionFollowUpHostedService> _logger = logger;

    private static readonly Action<ILogger, Exception> CycleFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "ActionFollowUpCycleFailed"),
        "یک دوره‌ی زمان‌بند پیگیری اقدامات شکست خورد.");

    private static readonly Action<ILogger, int, int, Exception?> CycleCompleted = LoggerMessage.Define<int, int>(
        LogLevel.Information,
        new EventId(2, "ActionFollowUpCycle"),
        "زمان‌بند پیگیری {Reminders} یادآور و {Escalations} تشدید پردازش کرد.");

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableFollowUpScheduler)
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
            var service = scope.ServiceProvider.GetRequiredService<IActionManagementService>();

            var now = DateTime.UtcNow;

            var reminders = await service.ProcessDueRemindersAsync(now, stoppingToken);
            var escalations = await service.ProcessOverdueEscalationsAsync(now, stoppingToken);

            if (reminders > 0 || escalations > 0)
            {
                CycleCompleted(_logger, reminders, escalations, null);
            }
        }
        catch (Exception ex)
        {
            // یک سرویس پس‌زمینه نباید به‌خاطر خطا از کار بیفتد.
            CycleFailed(_logger, ex);
        }
    }
}
