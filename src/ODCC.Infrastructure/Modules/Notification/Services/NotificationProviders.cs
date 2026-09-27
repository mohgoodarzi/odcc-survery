using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Infrastructure.Modules.Notification.Services;

/// <summary>
/// ارائه‌دهنده‌ی تحویل ایمیل. این لایه بین منطق تحویل و <see cref="IEmailSender"/>
/// است: به اعلان نگاه نمی‌کند، فقط یک <see cref="EmailMessage"/> می‌سازد و
/// نتیجه‌ی ارائه‌دهنده را برمی‌گرداند.
/// </summary>
public sealed class EmailNotificationProvider(
    IEmailSender emailSender,
    IOptions<NotificationEmailOptions> options) : INotificationDeliveryProvider
{
    private readonly IEmailSender _emailSender = emailSender;
    private readonly NotificationEmailOptions _options = options.Value;

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Email;

    /// <inheritdoc/>
    public async Task<DeliveryAttemptResult> DeliverAsync(NotificationDeliveryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // بدون آدرس ایمیل، تحویل غیرممکن و دائمی است (امتحان مجدد بی‌فایده).
        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return DeliveryAttemptResult.Failed("آدرس ایمیل گیرنده وجود ندارد.", isPermanent: true);
        }

        var message = new EmailMessage
        {
            To = request.RecipientEmail,
            Subject = request.Subject,
            Body = request.Body,
            FromName = string.IsNullOrWhiteSpace(_options.FromName) ? null : _options.FromName,
            FromAddress = string.IsNullOrWhiteSpace(_options.FromAddress) ? null : _options.FromAddress
        };

        try
        {
            var result = await _emailSender.SendAsync(message, ct);

            if (result.Success)
            {
                // ارائه‌دهنده‌های ایمیل معمولاً تأیید تحویل فوری نمی‌دهند؛
                // «پذیرفته‌شده» است، نه «تأییدشده».
                return DeliveryAttemptResult.Accepted(result.MessageId);
            }

            return DeliveryAttemptResult.Failed(
                result.FailureReason ?? "ارسال ایمیل ناموفق بود.",
                isPermanent: result.IsPermanent);
        }
        catch (Exception ex)
        {
            // خطاهای زیرساختی گذرا هستند: امتحان مجدد مفید است.
            return DeliveryAttemptResult.Failed($"خطای غیرمنتظره ارسال ایمیل: {ex.Message}", isPermanent: false);
        }
    }
}

/// <summary>
/// ارائه‌دهنده‌ی تحویل پیامک. مانند ایمیل، فقط <see cref="SmsMessage"/> می‌سازد.
/// </summary>
public sealed class SmsNotificationProvider(
    ISmsSender smsSender,
    IOptions<NotificationSmsOptions> options) : INotificationDeliveryProvider
{
    private readonly ISmsSender _smsSender = smsSender;
    private readonly NotificationSmsOptions _options = options.Value;

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Sms;

    /// <inheritdoc/>
    public async Task<DeliveryAttemptResult> DeliverAsync(NotificationDeliveryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RecipientPhone))
        {
            return DeliveryAttemptResult.Failed("شماره تلفن گیرنده وجود ندارد.", isPermanent: true);
        }

        var body = request.Body;

        // پیامک طول محدودی دارد؛ برش متن به حداکثر مجاز.
        var max = Math.Max(70, _options.MaxBodyLength);

        if (body.Length > max)
        {
            body = string.Concat(body.AsSpan(0, max - 1), "…");
        }

        var message = new SmsMessage { To = request.RecipientPhone!, Body = body };

        try
        {
            var result = await _smsSender.SendAsync(message, ct);

            if (result.Success)
            {
                return DeliveryAttemptResult.Accepted(result.MessageId);
            }

            return DeliveryAttemptResult.Failed(
                result.FailureReason ?? "ارسال پیامک ناموفق بود.",
                isPermanent: result.IsPermanent);
        }
        catch (Exception ex)
        {
            return DeliveryAttemptResult.Failed($"خطای غیرمنتظره ارسال پیامک: {ex.Message}", isPermanent: false);
        }
    }
}

/// <summary>
/// ارائه‌دهنده‌ی تحویل درون‌برنامه‌ای: خود وجود ردیف در پایگاه داده، «تحویل»
/// است. این ارائه‌دهنده فقط تأیید می‌کند که گیرنده کاربر سامانه است.
/// </summary>
public sealed class InAppNotificationProvider : INotificationDeliveryProvider
{
    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.InApp;

