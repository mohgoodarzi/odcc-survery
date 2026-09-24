namespace ODCC.Domain.Modules.Survey.Enums;

/// <summary>
/// وضعیت چرخه‌ی عمر یک نظرسنجی.
///
/// ماشین وضعیت:
/// <code>
/// Draft ──Publish──▶ Scheduled (تاریخ شروع در آینده) یا Active (بدون تأخیر)
/// Scheduled ──Start──▶ Active
/// Active ⇄ Paused (Pause / Resume)
/// Scheduled | Active | Paused ──Close──▶ Closed
/// هر حالت ──Archive──▶ Archived
/// </code>
/// فقط نظرسنجی «فعال» می‌تواند پاسخ دریافت کند. پاسخ‌های نظرسنجی‌های بسته‌شده
/// حفظ می‌شوند و فقط پیش‌نویس قابل حذف است.
/// </summary>
public enum SurveyStatus
{
    /// <summary>پیش‌نویس — در حال تنظیم، هنوز منتشر نشده.</summary>
    Draft = 1,

    /// <summary>زمان‌بندی‌شده — منتشر شده ولی پنجره‌ی پاسخ‌گویی هنوز باز نشده.</summary>
    Scheduled = 2,

    /// <summary>فعال — پنجره‌ی پاسخ‌گویی باز است و پاسخ دریافت می‌کند.</summary>
    Active = 3,

    /// <summary>متوقف‌شده — موقتاً پاسخ دریافت نمی‌کند (قابل از سرگیری).</summary>
    Paused = 4,

    /// <summary>بسته‌شده — پایان چرخه‌ی پاسخ‌گویی؛ پاسخ‌ها حفظ می‌شوند.</summary>
    Closed = 5,

    /// <summary>بایگانی‌شده — کنار گذاشته شده و فقط خواندنی.</summary>
    Archived = 6
}
