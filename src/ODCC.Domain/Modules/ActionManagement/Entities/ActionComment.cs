using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.ActionManagement.Entities;

/// <summary>
/// دیدگاه (کامنت) روی یک <see cref="ActionItem"/>. تاریخچه‌ی گفتگو را برای
/// پیگیری و هماهنگی بین مالک برنامه و مسئول آیتم نگه می‌دارد.
///
/// <b>حریم خصوصی:</b> این موجودیت هرگز محتوای پاسخ یک پاسخ‌گوی مشخص را ذخیره
/// نمی‌کند. کامنت‌ها توسط کاربران سامانه (با نام خودشان) نوشته می‌شوند.
/// </summary>
public sealed class ActionComment : BaseEntity
{
    /// <summary>آیتمی که این دیدگاه به آن تعلق دارد.</summary>
    public Guid ActionItemId { get; set; }

    public ActionItem? ActionItem { get; set; }

    /// <summary>نویسنده‌ی دیدگاه (کاربر سامانه).</summary>
    public Guid? AuthorUserId { get; set; }

    /// <summary>نام نمایشی نویسنده در زمان نوشتن (snapshot).</summary>
    public string? AuthorUserName { get; set; }

    /// <summary>متن دیدگاه.</summary>
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// مستندات/پیوست یک <see cref="ActionItem"/>: فایلی که مدرک اجرا یا زمینه‌ی
/// بیشتر است (مثلاً تصویر اقدام انجام‌شده، گزارش پیوست).
///
/// محتوای فایل از طریق <c>IActionEvidenceStore</c> ذخیره می‌شود (بیرون از
/// پایگاه داده) و این موجودیت فقط متادیتای آن را نگه می‌دارد. الگوی مشابه
/// <c>ReportExecution</c> در ماژول گزارش‌گیری است.
/// </summary>
public sealed class ActionEvidence : BaseEntity
{
    /// <summary>آیتمی که این پیوست به آن تعلق دارد.</summary>
    public Guid ActionItemId { get; set; }

    public ActionItem? ActionItem { get; set; }

    /// <summary>نام فایل اصلی ( همان چیزی که کاربر آپلود کرده).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>نوع محتوای فایل (MIME).</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>اندازه‌ی فایل به بایت.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>مسیر نسبی در انبار پیوست‌ها (داخل پایگاه داده نگه‌داری نمی‌شود).</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>کاربر آپلودکننده.</summary>
    public Guid? UploadedByUserId { get; set; }

    /// <summary>نام نمایشی آپلودکننده (snapshot).</summary>
    public string? UploadedByUserName { get; set; }

    /// <summary>زمان آپلود (UTC).</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
