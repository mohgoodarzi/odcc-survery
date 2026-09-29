using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;

namespace ODCC.Infrastructure.Services;

/// <summary>
/// صف درون‌حافظه‌ای کارهای پس‌زمینه.
///
/// از یک <see cref="Channel{T}"/> نامحدود‌-but-bounded استفاده می‌کند: اگر
/// ظرفیت پر شود، کار جدید رد می‌شود تا سرعت تولید بیش از مصرف نشود.
/// </summary>
public sealed class BackgroundJobRunner : IBackgroundJobRunner
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;
    private readonly BackgroundJobQueueOptions _options;

    public BackgroundJobRunner(IOptions<BackgroundJobQueueOptions> options)
    {
        _options = options.Value;
        var capacity = Math.Clamp(_options.QueueCapacity, 16, 100_000);

        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = false,
                SingleWriter = false
            });
    }

    /// <inheritdoc/>
    public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        // اگر صف پر باشد، کار رد می‌شود (DropWrite) تا فشار بیش از حد نباشد.
        _queue.Writer.TryWrite(workItem);
    }

    /// <inheritdoc/>
    public void Enqueue(Func<CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        Enqueue((_, ct) => workItem(ct));
    }

    /// <inheritdoc/>
    public async Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken) =>
        await _queue.Reader.ReadAsync(cancellationToken);
}

/// <summary>
/// پردازشگر کارهای پس‌زمینه: تعدادی worker که از صف می‌خوانند و اجرا می‌کنند.
///
/// <b>خطا:</b> هر کار در try/catch اجرا می‌شود؛ شکست یک کار نباید پردازشگر
/// را از کار بیندازد.
/// </summary>
public sealed class BackgroundJobRunnerHostedService(
    IServiceProvider serviceProvider,
    IBackgroundJobRunner queue,
    IOptions<BackgroundJobQueueOptions> options,
    ILogger<BackgroundJobRunnerHostedService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IBackgroundJobRunner _queue = queue;
    private readonly BackgroundJobQueueOptions _options = options.Value;
    private readonly ILogger<BackgroundJobRunnerHostedService> _logger = logger;

    private static readonly Action<ILogger, Exception> JobFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "BackgroundJobFailed"),
        "اجرای یک کار پس‌زمینه شکست خورد.");

    private static readonly Action<ILogger, Exception> ProcessorFatal = LoggerMessage.Define(
        LogLevel.Critical,
        new EventId(2, "BackgroundJobProcessorFatal"),
        "پردازشگر کارهای پس‌زمینه به‌خاطر خطای غیرمنتظره متوقف شد.");

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableProcessor)
        {
            return;
        }

        var workerCount = Math.Clamp(_options.WorkerCount, 1, 16);

        var workers = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(() => WorkerLoopAsync(stoppingToken), stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            // خاموشی طبیعی.
        }
        catch (Exception ex)
        {
            ProcessorFatal(_logger, ex);
        }
    }

    private async Task WorkerLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _queue.DequeueAsync(stoppingToken);

                using var scope = _serviceProvider.CreateScope();

                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // یک کار نباید worker را از کار بیندازد.
                JobFailed(_logger, ex);
            }
        }
    }
}
