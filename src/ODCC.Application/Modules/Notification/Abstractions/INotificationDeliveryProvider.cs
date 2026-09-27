using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Application.Modules.Notification.Abstractions;

/// <summary>
/// قرارداد یک کانال تحویل: برای هر <see cref="NotificationChannel"/> یک پیاده‌سازی
/// وجود دارد که پیام رندرشده را به مقصد می‌رساند.
///
/// این قرارداد لایه‌ی واسط بین «منطق تحویل» (dispatcher: ترجیحات، امتحان مجدد،
/// وضعیت) و «انتقال واقعی» (ایمیل/پیامک/درون‌برنامه‌ای) است. افزودن کانال جدید
/// (مثلاً push یا تلگرام) فقط پیاده‌سازی این قرارداد و ثبت آن در DI است.
/// </summary>
public interface INotificationDeliveryProvider
{
    /// <summary>کانالی که این ارائه‌دهنده متولد آن است.</summary>
    NotificationChannel Channel { get; }

    /// <summary>تحویل یک اعلان آماده‌ی ارسال.</summary>
    Task<DeliveryAttemptResult> DeliverAsync(NotificationDeliveryRequest request, CancellationToken ct = default);
}

/// <summary>درخواست تحویل یک اعلان: فقط داده‌ای که ارائه‌دهنده نیاز دارد.</summary>
public sealed record NotificationDeliveryRequest
{
    public required Guid NotificationId { get; init; }
    public Guid? RecipientUserId { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientEmail { get; init; }
    public string? RecipientPhone { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
}

/// <summary>نتیجه‌ی یک تلاش تحویل.</summary>
public sealed record DeliveryAttemptResult
{
    /// <summary>آیا تحویل موفق بود؟</summary>
    public required bool Success { get; init; }

    /// <summary>آیا تحویل تأیید شد (نه فقط پذیرفته‌شد)؟ برای درون‌برنامه‌ای <c>true</c>.</summary>
    public bool IsConfirmed { get; init; }

    /// <summary>شناسه‌ی پیام نزد ارائه‌دهنده (در صورت وجود).</summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>دلیل شکست (در صورت شکست).</summary>
    public string? FailureReason { get; init; }

    /// <summary>آیا شکست دائمی است و نباید امتحان مجدد شد؟</summary>
    public bool IsPermanentFailure { get; init; }

    public static DeliveryAttemptResult Confirmed(string? providerMessageId = null) =>
        new() { Success = true, IsConfirmed = true, ProviderMessageId = providerMessageId };

    public static DeliveryAttemptResult Accepted(string? providerMessageId = null) =>
        new() { Success = true, IsConfirmed = false, ProviderMessageId = providerMessageId };

    public static DeliveryAttemptResult Failed(string reason, bool isPermanent = false) =>
        new() { Success = false, FailureReason = reason, IsPermanentFailure = isPermanent };
}
