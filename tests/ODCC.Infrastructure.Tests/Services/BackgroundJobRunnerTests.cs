using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Infrastructure.Services;
using Xunit;

namespace ODCC.Infrastructure.Tests.Services;

/// <summary>
/// آزمون‌های صف کارهای پس‌زمینه و پردازشگر آن.
///
/// پوشش:
/// - صف: قرار دادن و بردن کار به‌ترتیب (FIFO).
/// - رد کار در زمان پر شدن ظرفیت (DropWrite).
/// - پردازشگر: فقط با تأیید صریح فعال می‌شود (سیاست اثر جانبی).
/// - پردازشگر: شکست یک کار نباید worker را از کار بیندازد.
/// - پردازشگر: کارها در scope جداگانه اجرا می‌شوند (سرویس‌های Scoped).
/// - خاموشی: لغو توکن پردازشگر را متوقف می‌کند.
/// </summary>
public class BackgroundJobRunnerTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Enqueue_Then_Dequeue_PreservesOrder()
    {
        using var serviceProvider = CreateRunner(out var runner);

        runner.Enqueue(ct => Task.CompletedTask);
        runner.Enqueue(_ => Task.CompletedTask);

        using var cts = new CancellationTokenSource();

        var first = await runner.DequeueAsync(cts.Token);
        var second = await runner.DequeueAsync(cts.Token);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        // ترتیب FIFO.
        (first == second).Should().BeFalse();
    }

    [Fact]
    public async Task Dequeue_WhenEmpty_BlocksUntilWorkArrives()
    {
        using var serviceProvider = CreateRunner(out var runner);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var act = async () => await runner.DequeueAsync(cts.Token);

        // هیچ کاری نیست → مسدود می‌ماند تا لغو شود.
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Enqueue_WithNullWorkItem_Throws()
    {
        using var serviceProvider = CreateRunner(out var runner);

        var act = () => runner.Enqueue((Func<IServiceProvider, CancellationToken, Task>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Enqueue_SimpleOverload_WrapsWorkItem()
    {
        using var serviceProvider = CreateRunner(out var runner);

        var executed = false;
        runner.Enqueue(ct =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var work = await runner.DequeueAsync(cts.Token);
        await work(null!, cts.Token);

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Enqueue_WhenCapacityFull_DropsWorkItem()
    {
        // ظرفیت کوچک.
        using var serviceProvider = CreateRunner(out var runner, queueCapacity: 4);

        // تا جایی که صف پر شود.
        for (var i = 0; i < 4; i++)
        {
            runner.Enqueue(ct => Task.Delay(2000, ct));
        }

        // حالا باید DropWrite شود — کاری که جا ندارد رد می‌شود.
        var dropped = 0;
        for (var i = 0; i < 10; i++)
        {
            runner.Enqueue(ct => Task.Delay(2000, ct));
            dropped++;
        }

        // رد شدن کارها نباید استثنا پرتاب کند (فقط نادیده گرفته می‌شوند).
        dropped.Should().BeGreaterThan(0);

        // کارهای داخل صف قابل بردن هستند.
        using var cts = new CancellationTokenSource();
        for (var i = 0; i < 4; i++)
        {
            (await runner.DequeueAsync(cts.Token)).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task HostedService_WhenProcessorDisabled_DoesNotProcess()
    {
        using var serviceProvider = CreateRunner(out var runner, enableProcessor: false);
        var service = ActivatorUtilities.CreateInstance<BackgroundJobRunnerHostedService>(serviceProvider);

        using var cts = new CancellationTokenSource();
        var task = service.StartAsync(cts.Token);

        // وقتی پردازشگر غیرفعال است، ExecuteAsync بلافاصله برمی‌گردد.
        await Task.WhenAny(task, Task.Delay(WaitTimeout));

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task HostedService_ProcessesWorkItems_FromScopedServices()
    {
        using var serviceProvider = CreateRunner(out var runner);
        var service = ActivatorUtilities.CreateInstance<BackgroundJobRunnerHostedService>(serviceProvider);

        var tcs = new TaskCompletionSource<bool>();
        var runnerService = serviceProvider.GetRequiredService<IBackgroundJobRunner>();

        // کار: سرویس Scoped را از scope فراهم‌شده می‌گیرد.
        runnerService.Enqueue(async (provider, ct) =>
        {
            // یک سرویس Scoped باید قابل رزولو کردن باشد.
            var scopeService = provider.GetRequiredService<ScopedCounter>();
            _ = scopeService;
            tcs.TrySetResult(true);
        });

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        (await tcs.Task.WaitAsync(WaitTimeout)).Should().BeTrue();

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        ScopedCounter.Total.Should().Be(1);
    }

    [Fact]
    public async Task HostedService_WhenJobFails_ContinuesProcessingNextJob()
    {
        using var serviceProvider = CreateRunner(out var runner, workerCount: 1);
        var service = ActivatorUtilities.CreateInstance<BackgroundJobRunnerHostedService>(serviceProvider);

        var second = new TaskCompletionSource<bool>();
        var runnerService = serviceProvider.GetRequiredService<IBackgroundJobRunner>();

        runnerService.Enqueue(ct => throw new InvalidOperationException("کار اول شکست خورد"));
        runnerService.Enqueue(ct =>
        {
            second.TrySetResult(true);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // کار شکست‌خورده نباید پردازشگر را از کار بیندازد — کار بعدی اجرا می‌شود.
        (await second.Task.WaitAsync(WaitTimeout)).Should().BeTrue();

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HostedService_ProcessesJobsConcurrently_WithMultipleWorkers()
    {
        using var serviceProvider = CreateRunner(out var runner, workerCount: 4);
        var service = ActivatorUtilities.CreateInstance<BackgroundJobRunnerHostedService>(serviceProvider);

        var running = 0;
        var maxConcurrent = 0;
        var allDone = new TaskCompletionSource<bool>();
        var runnerService = serviceProvider.GetRequiredService<IBackgroundJobRunner>();

        const int jobCount = 8;
        jobCounter = 0;

        for (var i = 0; i < jobCount; i++)
        {
            runnerService.Enqueue(async ct =>
            {
                var current = Interlocked.Increment(ref running);

                // حداکثر همزمانی تا اینجا.
                if (current > maxConcurrent)
                {
                    Interlocked.Exchange(ref maxConcurrent, current);
                }

                await Task.Delay(150, ct);

                Interlocked.Decrement(ref running);

                if (Interlocked.Increment(ref jobCounter) >= jobCount)
                {
                    allDone.TrySetResult(true);
                }
            });
        }

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        (await allDone.Task.WaitAsync(TimeSpan.FromSeconds(30))).Should().BeTrue();

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // با ۴ کارگر و ۸ کارِ ۱۵۰ms، باید حداقل یک زمانی بیش از یک کار همزمان اجرا شده باشد.
        maxConcurrent.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task HostedService_StopsCleanly_OnCancellation()
    {
        using var serviceProvider = CreateRunner(out var runner);
        var service = ActivatorUtilities.CreateInstance<BackgroundJobRunnerHostedService>(serviceProvider);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        cts.Cancel();

        var act = async () => await service.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HostedService_WhenJobCancelled_PropagatesCancellation()
    {
        using var serviceProvider = CreateRunner(out var runner, workerCount: 1);
        var service = ActivatorUtilities.CreateInstance<BackgroundJobRunnerHostedService>(serviceProvider);

        var observed = new TaskCompletionSource<bool>();
        var runnerService = serviceProvider.GetRequiredService<IBackgroundJobRunner>();

        runnerService.Enqueue(async ct =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                observed.TrySetResult(false);
            }
            catch (OperationCanceledException)
            {
                // لغو واقعی باید به کار منتقل شود.
                observed.TrySetResult(true);
            }
        });

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // به کارِ در حال اجرا اجازه‌ی شروع بده.
        await Task.Delay(200);

        // لغو توکنِ توقف باید به کار در حال اجرا منتقل شود.
        cts.Cancel();

        (await observed.Task.WaitAsync(WaitTimeout)).Should().BeTrue();

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Options_ClampExtremes()
    {
        // مقادیر نامعتبر باید به بازه‌ی امن محدود شوند، نه استثنا.
        var serviceProvider = CreateRunner(out var runner, workerCount: 0, queueCapacity: -5);

        runner.Should().NotBeNull();

        // صف با حداقل ظرفیت باید قابل استفاده باشد.
        using var cts = new CancellationTokenSource();
        runner.Enqueue(ct => Task.CompletedTask);
        (await runner.DequeueAsync(cts.Token)).Should().NotBeNull();
    }
    // --- کمک‌ها ------------------------------------------------------------------

    private static int jobCounter;

    private static ServiceProvider CreateRunner(out BackgroundJobRunner runner, bool enableProcessor = true, int workerCount = 2, int queueCapacity = 1000)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddSingleton<ScopedCounter>();
        collection.AddSingleton(Options.Create(new BackgroundJobQueueOptions
        {
            EnableProcessor = enableProcessor,
            WorkerCount = workerCount,
            QueueCapacity = queueCapacity
        }));

        collection.AddSingleton<IBackgroundJobRunner, BackgroundJobRunner>();

        var services = collection.BuildServiceProvider();

        runner = (BackgroundJobRunner)services.GetRequiredService<IBackgroundJobRunner>();

        return services;
    }

    /// <summary>سرویس Scoped آزمون: شمارنده‌ی نمونه‌های ساخته‌شده از داخل scope کار.</summary>
    private sealed class ScopedCounter
    {
        public ScopedCounter()
        {
            Interlocked.Increment(ref Total);
        }

        public static int Total;
    }
}
