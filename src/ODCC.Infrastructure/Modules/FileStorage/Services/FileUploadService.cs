using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.FileStorage.Abstractions;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Modules.FileStorage.Services;

/// <summary>
/// سرویس انتقال امن فایل آپلودی به انبار. نام اصلی هرگر پذیرفته نمی‌شود —
/// فقط پسوند آن (پس از تطبیق با allowlist) استفاده می‌شود.
/// </summary>
public sealed class FileUploadService(
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> options,
    ILogger<FileUploadService> logger) : IFileUploadService
{
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly FileStorageOptions _options = options.Value;
    private readonly ILogger<FileUploadService> _logger = logger;

    private static readonly Action<ILogger, string, string, Exception?> UploadRejected = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(1, "FileUploadRejected"),
        "بارگذاری فایل «{OriginalFileName}» به‌خاطر پسوند غیرمجاز «.{Extension}» رد شد.");

    /// <inheritdoc/>
    public async Task<Result<StoredFile>> SaveUploadAsync(
        Stream content, string originalFileName, string bucket, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);

        var extension = ExtractExtension(originalFileName);

        if (extension is null)
        {
            UploadRejected(_logger, originalFileName ?? string.Empty, "نامشخص", null);

            return Result.Failure<StoredFile>("file_extension_invalid",
                "نام فایل فاقد پسوند است یا پسوند آن مجاز نیست.");
        }

        if (!_options.AllowedExtensions.Contains(extension))
        {
            UploadRejected(_logger, originalFileName ?? string.Empty, extension, null);

            return Result.Failure<StoredFile>("file_extension_not_allowed",
                $"بارگذاری فایل با پسوند «.{extension}» مجاز نیست.");
        }

        var maxBytes = Math.Max(1L, _options.MaxFileSizeMb) * 1024 * 1024;

        if (content.Length > maxBytes)
        {
            return Result.Failure<StoredFile>("file_too_large",
                $"اندازه‌ی فایل از حد مجاز ({_options.MaxFileSizeMb} مگابایت) بیشتر است.");
        }

        try
        {
            var stored = await _fileStorage.SaveAsync(content, extension, bucket, ct);

            return Result.Success(stored);
        }
        catch (Exception)
        {
            return Result.Failure<StoredFile>("file_storage_failed", "ذخیره‌ی فایل ناموفق بود.");
        }
    }

    /// <summary>استخراج پسوند امن از نام اصلی فایل.</summary>
    private static string? ExtractExtension(string? originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return null;
        }

        try
        {
            var extension = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();

            return string.IsNullOrEmpty(extension) ? null : extension;
        }
        catch
        {
            return null;
        }
    }
}
