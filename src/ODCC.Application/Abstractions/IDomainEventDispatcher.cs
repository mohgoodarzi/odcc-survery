using ODCC.Domain.Common;

namespace ODCC.Application.Abstractions;

/// <summary>
/// قرارداد انتشار رویدادهای دامنه.
/// تنها مسیر مجاز برای ارتباط بین ماژول‌ها: یک ماژول رویدادی را منتشر می‌کند و
/// ماژول‌های دیگر بدون اینکه اولی از وجود آن‌ها بداند، به آن گوش می‌دهند.
/// این مکانیزم از وابستگی چرخه‌ای میان ماژول‌ها جلوگیری می‌کند.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>رویداد را به تمام پردازنده‌های ثبت‌شده در همین فرآیند (In-process) تحویل می‌دهد.</summary>
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

/// <summary>پردازنده‌ی یک نوع رویداد دامنه.</summary>
public interface IDomainEventListener<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
