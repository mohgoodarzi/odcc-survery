using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.FileStorage.Abstractions;
using ODCC.Domain.Common;
using ODCC.Infrastructure.Modules.FileStorage.Services;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.FileStorage;

/// <summary>
/// آزمون‌های انبار امن فایل.
///
/// پوشش (امنیت مسیر حیاتی است):
/// - پسوند فقط از allowlist پذیرفته می‌شود (deny-by-default).
/// - نام فایل از کلاینت هرگز پذیرفته نمی‌شود — نام تصادفی تولید می‌شود.
/// - path traversal (خروج از ریشه) در ذخیره، خواندن، حذف و Exists رد می‌شود.
/// - حمله‌ی پیشوند مسیر (شاخسه‌ی خواهرخوانده با پیشوند مشابه) رد می‌شود.
/// - اندازه‌ی فایل اعمال می‌شود و فایلِ بیش‌ازحد پاک می‌شود.
///
/// همه‌ی فایل‌ها در شاخه‌ی موقت سیستم‌عامل نوشته می‌شوند و در پایان آزمون پاک می‌شوند.
/// </summary>
public class FileStorageTests : IDisposable
{
    private readonly List<string> _roots = [];

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (var root in _roots)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch
            {
                // پاک‌سازی بهترین‌حالت است.
            }
        }
    }

    // --- ذخیره و خواندن ----------------------------------------------------------

    [Fact]
    public async Task SaveAsync_WithAllowedExtension_StoresAndRoundTrips()
    {
        var storage = CreateStorage();

        var content = Encoding.UTF8.GetBytes("hello-world");

        var stored = await storage.SaveAsync(new MemoryStream(content), "pdf", "evidence");

        stored.Length.Should().Be(content.Length);
        stored.ContentType.Should().Be("application/pdf");

        // نام فایل تصادفی و مسیر داخل bucket/shahr است.
        stored.RelativePath.Should().StartWith("evidence/");
        stored.RelativePath.Should().EndWith(".pdf");
        Path.GetFileName(stored.RelativePath).Should().NotBeNullOrEmpty();
        stored.RelativePath.Should().MatchRegex(@"^evidence/\d{4}-\d{2}/[0-9a-f]+\.pdf$");

        storage.Exists(stored.RelativePath).Should().BeTrue();

        await using var readStream = await storage.OpenReadAsync(stored.RelativePath);
        using var reader = new StreamReader(readStream);
        (await reader.ReadToEndAsync()).Should().Be("hello-world");
    }

    [Fact]
    public async Task SaveAsync_NormalizesExtension_CaseInsensitive()
    {
        var storage = CreateStorage();

        var stored = await storage.SaveAsync(new MemoryStream([1, 2, 3]), ".PDF", "reports");

        // پسوند کوچک‌سازی می‌شود تا allowlist یکپارچه اعمال شود.
        stored.RelativePath.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task SaveAsync_WithDisallowedExtension_Throws()
    {
        var storage = CreateStorage();

        var act = async () => await storage.SaveAsync(new MemoryStream([1]), "exe", "evidence");

        // deny-by-default: فقط پسوندهای مجاز.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveAsync_WithEmptyExtension_Throws()
    {
        var storage = CreateStorage();

        var act = async () => await storage.SaveAsync(new MemoryStream([1]), "  ", "evidence");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_TooLarge_DeletesFileAndThrows()
    {
        // سقف کوچک برای آزمون.
        var storage = CreateStorage(maxFileSizeMb: 1);
        var big = new byte[2 * 1024 * 1024]; // ۲ مگابایت

        var act = async () => await storage.SaveAsync(new MemoryStream(big), "txt", "evidence");

        await act.Should().ThrowAsync<InvalidOperationException>();

        // فایلِ بیش از حد بزرگ نباید روی دیسک بماند.
        Directory.GetFiles(Path.Combine(_roots[0]), "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WithNullContent_Throws()
    {
        var storage = CreateStorage();

        var act = async () => await storage.SaveAsync(null!, "pdf", "evidence");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_WithEmptyBucket_Throws()
    {
        var storage = CreateStorage();

        var act = async () => await storage.SaveAsync(new MemoryStream([1]), "pdf", "  ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var storage = CreateStorage();

        var stored = await storage.SaveAsync(new MemoryStream([1, 2]), "txt", "evidence");

        await storage.DeleteAsync(stored.RelativePath);

        storage.Exists(stored.RelativePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_DoesNotThrow()
    {
        var storage = CreateStorage();

        var act = async () => await storage.DeleteAsync("evidence/2026-01/missing.txt");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OpenReadAsync_MissingFile_Throws()
    {
        var storage = CreateStorage();

        var act = async () => await storage.OpenReadAsync("evidence/2026-01/missing.txt");

        // ممکن است FileNotFoundException یا DirectoryNotFoundException باشد —
        // هر دو از IOException مشتق می‌شوند. مهم این است که مسیر خوانده نمی‌شود.
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task Exists_WithMissingFile_ReturnsFalse()
    {
        var storage = CreateStorage();

        storage.Exists("evidence/2026-01/missing.txt").Should().BeFalse();
    }

    // --- امنیت مسیر (path traversal) ---------------------------------------------

    [Theory]
    [InlineData("../../evil.txt")]
    [InlineData("..\\..\\evil.txt")]
    [InlineData("evidence/../../evil.txt")]
    [InlineData("evidence/../../../../../../evil.txt")]
    public async Task OpenReadAsync_OutsideRoot_Throws(string relativePath)
    {
        var storage = CreateStorage();

        var act = async () => await storage.OpenReadAsync(relativePath);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task OpenReadAsync_WithLeadingSeparators_StaysInsideRoot()
    {
        // جداکننده‌های ابتدای مسیر جدا می‌شوند و مسیر داخل ریشه باقی می‌ماند —
        // این مسیرها فرار نمی‌کنند، فقط فایل/شاخسه‌ی ناموجود هستند.
        var storage = CreateStorage();

        var act = async () => await storage.OpenReadAsync("/etc/passwd");

        await act.Should().ThrowAsync<IOException>();
        storage.Exists("/etc/passwd").Should().BeFalse();
    }

    [Theory]
    [InlineData("../../evil.txt")]
    [InlineData("evidence/../../../evil.txt")]
    public async Task DeleteAsync_OutsideRoot_Throws(string relativePath)
    {
        var storage = CreateStorage();

        var act = async () => await storage.DeleteAsync(relativePath);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("../../evil.txt")]
    [InlineData("evidence/../../../evil.txt")]
    public async Task Exists_OutsideRoot_Throws(string relativePath)
    {
        var storage = CreateStorage();

        var act = () => storage.Exists(relativePath);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task PathTraversal_SiblingWithSharedPrefix_Throws()
    {
        // شاخسه‌ی خواهرخوانده با پیشوند مشابه نباید فریب ریشه را بزند.
        var root = Path.Combine(Path.GetTempPath(), "odcc-filestorage-prefix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sibling = Path.Combine(Path.GetDirectoryName(root)!, Path.GetFileName(root) + "-sibling");
        Directory.CreateDirectory(sibling);
        _roots.Add(root);
        _roots.Add(sibling);

        var storage = new FileSystemFileStorage(Options.Create(new FileStorageOptions
        {
            RootPath = root,
            MaxFileSizeMb = 25
        }));

        var escape = "../" + Path.GetFileName(sibling) + "/evil.txt";

        var act = async () => await storage.OpenReadAsync(escape);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // --- سرویس بارگذاری (IFileUploadService) ---------------------------------------

    [Fact]
    public async Task SaveUpload_WithDisallowedExtension_ReturnsFailure()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var uploadService = env.Services.GetRequiredService<IFileUploadService>();

        var result = await uploadService.SaveUploadAsync(new MemoryStream([1, 2, 3]), "malware.exe", "evidence");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("file_extension_not_allowed");
    }

    [Fact]
    public async Task SaveUpload_WithoutExtension_ReturnsFailure()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var uploadService = env.Services.GetRequiredService<IFileUploadService>();

        var result = await uploadService.SaveUploadAsync(new MemoryStream([1]), "noextension", "evidence");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("file_extension_invalid");
    }

    [Fact]
    public async Task SaveUpload_TooLarge_ReturnsFailure()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var uploadService = env.Services.GetRequiredService<IFileUploadService>();

        // کمی بیش از سقف ۲۵ مگابایت.
        var oversized = new byte[(25 * 1024 * 1024) + 1];

        var result = await uploadService.SaveUploadAsync(new MemoryStream(oversized), "report.pdf", "evidence");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("file_too_large");
    }

    [Fact]
    public async Task SaveUpload_IgnoresOriginalFileName_StoresSecurely()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var storage = env.Services.GetRequiredService<IFileStorage>();
        var uploadService = env.Services.GetRequiredService<IFileUploadService>();

        // نام اصلی مخرب — نباید هیچ نقشی در نام فایل ذخیره‌شده داشته باشد.
        const string maliciousName = "../../etc/passwd.pdf";

        var result = await uploadService.SaveUploadAsync(new MemoryStream([1, 2, 3, 4]), maliciousName, "evidence");

        result.IsSuccess.Should().BeTrue();
        result.Value!.RelativePath.Should().StartWith("evidence/");
        result.Value.RelativePath.Should().NotContain("..");
        result.Value.RelativePath.Should().NotContain("passwd");
        storage.Exists(result.Value.RelativePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveUpload_WhenStorageFails_ReturnsFailureWithoutLeakingException()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var uploadService = env.Services.GetRequiredService<IFileUploadService>();

        // استریمی که طولش زیر سقف است اما محتوای واقعی‌اش فراتر می‌شود —
        // انبار فایل آن را پس از نوشتن تشخیص و پاک می‌کند.
        var result = await uploadService.SaveUploadAsync(new LyingStream(), "evidence.pdf", "evidence");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("file_storage_failed");
    }

    [Fact]
    public async Task SaveUpload_PreservesContentType()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var uploadService = env.Services.GetRequiredService<IFileUploadService>();

        var result = await uploadService.SaveUploadAsync(new MemoryStream([1]), "image.PNG", "evidence");

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContentType.Should().Be("image/png");
    }

    // --- کمک‌ها ------------------------------------------------------------------

    private FileSystemFileStorage CreateStorage(int maxFileSizeMb = 25)
    {
        var root = Path.Combine(Path.GetTempPath(), "odcc-filestorage-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _roots.Add(root);

        return new FileSystemFileStorage(Options.Create(new FileStorageOptions
        {
            RootPath = root,
            MaxFileSizeMb = maxFileSizeMb
        }));
    }

    /// <summary>
    /// استریمی که طولش را کمتر از سقف اعلام می‌کند ولی هنگام کپی بیشتر از سقف
    /// می‌نویسد — برای آزمون مسیر شکستِ انبار فایل.
    /// </summary>
    private sealed class LyingStream : Stream
    {
        private readonly byte[] _buffer = new byte[30 * 1024 * 1024];
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 1024; // دروغ می‌گوید — زیر سقف است.
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var toCopy = Math.Min(count, _buffer.Length - _position);
            if (toCopy <= 0)
            {
                return 0;
            }

            Array.Copy(_buffer, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
