namespace ODCC.Domain.Modules.Workflow.Enums;

/// <summary>
/// نوع موجودیتی که یک گردش کار می‌تواند چرخه‌ی عمر آن را کنترل کند.
/// </summary>
public enum WorkflowEntityType
{
    /// <summary>نظرسنجی‌ها (انتشار، شروع، توقف، بستن).</summary>
    Survey = 0,

    /// <summary>کمپین‌ها (زمان‌بندی، اجرا، تکمیل).</summary>
    Campaign = 1,

    /// <summary>برنامه‌های اقدام (فعال‌سازی، تکمیل).</summary>
    ActionPlan = 2,

    /// <summary>تعاریف گزارش (فعال‌سازی).</summary>
    ReportDefinition = 3,

    /// <summary>نوع عمومی/سفارشی (شناسه‌ی موجودیت فقط مرجع است).</summary>
    Custom = 99
}

/// <summary>
/// چرخه‌ی عمر تعریف گردش کار.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>پیش‌نویس: در حال طراحی، هنوز قابل استفاده نیست.</summary>
    Draft = 0,

    /// <summary>فعال: می‌تواند نمونه‌سازی کند.</summary>
    Active = 1,

    /// <summary>بایگانی‌شده (حذف نرم): دیگر نمونه‌ی جدیدی نمی‌سازد.</summary>
    Archived = 2
}

/// <summary>
/// وضعیت یک نمونه‌ی گردش کار.
/// </summary>
public enum WorkflowInstanceState
{
    /// <summary>در حال اجرا.</summary>
    Running = 0,

    /// <summary>تکمیل‌شده (به وضعیت پایانی رسیده).</summary>
    Completed = 1,

    /// <summary>لغوشده توسط کاربر.</summary>
    Cancelled = 2,

    /// <summary>ناموفق (خطای غیرقابل‌بازگشت).</summary>
    Failed = 3
}

/// <summary>
/// وضعیت یک درخواست تأیید.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>در انتظار تصمیم.</summary>
    Pending = 0,

    /// <summary>تأییدشده.</summary>
    Approved = 1,

    /// <summary>ردشده.</summary>
    Rejected = 2,

    /// <summary>لغوشده (مثلاً به‌خاطر لغو نمونه).</summary>
    Cancelled = 3,

    /// <summary>منقضی‌شده (گذشته از زمان انقضا).</summary>
    Expired = 4
}
