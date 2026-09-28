namespace ODCC.Domain.Modules.ActionManagement.Enums;

/// <summary>
/// چرخه‌ی عمر یک برنامه‌ی اقدام. این ماشین وضعیت سمت سرور است که توسط
/// <c>IActionManagementService</c> حرکت می‌کند.
///
/// <code>
/// Draft ──activate──▶ Active ──complete──▶ Completed
///   │                    │ ──cancel──▶ Cancelled
///   └──archive──▶ Archived (هر وضعیت غیربایگانی‌شده ──archive──▶ Archived)
/// </code>
/// </summary>
public enum ActionPlanStatus
{
    /// <summary>پیش‌نویس: هنوز فعال نشده و یادآور/تشدید برای آن تولید نمی‌شود.</summary>
    Draft = 0,

    /// <summary>فعال: آیتم‌های آن در حال پیگیری هستند.</summary>
    Active = 1,

    /// <summary>تکمیل‌شده: همه‌ی آیتم‌های لازم انجام شده‌اند.</summary>
    Completed = 2,

    /// <summary>لغوشده: بدون تکمیل بسته شده است.</summary>
    Cancelled = 3,

    /// <summary>بایگانی‌شده (حذف نرم): از فهرست‌ها پنهان می‌شود مگر با درخواست صریح.</summary>
    Archived = 4
}

/// <summary>
/// چرخه‌ی عمر یک آیتم اقدام (یک گام اجرایی داخل برنامه).
///
/// <code>
/// Open ──start──▶ InProgress ──complete──▶ Done
///  │ ──cancel──▶ Cancelled     ──cancel──▶ Cancelled
/// </code>
/// </summary>
public enum ActionItemStatus
{
    /// <summary>باز: هنوز شروع نشده.</summary>
    Open = 1,

    /// <summary>در حال انجام: مسئول شروع کرده ولی هنوز تمام نشده.</summary>
    InProgress = 2,

    /// <summary>انجام‌شده.</summary>
    Done = 3,

    /// <summary>لغوشده (مثلاً نامناسب یا تکراری).</summary>
    Cancelled = 4
}

/// <summary>
/// اولویت یک برنامه/آیتم اقدام. اولویت بالاتر = عدد بزرگ‌تر.
/// </summary>
public enum ActionPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// منشأ یک برنامه‌ی اقدام: چگونه ایجاد شده است.
/// </summary>
public enum ActionSource
{
    /// <summary>ایجادشده به‌صورت دستی توسط یک کاربر.</summary>
    Manual = 0,

    /// <summary>تولیدشده خودکار بر اساس هشدار تحلیلات (مثلاً افت NPS زیر بنچمارک).</summary>
    AnalyticsAlert = 1,

    /// <summary>تولیدشده خودکار بر اساس یافته‌ی یک نظرسنجی (مثلاً نظرات باز باید بررسی شوند).</summary>
    SurveyFinding = 2
}

/// <summary>
/// نوع شاخصی که یک برنامه‌ی اقدامِ مبتنی بر تحلیلات به آن واکنش نشان می‌دهد.
/// این کپی محلی از شاخص‌های تحلیلات است تا ماژول اقدامات به دامنه‌ی تحلیلات
/// وابستگی مستقیم نداشته باشد (قرارداد ماژول‌ها: فقط از طریق رویدادها صحبت می‌کنند).
/// </summary>
public enum ActionMetricType
{
    Nps = 1,
    Csat = 2,
    Ces = 3
}

/// <summary>
/// درجه‌ی شدیدسازی پیگیری یک آیتم سررسیده‌نشده. هر چه عدد بزرگ‌تر باشد،
/// پیگیری تهاجمی‌تر است و به افراد بالاتر اطلاع داده می‌شود.
/// </summary>
public enum EscalationLevel
{
    /// <summary>هیچ شدیدسازی‌ای انجام نشده.</summary>
    None = 0,

    /// <summary>یادآوری اول: سررسید شده ولی هنوز تشدید نشده.</summary>
    Reminder = 1,

    /// <summary>تشدید اول: اعلان به مالک برنامه در کنار مسئول آیتم.</summary>
    EscalatedToOwner = 2,

    /// <summary>تشدید نهایی: اعلان به مدیران دارای مجوز مدیریت اقدامات.</summary>
    EscalatedToManagement = 3
}

/// <summary>
/// سنجش اثربخشی یک آیتم یا برنامه‌ی اقدام پس از اجرا.
/// این مقدار به‌صورت دستی توسط مالک/مسئول ثبت می‌شود و باید بعد از اجرا باشد.
/// </summary>
public enum EffectivenessRating
{
    /// <summary>هنوز ارزیابی نشده.</summary>
    NotAssessed = 0,

    /// <summary>موثر: مشکل هدف حل شد.</summary>
    Effective = 1,

    /// <summary>بخشی موثر بود.</summary>
    PartiallyEffective = 2,

    /// <summary>ناموثر: مشکل باقی است.</summary>
    Ineffective = 3
}
