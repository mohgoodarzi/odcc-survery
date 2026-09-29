using ODCC.Domain.Common;

namespace ODCC.Application.Modules.FileStorage.Abstractions;

/// <summary>
/// انبار امن فایل‌های سامانه: بارگذاری، دانلود و حذف فایل‌ها با اعتبارسنجی
/// نام، نوع و اندازه.
///
/// <b>امنیت (حیاتی):</b>
/// <list type="bullet">
///   <item>نام فایل هرگز از کلاینت پذیرفته نمی‌شود — یک نام تصادفی و امن تولید می‌شود.</item>
///   <item>مسیر خروج از ریشه‌ی انبار (path traversal) غیرممکن است.</item>
///   <item>پسوندهای مجاز به‌صورت allowlist بررسی می‌شوند (deny-by-default).</item>
///   <item>نوع MIME فایل با محتوای واقعی تطبیق داده نمی‌شود (signature check)؛
///   برای جلوگیری از بارگذاری محتوای اجرایی، پسوند کافی نیست ولی لایه‌ی اول دفاع است.</item>
/// </list>
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// ذخیره‌ی یک استریم به‌عنوان فایل با پسوند دلخواه.
    /// </summary>
    /// <param name="content">محتوای فایل.</param>
    /// <param name="extension">پسوند مجاز (مثلاً «pdf»). باید در allowlist باشد.</param>
    /// <param name="bucket">شاخه‌ی منطقی (مثلاً «evidence»).</param>
    /// <param name="ct">توکن لغو.</param>
    /// <returns>مسیر نسبی فایل ذخیره‌شده و اندازه‌ی آن.</returns>
    Task<StoredFile> SaveAsync(Stream content, string extension, string bucket, CancellationToken ct = default);

    /// <summary>باز کردن فایل برای خواندن.</summary>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>حذف فایل.</summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>آیا فایل وجود دارد؟</summary>
    bool Exists(string relativePath);
}

/// <summary>نتیجه‌ی ذخیره‌ی فایل.</summary>
public sealed record StoredFile(string RelativePath, long Length, string ContentType);

/// <summary>تنظیمات انبار فایل.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>ریشه‌ی فیزیکی انبار (نسبی به ContentRoot یا مطلق).</summary>
    public string RootPath { get; set; } = "App_Data/files";

    /// <summary>حداکثر اندازه‌ی هر فایل (مگابایت).</summary>
    public int MaxFileSizeMb { get; set; } = 25;

    /// <summary>پسوندهای مجاز (بدون نقطه).</summary>
    public HashSet<string> AllowedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "png", "jpg", "jpeg", "gif", "webp", "xlsx", "xls", "docx", "doc", "txt", "csv", "zip"
    };

    /// <summary>
    /// نگاشت پسوند → نوع MIME. برای Content-Type دانلود استفاده می‌شود.
    /// </summary>
    public Dictionary<string, string> ContentTypeMapping { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["gif"] = "image/gif",
        ["webp"] = "image/webp",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["xls"] = "application/vnd.ms-excel",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["doc"] = "application/msword",
        ["txt"] = "text/plain",
        ["csv"] = "text/csv",
        ["zip"] = "application/zip"
    };
}

/// <summary>سرویس اعتبارسنجی و انتقال امن فایل آپلودی به انبار.</summary>
public interface IFileUploadService
{
    /// <summary>
    /// ذخیره‌ی یک فایل آپلودی (IFormFile) در انبار. نام اصلی فایل نادیده گرفته
    /// می‌شود و فقط پسوند آن (پس از تطبیق با allowlist) استفاده می‌شود.
    /// </summary>
    Task<Result<StoredFile>> SaveUploadAsync(Stream content, string originalFileName, string bucket, CancellationToken ct = default);
}
