using ODCC.Application.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Reporting.Entities;

namespace ODCC.Application.Modules.Reporting.Abstractions;

/// <summary>
/// قرارداد رندر یک بسته‌ی داده‌ی گزارش به یک قالب خروجی (PDF یا Excel).
/// هر پیاده‌سازی متعلق به یک <see cref="Domain.Modules.Reporting.Enums.ReportFormat"/> است.
/// </summary>
public interface IReportRenderer
{
    /// <summary>قالبی که این رندر تولید می‌کند.</summary>
    Domain.Modules.Reporting.Enums.ReportFormat Format { get; }

    /// <summary>
    /// رندر بسته‌ی داده به یک جریان خروجی همراه با نام و نوع محتوای فایل.
    /// </summary>
    Task<RenderedReport> RenderAsync(ReportDataBundle bundle, CancellationToken ct = default);
}

/// <summary>خروجی رندر شده: جریان محتوا، نام فایل، نوع محتوا و تعداد ردیف‌های داده.</summary>
public sealed record RenderedReport(Stream Content, string FileName, string ContentType)
{
    /// <summary>تعداد ردیف‌های داده‌ای که در خروجی نوشته شده.</summary>
    public int RowCount { get; init; }
}

/// <summary>
/// انبار فایل‌های خروجی گزارش‌ها.
///
/// پیاده‌سازی پیش‌فرض از سیستم فایل استفاده می‌کند. این قرارداد در لایه‌ی
/// کاربرد قرار دارد تا سرویس گزارش‌گیری بدون دانستن جزئیات ذخیره‌سازی کار کند
/// و در آزمون‌ها با یک پیاده‌سازی موقت قابل جایگزینی باشد.
/// </summary>
public interface IReportArtifactStore
{
    /// <summary>ذخیره‌ی جریان محتوا و بازگرداندن مسیر نسبی و اندازه‌ی آن.</summary>
    Task<StoredArtifact> SaveAsync(Stream content, string fileName, CancellationToken ct = default);

    /// <summary>باز کردن جریان خواندن فایل ذخیره‌شده.</summary>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>حذف فیزیکی فایل (در زمان پاکسازی اجراهای قدیمی).</summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>آیا فایل ذخیره‌شده هنوز وجود دارد؟</summary>
    bool Exists(string relativePath);
}

/// <summary>اطلاعات فایل ذخیره‌شده.</summary>
public sealed record StoredArtifact(string RelativePath, long SizeBytes);

/// <summary>
/// تنظیمات انبار فایل‌های گزارش از بخش <c>Reports</c> پیکربندی.
/// </summary>
public sealed class ReportArtifactOptions
{
    public const string SectionName = "Reports";

    /// <summary>
    /// مسیر ریشه‌ی نسبی ذخیره‌ی خروجی گزارش‌ها (نسبت به ریشه‌ی محتوای برنامه).
    /// مسیرها در این شاخه با شناسه‌ی اجرای گزارش دسته‌بندی می‌شوند.
    /// </summary>
    public string ArtifactRoot { get; set; } = "App_Data/reports";

    /// <summary>حداکثر طول مجاز نام فایل خروجی.</summary>
    public int MaxFileNameLength { get; set; } = 180;

    /// <summary>
    /// مسیر اختیاری فایل TTF فارسی‌پشتیبان برای رندر PDF. اگر خالی باشد،
    /// فونت‌های رایج سیستم (Tahoma/Arial) به‌صورت خودکار کشف می‌شوند.
    /// </summary>
    public string? PersianFontPath { get; set; }
}
