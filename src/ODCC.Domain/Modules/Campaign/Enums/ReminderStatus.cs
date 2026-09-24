namespace ODCC.Domain.Modules.Campaign.Enums;

/// <summary>
/// وضعیت یک یادآور کمپین.
/// </summary>
public enum ReminderStatus
{
    /// <summary>زمان‌بندی‌شده — در زمان مقرر ارسال خواهد شد.</summary>
    Scheduled = 1,

    /// <summary>ارسال شده.</summary>
    Sent = 2,

    /// <summary>لغو شده.</summary>
    Cancelled = 3
}
