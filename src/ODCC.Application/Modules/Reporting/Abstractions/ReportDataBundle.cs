using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Application.Modules.Reporting.Abstractions;

/// <summary>
/// بسته‌ی داده‌ی نمایش‌گرا یک گزارش: مجموعه‌ای از بخش‌های جدولی.
///
/// این نوع عمداً **خنثی و بدون وابستگی به قالب** است: هم رندر PDF و هم رندر
/// Excel از همین ساختار تغذیه می‌شوند تا داده‌ی یک گزارش در دو قالب یکسان
/// بماند. همچنین هیچ شناسه‌ی پاسخ‌گویی در آن قرار ندارد — فقط تجمع‌ها.
/// </summary>
public sealed record ReportDataBundle
{
    /// <summary>عنوان اصلی گزارش.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>زیرعنوان: دامنه، بازه و سازنده.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>نوع گزارش.</summary>
    public ReportType Type { get; init; }

    /// <summary>زمان تولید داده (UTC).</summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    /// <summary>نام راه‌انداز (یا «زمان‌بند خودکار»).</summary>
    public string GeneratedBy { get; init; } = string.Empty;

    /// <summary>بخش‌های جدولی گزارش به ترتیب نمایش.</summary>
    public IReadOnlyList<ReportSection> Sections { get; init; } = [];
}

/// <summary>یک بخش جدولی از گزارش.</summary>
public sealed record ReportSection
{
    /// <summary>عنوان بخش.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>ستون‌های جدول.</summary>
    public IReadOnlyList<ReportColumn> Columns { get; init; } = [];

    /// <summary>ردیف‌های داده; هر ردیف به‌اندازه‌ی ستون‌ها مقدار دارد.</summary>
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; } = [];

    /// <summary>یادداشت کوتاه زیر جدول (اختیاری).</summary>
    public string? Footnote { get; init; }
}

/// <summary>یک ستون جدول گزارش.</summary>
/// <param name="Title">عنوان ستون.</param>
/// <param name="Width">عرض پیشنهادی بر حسب پوینت (PDF) یا کاراکتر (Excel).</param>
/// <param name="ColumnType">نوع قالب‌بندی مقدار.</param>
public sealed record ReportColumn(string Title, double Width = 120, ReportColumnType ColumnType = ReportColumnType.Text);

/// <summary>نوع قالب‌بندی ستون گزارش.</summary>
public enum ReportColumnType
{
    /// <summary>متن (راست‌چین برای فارسی).</summary>
    Text = 0,

    /// <summary>عدد دلخواه.</summary>
    Number = 1,

    /// <summary>درصد (مقدار ۰ تا ۱۰۰).</summary>
    Percent = 2,

    /// <summary>تاریخ/زمان UTC.</summary>
    Date = 3
}