    /// <inheritdoc/>
    public Task<DeliveryAttemptResult> DeliverAsync(NotificationDeliveryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RecipientUserId is null)
        {
            return Task.FromResult(DeliveryAttemptResult.Failed("اعلان درون‌برنامه‌ای نیازمند شناسه‌ی کاربر است.", isPermanent: true));
        }

        // ردیف از قبل نوشته شده است؛ تحویل تأییدشده.
        return Task.FromResult(DeliveryAttemptResult.Confirmed());
    }
}

/// <summary>
/// گزینه‌های ایمیل ماژول اعلان‌ها.
/// </summary>
public sealed class NotificationEmailOptions
{
    public const string SectionName = "Notifications:Email";

    /// <summary>نام نمایشی فرستنده.</summary>
    public string FromName { get; set; } = "سامانه نظرسنجی سازمانی";

    /// <summary>آدرس فرستنده. خالی یعنی پیش‌فرض ارائه‌دهنده.</summary>
    public string FromAddress { get; set; } = string.Empty;
}

/// <summary>گزینه‌های پیامک ماژول اعلان‌ها.</summary>
public sealed class NotificationSmsOptions
{
    public const string SectionName = "Notifications:Sms";

    /// <summary>بیشترین طول مجاز بدنه‌ی پیامک پیش از برش.</summary>
    public int MaxBodyLength { get; set; } = 480;
}

/// <summary>
/// گزینه‌های کلی تحویل اعلان.
/// </summary>
public sealed class NotificationDeliveryOptions
{
    public const string SectionName = "Notifications:Delivery";

    /// <summary>حداکثر تلاش مجدد پیش از علامت‌گذاری به‌عنوان Failed.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>حداکثر تعداد اعلان پردازش‌شده در هر دوره‌ی زمان‌بند.</summary>
    public int MaxPerCycle { get; set; } = 100;
}

/// <summary>
/// پیاده‌سازی پیش‌فرض <see cref="IEmailSender"/>: هیچ ایمیلی واقعاً ارسال نمی‌کند.
/// نتیجه را موفق ثبت و یک هشدار لاگ می‌کند. این پیاده‌سازی عمداً ساده است تا
/// سامانه بدون پیکربندی SMTP کار کند؛ برای ارسال واقعی، ارائه‌دهنده‌ی دلخواه
/// (SMTP، MailKit، AWS SES و غیره) کافی است این قرارداد را پیاده و در DI ثبت کند.
/// </summary>
public sealed class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    private readonly ILogger<NoOpEmailSender> _logger = logger;

    private static readonly Action<ILogger, string, string, string?, Exception?> EmailSimulated = LoggerMessage.Define<string, string, string?>(
        LogLevel.Warning,
        new EventId(1, "EmailNotActuallySent"),
        "ایمیل شبیه‌سازی شد (ارائه‌دهنده‌ای پیکربندی نشده است). گیرنده: {Recipient}، موضوع: {Subject}، شناسه: {MessageId}. برای ارسال واقعی، IEmailSender را با یک ارائه‌دهنده‌ی SMTP جایگزین کنید.");

    /// <inheritdoc/>
    public Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageId = $"no-op-{Guid.NewGuid():N}";

        EmailSimulated(_logger, message.To, message.Subject, messageId, null);

        // موفقیت ثبت می‌شود تا جریان کار طبیعی ادامه یابد و بتوان نتیجه را دید.
        return Task.FromResult(EmailDeliveryResult.Succeeded(messageId));
    }
}

/// <summary>
/// پیاده‌سازی پیش‌فرض <see cref="ISmsSender"/>: مانند <see cref="NoOpEmailSender"/>
/// پیامک را شبیه‌سازی می‌کند و موفقیت برمی‌گرداند.
/// </summary>
public sealed class NoOpSmsSender(ILogger<NoOpSmsSender> logger) : ISmsSender
{
    private readonly ILogger<NoOpSmsSender> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> SmsSimulated = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "SmsNotActuallySent"),
        "پیامک شبیه‌سازی شد (ارائه‌دهنده‌ای پیکربندی نشده است). گیرنده: {Recipient}. برای ارسال واقعی، ISmsSender را با یک ارائه‌دهنده جایگزین کنید.");

    /// <inheritdoc/>
    public Task<SmsDeliveryResult> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageId = $"no-op-{Guid.NewGuid():N}";

        if (!string.IsNullOrWhiteSpace(message.To))
        {
            SmsSimulated(_logger, message.To, null);
        }

        return Task.FromResult(SmsDeliveryResult.Succeeded(messageId));
    }
}
