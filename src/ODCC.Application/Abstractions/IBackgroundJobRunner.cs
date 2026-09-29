namespace ODCC.Application.Abstractions;

/// <summary>
/// صف درون‌برنامه‌ای پردازش پس‌زمینه: راهی ساده و ایمن برای اجرای کارها
/// بدون مسدودکردن درخواست جاری.
///
/// <b>طراحی:</b> یک صف سبک درون‌حافظه‌ای (Queue + BackgroundService) به‌جای
/// یک جدول دیتابیس. برای حجم متوسط کافی است؛ برای توزیع‌شده باید با یک
/// جدول دیتابیس یا پیام‌رسان جایگزین شود (نقطه‌ی توسعه‌ی آینده).
///
/// <b>خطا:</b> اگر کار شکست بخورد، خطا لاگ می‌شود و کار دور ریخته می‌شود —
/// برخلاف زمان‌بندهای ماژولی که تلاش مجدد خود را دارند. کارهای بحرانی باید
/// زمان‌بند اختصاصی داشته باشند.
/// </summary>
public interface IBackgroundJobRunner
{
    /// <summary>قرار دادن یک کار در صف.</summary>
    void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem);

    /// <summary>قرار دادن یک کار در صف (بدون پارامتر اضافه).</summary>
    void Enqueue(Func<CancellationToken, Task> workItem);

    /// <summary>بردن کار بعدی از صف (تا زمانی که کار بیاید مسدود می‌شود).</summary>
    Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>تنظیمات صف کارهای پس‌زمینه.</summary>
public sealed class BackgroundJobQueueOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// فعال‌سازی پردازش‌گر پس‌زمینه. یک اثر جانبی است و باید صریحاً فعال شود
    /// (سیاست پروژه). صف همیشه در دسترس است ولی پردازش با این پرچم است.
    /// </summary>
    public bool EnableProcessor { get; set; }

    /// <summary>تعداد پردازشگرهای موازی.</summary>
    public int WorkerCount { get; set; } = 2;

    /// <summary>حداکثر تعداد کار در صف (بیشتر از این رد می‌شود).</summary>
    public int QueueCapacity { get; set; } = 1000;
}
