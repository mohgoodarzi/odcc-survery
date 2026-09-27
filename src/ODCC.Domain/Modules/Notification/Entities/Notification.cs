using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Domain.Modules.Notification.Entities;

/// <summary>
/// یک نمونه‌ی اعلان برای یک گیرنده در یک کانال خاص.
///
/// این موجودیت رکورد کامل تحویل است: محتوای رندرشده (قالب + جایگزینی متغیرها)،
/// آدرس گیرنده، وضعیت چرخه‌ی عمر، شمارنده‌ی امتحان مجدد و پیوند به منبع
/// (برای deep-link در رابط کاربری) را نگه می‌دارد.
///
/// <b>حریم خصوصی:</b> این موجودیت هرگز داده‌ی پاسخ‌گوییِ پاسخ‌های ثبت‌شده را
/// نگه نمی‌دارد. دعوت‌نامه‌ها به مخاطب شناخته‌شده‌ی کمپین ارسال می‌شوند
/// (کمپین مخاطب خود را می‌شناسد)، ولی محتوای اعلان فقط شامل اطلاعات تجمیعی
/// یا عمومی است — هرگز پاسخ یا شناسه‌ی پاسخ‌گوی یک نظرسنجی ناشناس.
/// </summary>
public sealed class Notification : BaseEntity
{
    /// <summary>کاربر گیرنده. برای اعلان‌های درون‌برنامه‌ای الزامی است.</summary>
    public Guid? RecipientUserId { get; set; }

    /// <summary>نام نمایشی گیرنده در زمان ارسال (snapshot برای تاریخچه).</summary>
    public string? RecipientName { get; set; } = string.Empty;

    /// <summary>آدرس ایمیل حل‌شده (فقط برای کانال ایمیل).</summary>
    public string? RecipientEmail { get; set; }

    /// <summary>شماره تلفن حل‌شده (فقط برای کانال پیامک).</summary>
    public string? RecipientPhone { get; set; }

    /// <summary>کانال تحویل.</summary>
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    /// <summary>دسته‌بندی منطقی.</summary>
    public NotificationCategory Category { get; set; } = NotificationCategory.General;

    /// <summary>وضعیت چرخه‌ی عمر.</summary>
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    /// <summary>کد قالبی که محتوا از آن رندر شده است.</summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>موضوع رندرشده.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>بدنه‌ی رندرشده.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>زبان محتوای رندرشده.</summary>
    public Language Language { get; set; } = Language.Fa;

    // --- زمان‌بندی و امتحان مجدد -------------------------------------------

    /// <summary>تعداد تلاش‌های انجام‌شده تا کنون.</summary>
    public int RetryCount { get; set; }

    /// <summary>حداکثر تلاش مجاز پیش از علامت‌گذاری به‌عنوان Failed.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>زمان مناسب برای تلاش بعدی (UTC). null یعنی همین حالا.</summary>
    public DateTime? NextTryAt { get; set; }

    /// <summary>زمان پذیرش توسط ارائه‌دهنده (UTC).</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>زمان تأیید تحویل (UTC).</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>زمان خوانده‌شدن توسط گیرنده (UTC) — فقط کانال درون‌برنامه‌ای.</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>پیام خطای آخرین تلاش (در صورت شکست).</summary>
    public string? LastError { get; set; }

    /// <summary>شناسه‌ی پیام نزد ارائه‌دهنده (در صورت وجود).</summary>
    public string? ProviderMessageId { get; set; }

    // --- پیوند به منبع ------------------------------------------------------

    /// <summary>نوع موجودیت مبدأ (مثلاً «distribution»، «report_execution»).</summary>
    public string? SourceType { get; set; }

    /// <summary>شناسه‌ی موجودیت مبدأ.</summary>
    public Guid? SourceId { get; set; }

    /// <summary>مسیر deep-link در رابط کاربری (نسبی به فرهنگ).</summary>
    public string? Url { get; set; }

    // --- چرخه‌ی عمر -----------------------------------------------------------

    /// <summary>تحویل تأیید شد (برای درون‌برنامه‌ای: نوشته‌شدن در صندوق ورودی).</summary>
    public void MarkDelivered(string? providerMessageId = null)
    {
        Status = NotificationStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        ProviderMessageId = providerMessageId;
        LastError = null;
    }

    /// <summary>ارائه‌دهنده پذیرفت ولی تأیید تحویل خارجی وجود ندارد (ایمیل/پیامک).</summary>
    public void MarkSent(string? providerMessageId = null)
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        ProviderMessageId = providerMessageId;
        LastError = null;
    }

    /// <summary>شکست دائمی: تمام تلاش‌ها تمام شدند یا ارائه‌دهنده دائمی گزارش کرد.</summary>
    public void MarkFailed(string reason)
    {
        Status = NotificationStatus.Failed;
        LastError = string.IsNullOrWhiteSpace(reason) ? "خطای نامشخص ارسال" : reason;
    }

    /// <summary>شکست موقت: تلاش بعدی را با تأخیر نمایی برنامه‌ریزی می‌کند.</summary>
    public void ScheduleRetry(string reason)
    {
        RetryCount++;
        LastError = string.IsNullOrWhiteSpace(reason) ? "خطای نامشخص ارسال" : reason;

        // تأخیر نمایی: ۲^retryCount دقیقه (حداکثر ۳۰ دقیقه).
        var delayMinutes = Math.Min(Math.Pow(2, RetryCount), 30);
        NextTryAt = DateTime.UtcNow.AddMinutes(delayMinutes);

        // تا زمان رسیدن زمان تلاش بعدی، در صف می‌ماند.
        Status = NotificationStatus.Pending;
    }

    /// <summary>آیا تلاش مجدد دیگری مجاز است؟</summary>
    public bool CanRetry => RetryCount < MaxRetries;

    /// <summary>به‌دلیل ترجیحات کاربر ارسال نشد.</summary>
    public void MarkSuppressed()
    {
        Status = NotificationStatus.Suppressed;
        LastError = null;
    }

    /// <summary>گیرنده اعلان درون‌برنامه‌ای را خواند.</summary>
    public void MarkRead()
    {
        ReadAt = DateTime.UtcNow;
    }

    /// <summary>آیا این اعلان درون‌برنامه‌ای هنوز خوانده نشده است؟</summary>
    public bool IsUnread => Channel == NotificationChannel.InApp
        && Status is NotificationStatus.Delivered or NotificationStatus.Sent
        && ReadAt is null;
}
