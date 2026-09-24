namespace ODCC.Domain.Modules.Campaign.Enums;

/// <summary>
/// وضعیت توزیع یک دعوت‌نامه برای یک گیرنده‌ی خاص.
/// </summary>
public enum DistributionStatus
{
    /// <summary>در صف ارسال (هنوز ارسال نشده).</summary>
    Pending = 1,

    /// <summary>ارسال شده.</summary>
    Sent = 2,

    /// <summary>ارسال ناموفق (خطای کانال).</summary>
    Failed = 3,

    /// <summary>گیرنده پاسخ داده است (توسط ماژول پاسخ‌ها تنظیم می‌شود).</summary>
    Responded = 4
}
