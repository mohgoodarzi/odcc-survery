using ODCC.Application.Modules.Reporting.Abstractions;
using Microsoft.Extensions.Options;

namespace ODCC.Infrastructure.Modules.Reporting.Services;

/// <summary>
/// ذخیره‌ی فایل‌های خروجی گزارش در سیستم فایل.
///
/// فایل‌ها در مسیر ریشه‌ی پیکربندی‌شده (<c>Reports:ArtifactRoot</c>) و درون یک
/// زیرشاخسه‌ی ماهانه قرار می‌گیرند. مسیر ذخیره‌شده در ردیف اجرا <b>نسبی</b> است
/// تا جابه‌جایی ریشه (مثلاً بین محیط‌ها) فایل‌ها را بی‌ارتباط نکند.
/// </summary>
public sealed class FileSystemReportArtifactStore(
    IOptions<ReportArtifactOptions> options) : IReportArtifactStore
{
    private readonly ReportArtifactOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task<StoredArtifact> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var relativePath = Path.Combine(DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), fileName);
        var absolutePath = ToAbsolutePath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);

        return new StoredArtifact(Normalize(relativePath), fileStream.Length);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var absolutePath = ToAbsolutePath(relativePath);
        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var absolutePath = ToAbsolutePath(relativePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public bool Exists(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return File.Exists(ToAbsolutePath(relativePath));
    }

    /// <summary>تبدیل مسیر نسبی ذخیره‌شده به مسیر مطلق روی دیسک.</summary>
    internal string ToAbsolutePath(string relativePath)
    {
        var root = _options.ArtifactRoot;

        if (!Path.IsPathRooted(root))
        {
            // مسیرهای نسبی نسبت به ریشه‌ی محتوای برنامه (wwwroot/..) حل می‌شوند.
            root = Path.Combine(AppContext.BaseDirectory, root);
        }

        var absolute = Path.Combine(root, relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        // جلوگیری از خروج مسیر از ریشه (path traversal): مسیر نهایی باید زیردرخت ریشه باشد.
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(absolute);

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("مسیر فایل گزارش خارج از شاخشه‌ی مجاز است.");
        }

        return fullPath;
    }

    private static string Normalize(string relativePath) =>
        relativePath.Replace('\\', '/');
}
