using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Workflow.Abstractions;

namespace ODCC.Infrastructure.Modules.Workflow.Scheduled;

/// <summary>
/// زمان‌بند انقضای درخواست‌های تأیید گردش کار.
///
/// درخواست‌های تأییدی که زمان انقضای آن‌ها رسیده و هنوز در انتظار هستند را
/// منقضی می‌کند تا مسدود نشدن نمونه‌ها جلوگیری شود. این یک اثر جانبی است و
/// فقط با تأیید صریح (<c>Workflows:EnableApprovalExpiryScheduler</c>) فعال
/// می‌شود.
/// </summary>
public sealed class WorkflowApprovalExpiryHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<WorkflowSchedulerOptions> options,
    ILogger<WorkflowApprovalExpiryHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly WorkflowSchedulerOptions _options = options.Value;
    private readonly ILogger<WorkflowApprovalExpiryHostedService> _logger = logger;

    private static readonly Action<ILogger, Exception> CycleFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "WorkflowExpiryCycleFailed"),
        "یک دوره‌ی زمان‌بند انقضای تأییدها شکست خورد.");

    private static readonly Action<ILogger, int, Exception?> CycleCompleted = LoggerMessage.Define<int>(
        LogLevel.Information,
        new EventId(2, "WorkflowExpiryCycle"),
        "زمان‌بند انقضا {Count} درخواست تأیید را منقضی کرد.");

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableApprovalExpiryScheduler)
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
            var service = scope.ServiceProvider.GetRequiredService<IWorkflowService>();

            var expired = await service.ExpireDueApprovalsAsync(DateTime.UtcNow, stoppingToken);

            if (expired > 0)
            {
                CycleCompleted(_logger, expired, null);
            }
        }
        catch (Exception ex)
        {
            // یک سرویس پس‌زمینه نباید به‌خاطر خطا از کار بیفتد.
            CycleFailed(_logger, ex);
        }
    }
}
