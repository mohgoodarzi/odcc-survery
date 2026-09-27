using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Application.Modules.Notification.Abstractions;

/// <summary>
/// قرارداد ارسال ایمیل — نقطه‌ی اتصال ارائه‌دهنده‌های بیرونی.
///
/// این قرارداد عمداً فقط «پیام را بپذیر و نتیجه را برگردان» است؛ هیچ دانشی از
/// قالب‌ها، ترجیحات کاربر یا امتحان مجدد ندارد. پیاده‌سازی‌های ممکن:
/// <list type="bullet">
///   <item><c>NoOpEmailSender</c> (پیش‌فرض): هیچ ایمیلی واقعاً ارسال نمی‌کند،
///   موفقیت ثبت می‌شود و یک هشدار لاگ می‌شود. برای توسعه و محیط‌های بدون SMTP.</item>
///   <item>یک ارائه‌دهنده‌ی واقعی (SMTP/MailKit، AWS SES، Postmark و غیره):
///   کافی است این قرارداد را پیاده کند و در DI ثبت شود — هیچ کد دیگری تغییر
///   نمی‌کند. این یعنی ماژول اعلان‌ها <b>آماده‌ی ارائه‌دهنده</b> است.</item>
/// </list>
/// </summary>
public interface IEmailSender
{
    /// <summary>ارسال یک پیام ایمیل.</summary>
    Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>یک پیام ایمیل مستقل از ارائه‌دهنده.</summary>
public sealed record EmailMessage
{
    /// <summary>آدرس گیرنده.</summary>
    public required string To { get; init; }

    /// <summary>موضوع.</summary>
    public required string Subject { get; init; }

    /// <summary>بدنه (متن ساده؛ HTML در صورت پشتیبانی ارائه‌دهنده).</summary>
    public required string Body { get; init; }

    /// <summary>نام فرستنده (اختیاری؛ از پیکربندی پیش‌فرض می‌آید).</summary>
    public string? FromName { get; init; }

    /// <summary>آدرس فرستنده (اختیاری؛ از پیکربندی پیش‌فرض می‌آید).</summary>
    public string? FromAddress { get; init; }
}

/// <summary>نتیجه‌ی تلاش ارسال ایمیل.</summary>
public sealed record EmailDeliveryResult
{
    /// <summary>آیا ارائه‌دهنده پیام را پذیرفت؟</summary>
    public required bool Success { get; init; }

    /// <summary>شناسه‌ی پیام نزد ارائه‌دهنده (در صورت وجود).</summary>
    public string? MessageId { get; init; }

    /// <summary>دلیل شکست (در صورت شکست).</summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// آیا شکست دائمی است (مثلاً آدرس نامعتبر) و نباید امتحان مجدد شد؟
    /// <c>false</c> یعنی شکست گذرا و قابل‌امتحان مجدد.
    /// </summary>
    public bool IsPermanent { get; init; }

    public static EmailDeliveryResult Succeeded(string? messageId = null) =>
        new() { Success = true, MessageId = messageId };

    public static EmailDeliveryResult Failed(string reason, bool isPermanent = false) =>
        new() { Success = false, FailureReason = reason, IsPermanent = isPermanent };
}
