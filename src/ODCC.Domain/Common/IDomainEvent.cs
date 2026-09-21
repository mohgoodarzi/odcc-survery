namespace ODCC.Domain.Common;

/// <summary>
/// نشانگر رویدادهای دامنه.
/// رویدادهای دامنه تنها واقعیتی هستند که ماژول‌ها می‌توانند از طریق آن بدون ارجاع مستقیم
/// به یکدیگر ارتباط برقرار کنند (انتشار از طریق <c>IDomainEventDispatcher</c>).
/// </summary>
public interface IDomainEvent
{
    /// <summary>زمان وقوع رویداد بر حسب UTC.</summary>
    DateTime OccurredAt { get; }
}

/// <summary>
/// کلاس پایه‌ی رویدادهای دامنه.
/// </summary>
public abstract class DomainEvent : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
