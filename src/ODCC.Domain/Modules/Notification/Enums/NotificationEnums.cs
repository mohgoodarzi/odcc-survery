namespace ODCC.Domain.Modules.Notification.Enums;

/// <summary>
/// کانال تحویل اعلان. کانال تعیین می‌کند که پیام چگونه به گیرنده می‌رسد
/// و کدام ارائه‌دهنده‌ی تحویل (<c>INotificationDeliveryProvider</c>) آن را پردازش می‌کند.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// درون‌برنامه‌ای: پیام در صندوق ورودی خود کاربر نوشته می‌شود. تحویل همان
    /// نوشته‌شدن ردیف است؛ این کانال هیچ ارائه‌دهنده‌ی بیرونی نیاز ندارد.
    /// </summary>
    InApp = 1,

    /// <summary>ایمیل سازمانی (یا شخصی) از طریق <c>IEmailSender</c>.</summary>
    Email = 2,

    /// <summary>پیامک از طریق <c>ISmsSender</c>.</summary>
    Sms = 3
}

/// <summary>
/// وضعیت چرخه‌ی عمر یک اعلان. این یک ماشین وضعیت سمت سرور است که توسط
/// <c>INotificationDispatcher</c> حرکت می‌کند.
/// </summary>
public enum NotificationStatus
{
    /// <summary>در صف تحویل؛ هنوز تلاشی انجام نشده یا تلاش قبلی برای امتحان مجدد برنامه‌ریزی شده است.</summary>
    Pending = 1,

    /// <summary>ارائه‌دهنده‌ی تحویل پیام را پذیرفت، ولی تأیید تحویل بیرونی نداشتیم (ایمیل/پیامک).</summary>
    Sent = 2,

    /// <summary>تحویل تأییدشده. برای کانال درون‌برنامه‌ای معادل نوشته‌شدن در صندوق ورودی است.</summary>
    Delivered = 3,

    /// <summary>تمام تلاش‌ها شکست خورد یا ارائه‌دهنده شکست دائمی گزارش کرد.</summary>
    Failed = 4,

    /// <summary>به‌دلیل ترجیحات کاربر (انصراف از این کانال/دسته) ارسال نشد.</summary>
    Suppressed = 5
}

/// <summary>
/// دسته‌بندی منطقی اعلان. کاربران می‌توانند تحویل را به‌ازای (کانال، دسته) خاموش کنند.
/// </summary>
public enum NotificationCategory
{
    /// <summary>اعلان عمومی/سیستمی.</summary>
    General = 0,

    /// <summary>دعوت‌نامه‌ها و یادآورهای کمپین.</summary>
    Campaign = 1,

    /// <summary>رویدادهای چرخه‌ی عمر نظرسنجی (مثلاً بسته‌شدن نظرسنجی).</summary>
    Survey = 2,

    /// <summary>هشدارهای تحلیلی (مثلاً افت NPS زیر بنچمارک).</summary>
    Analytics = 3,

    /// <summary>آماده‌بودن یا شکست خروجی گزارش.</summary>
    Reporting = 4,

    /// <summary>برنامه‌های اقدام و پیگیری آن‌ها (انتصاب، یادآور، تشدید).</summary>
    Action = 5
}
