using Microsoft.Extensions.Options;
using ODCC.Application.Modules.ActionManagement.Abstractions;

namespace ODCC.Infrastructure.Modules.ActionManagement.Services;

/// <summary>
/// تنظیمات انبار پیوست‌های اقدامات از بخش <c>Actions:EvidenceStore</c> پیکربندی.
/// </summary>
public sealed class ActionEvidenceOptions
{
    public const string SectionName = "Actions:EvidenceStore";

    /// <summary>
    /// ریشه‌ی شاخه‌ی ذخیره‌ی پیوست‌ها. در صورت خالی بودن، یک شاخسه‌ی زیر
    /// فایل‌های موقت سیستم استفاده می‌شود (فقط برای توسعه/آزمون).
    /// </summary>
    public string? RootPath { get; set; }
}

/// <summary>
/// پیاده‌سازی پیش‌فرض <see cref="IActionEvidenceStore"/>: ذخیره‌ی فایل‌ها در
/// یک شاخه‌ی ریشه‌ی پیکربندی‌شده.
///
/// <b>امنیت:</b> مسیرهای نسبی قبل از ترکیب با ریشه بررسی می‌شوند تا
/// path traversal (خروج از شاخسه‌ی مجاز) ممکن نباشد. این الگوی مشابه
/// <c>FileSystemReportArtifactStore</c> در ماژول گزارش‌گیری است.
/// </summary>
public sealed class FileSystemActionEvidenceStore(
    IOptions<ActionEvidenceOptions> options) : IActionEvidenceStore
{
    private readonly string _root = ResolveRoot(options.Value.RootPath);

    private static string ResolveRoot(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        // پیش‌فرض امن: شاخسه‌ی اختصاصی زیر فایل‌های موقت سیستم.
        return Path.Combine(Path.GetTempPath(), "odcc-action-evidence");
    }

    public async Task<StoredEvidence> SaveAsync(
        Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Directory.CreateDirectory(_root);

        // مسیر نسبی یکتا: شاخه‌ی ماهانه + نام فایل یکتا. از یک GUID نسخه ۷
        // استفاده می‌شود تا دو پیوست هم‌نام روی فایل یکدیگر ننویسند.
        var month = DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var uniqueName = MakeUniqueFileName(fileName);
        var relative = $"{month}/{uniqueName}";
        var absolute = ToAbsolutePath(relative);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        await using (var fileStream = new FileStream(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        var length = new FileInfo(absolute).Length;

        return new StoredEvidence(Normalize(relative), length);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        Stream stream = new FileStream(ToAbsolutePath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var absolute = ToAbsolutePath(relativePath);

        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        return Task.CompletedTask;
    }

    public bool Exists(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return File.Exists(ToAbsolutePath(relativePath));
    }

    /// <summary>تبدیل مسیر نسبی به مطلق با جلوگیری از path traversal.</summary>
    private string ToAbsolutePath(string relativePath)
    {
        var absolute = Path.Combine(_root, relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var fullRoot = Path.GetFullPath(_root);
        var fullPath = Path.GetFullPath(absolute);

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("مسیر پیوست اقدام خارج از شاخسه‌ی مجاز است.");
        }

        return fullPath;
    }

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/');

    /// <summary>
    /// ساخت نام فایل یکتا: شناسه‌ی یکتا به نام اصلی افزوده می‌شود تا دو پیوست
    /// هم‌نام روی فایل یکدیگر ننویسند.
    /// </summary>
    private static string MakeUniqueFileName(string fileName)
    {
        var dotIndex = fileName.LastIndexOf('.');
        var stem = dotIndex > 0 ? fileName[..dotIndex] : fileName;
        var extension = dotIndex > 0 ? fileName[dotIndex..] : string.Empty;

        // sanitize: حذف کاراکترهای غیرمجاز در نام فایل.
        stem = string.Concat(stem.Split(Path.GetInvalidFileNameChars()));

        return $"{stem}-{Guid.CreateVersion7():N}{extension}";
    }
}
