using System.Globalization;
using System.IO;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.FileStorage.Abstractions;

namespace ODCC.Infrastructure.Modules.FileStorage.Services;

/// <summary>
/// پیاده‌سازی امن انبار فایل بر پایه‌ی filesystem.
///
/// امنیت:
/// - نام فایل = یک Guid تصادفی + پسوند تطبیق‌شده با allowlist.
/// - تمام مسیرها داخل ریشه‌ی تنظیم‌شده محصور می‌شوند (path traversal رد می‌شود).
/// - فایل‌ها در زیرشاخه‌ی yyyy-MM برای جلوگیری از شلوغی یک پوشه ذخیره می‌شوند.
/// </summary>
public class FileSystemFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly FileStorageOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task<StoredFile> SaveAsync(Stream content, string extension, string bucket, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);

        var safeExtension = NormalizeExtension(extension);

        if (!_options.AllowedExtensions.Contains(safeExtension))
        {
            throw new InvalidOperationException($"پسوند «.{safeExtension}» مجاز نیست.");
        }

        var maxBytes = Math.Max(1L, _options.MaxFileSizeMb) * 1024 * 1024;

        var relativePath = Path.Combine(
            bucket.TrimStart('/').Replace('/', Path.DirectorySeparatorChar),
            DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            $"{Guid.CreateVersion7():N}.{safeExtension}");

        var absolutePath = ToAbsolutePath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, ct);

            if (fileStream.Length > maxBytes)
            {
                // پاک‌سازی فایلِ بیش از حد بزرگ.
                fileStream.Close();
                TryDelete(absolutePath);

                throw new InvalidOperationException($"اندازه‌ی فایل از حد مجاز ({_options.MaxFileSizeMb} مگابایت) بیشتر است.");
            }
        }

        var length = new FileInfo(absolutePath).Length;
        var contentType = GetContentType(safeExtension);

        return new StoredFile(Normalize(relativePath), length, contentType);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        Stream stream = new FileStream(ToAbsolutePath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);

        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        TryDelete(ToAbsolutePath(relativePath));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public bool Exists(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return File.Exists(ToAbsolutePath(relativePath));
    }

    /// <summary>تبدیل مسیر نسبی به مطلق با جلوگیری از path traversal.</summary>
    internal string ToAbsolutePath(string relativePath)
    {
        var root = Path.GetFullPath(Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(AppContext.BaseDirectory, _options.RootPath));

        var normalized = relativePath
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        var absolute = Path.GetFullPath(Path.Combine(root, normalized));

        // باید حتماً داخل ریشه باشد (با جداکننده‌ی دایرکتوری یا برابر ریشه).
        if (!absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(absolute, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("مسیر فایل خارج از شاخسه‌ی مجاز انبار است.");
        }

        return absolute;
    }

    /// <summary>نرمال‌سازی پسوند: حذف نقطه‌ها، کوچک‌کردن حروف.</summary>
    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var value = extension.Trim().TrimStart('.').ToLowerInvariant();

        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException("پسوند فایل خالی است.");
        }

        return value;
    }

    private string GetContentType(string extension) =>
        _options.ContentTypeMapping.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";

    private static void TryDelete(string absolutePath)
    {
        try
        {
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }
        catch
        {
            // حذف بهترین‌حالت است؛ نباید عملیات اصلی را لغو کند.
        }
    }

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/');
}
