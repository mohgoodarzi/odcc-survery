namespace ODCC.Domain.Modules.Reporting.Enums;

/// <summary>
/// نوع محتوای یک گزارش. نوع گزارش تعیین می‌کند کدام داده‌ها جمع‌آوری و در
/// خروجی نمایش داده می‌شوند.
/// </summary>
public enum ReportType
{
    /// <summary>شاخص‌های یک نظرسنجی: NPS/CSAT/CES، توزیع پاسخ سؤال‌ها و روند زمانی.</summary>
    SurveyAnalytics = 1,

    /// <summary>خلاصه‌ی داشبورد تحلیلات سطح شرکت (چند نظرسنجی).</summary>
    DashboardSummary = 2,

    /// <summary>مقایسه‌ی شاخص‌های یک نظرسنجی با بنچمارک‌های تعریف‌شده.</summary>
    BenchmarkComparison = 3
}

/// <summary>
/// قالب خروجی گزارش.
/// </summary>
public enum ReportFormat
{
    /// <summary>سند PDF با چیدمان راست‌به‌چپ و فونت فارسی.</summary>
    Pdf = 1,

    /// <summary>کاربرگ Excel (OOXML) برای تحلیل بیشتر در ابزارهای جدولی.</summary>
    Excel = 2
}

/// <summary>
/// زمان‌بندی اجرای یک تعریف گزارش.
/// </summary>
public enum ReportSchedule
{
    /// <summary>فقط با درخواست صریح کاربر اجرا می‌شود (زمان‌بندی ندارد).</summary>
    OneTime = 0,

    /// <summary>هر روز یک‌بار.</summary>
    Daily = 1,

    /// <summary>هر هفته یک‌بار.</summary>
    Weekly = 2,

    /// <summary>هر ماه یک‌بار.</summary>
    Monthly = 3
}

/// <summary>
/// چرخه‌ی عمر یک تعریف گزارش.
///
/// <code>
/// Draft ──activate──▶ Active ──archive──▶ Archived
/// </code>
///
/// فقط تعاریف <c>Active</c> توسط زمان‌بند اجرا می‌شوند. بایگانی‌شده‌ها نرم
/// حذف می‌شوند و در نتایج جستجو فقط با پرچم نمایش داده می‌شوند.
/// </summary>
public enum ReportStatus
{
    /// <summary>تعریف ذخیره شده ولی هنوز فعال نشده (زمان‌بند آن را نادیده می‌گیرد).</summary>
    Draft = 0,

    /// <summary>فعال: در زمان‌بندی‌های رسیده اجرا می‌شود.</summary>
    Active = 1,

    /// <summary>بایگانی‌شده (حذف نرم): دیگر اجرا نمی‌شود ولی تاریخچه باقی می‌ماند.</summary>
    Archived = 2
}

/// <summary>
/// وضعیت یک اجرای گزارش.
///
/// <code>
/// Pending ──start──▶ Running ──succeed──▶ Succeeded
///                   Running ──fail────▶ Failed
/// </code>
/// </summary>
public enum ReportExecutionStatus
{
    /// <summary>در صف اجرا (ردیف ذخیره شده ولی رندر شروع نشده).</summary>
    Pending = 0,

    /// <summary>در حال جمع‌آوری داده و رندر.</summary>
    Running = 1,

    /// <summary>خروجی با موفقیت تولید و ذخیره شد.</summary>
    Succeeded = 2,

    /// <summary>اجرای شکست‌خورده (پیام خطا در ردیف ذخیره شده).</summary>
    Failed = 3
}
