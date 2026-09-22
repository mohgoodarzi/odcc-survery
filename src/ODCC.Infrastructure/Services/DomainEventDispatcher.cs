using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Services;

/// <summary>
/// پیاده‌سازی درون‌فرآیندی (In-process) <see cref="IDomainEventDispatcher"/>.
///
/// رویدادها را به تمام <see cref="IDomainEventListener{TEvent}"/>های ثبت‌شده در
/// همان کانتینر تزریق وابستگی تحویل می‌دهد. این تنها مسیر مجاز ارتباط بین
/// ماژول‌هاست: ماژولی که رویداد را منتشر می‌کند از وجود شنونده‌ها بی‌خبر است و
/// وابستگی چرخه‌ای ایجاد نمی‌شود.
///
/// <b>طراحی:</b>
/// <list type="bullet">
///   <item>شنونده‌ها به‌صورت <c>Keyed</c> با کلید نوع رویداد ثبت می‌شوند تا
///   نیازی به اسکن اسمبلی در زمان شروع نباشد.</item>
///   <item>تحویل به شنونده‌های یک رویداد به‌صورت موازی و مستقل انجام می‌شود؛
///   خطای یک شنونده مانع تحویل به بقیه نمی‌شود، اما خطا لاگ می‌شود.</item>
/// </list>
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> DispatchFailed = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(2, "DomainEventDispatchFailed"),
        "خطا در تحویل رویداد دامنه‌ی {EventType} به یک شنونده.");

    /// <summary>
    /// تحویل رویدادها به تمام شنونده‌های ثبت‌شده برای این نوع رویداد.
    /// </summary>
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var eventType = domainEvent.GetType();
        var listenerType = typeof(IDomainEventListener<>).MakeGenericType(eventType);

        // تمام شنونده‌های این نوع رویداد را از کانتینر می‌گیریم.
        var listeners = _serviceProvider.GetServices(listenerType).ToList();

        foreach (var listener in listeners)
        {
            try
            {
                var handler = listenerType.GetMethod(nameof(IDomainEventListener<IDomainEvent>.HandleAsync));
                if (handler?.Invoke(listener, [domainEvent, ct]) is Task task)
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // یک شنونده‌ی خطادار نباید کل زنجیره را بشکند؛ خطا لاگ می‌شود
                // و تحویل به سایر شنونده‌ها ادامه می‌یابد.
                DispatchFailed(_logger, eventType.Name, ex);
            }
        }
    }
}
