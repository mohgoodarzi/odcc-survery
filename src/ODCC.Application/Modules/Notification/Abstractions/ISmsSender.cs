namespace ODCC.Application.Modules.Notification.Abstractions;

/// <summary>
/// قرارداد ارسال پیامک — نقطه‌ی اتصال ارائه‌دهنده‌های بیرونی (Kavenegar،
/// Twilio، فراز و غیره). مانند <see cref="IEmailSender"/>، پیاده‌سازی پیش‌فرض
/// <c>NoOpSmsSender</c> است و جایگزینی آن فقط ثبت یک سرویس در DI است.
/// </summary>
public interface ISmsSender
{
    /// <summary>ارسال یک پیامک.</summary>
    Task<SmsDeliveryResult> SendAsync(SmsMessage message, CancellationToken ct = default);
}

/// <summary>یک پیام متنی مستقل از ارائه‌دهنده.</summary>
public sealed record SmsMessage
{
    /// <summary>شماره گیرنده.</summary>
    public required string To { get; init; }

    /// <summary>متن پیام.</summary>
    public required string Body { get; init; }
}

/// <summary>نتیجه‌ی تلاش ارسال پیامک.</summary>
public sealed record SmsDeliveryResult
{
    public required bool Success { get; init; }

    public string? MessageId { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>آیا شکست دائمی است و نباید امتحان مجدد شد؟</summary>
    public bool IsPermanent { get; init; }

    public static SmsDeliveryResult Succeeded(string? messageId = null) =>
        new() { Success = true, MessageId = messageId };

    public static SmsDeliveryResult Failed(string reason, bool isPermanent = false) =>
        new() { Success = false, FailureReason = reason, IsPermanent = isPermanent };
}
