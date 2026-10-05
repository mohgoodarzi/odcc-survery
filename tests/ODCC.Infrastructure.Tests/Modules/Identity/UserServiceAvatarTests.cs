using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.FileStorage.Abstractions;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Infrastructure.Modules.Identity.Entities;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Identity;

/// <summary>
/// آزمون‌های تصویر آواتار: بارگذاری، تعویض، حذف، اعتبارسنجی نوع/اندازه و
/// ساخته‌شدن نشانی عمومی.
/// </summary>
public class UserServiceAvatarTests
{
    private const string Password = "Test1234!";

    /// <summary>یک PNG معتبر ۸×۸.</summary>
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAAXNSR0IArs4c6QAAABxpRE9UAAAAAgAAAA" +
        "AAAAAAAAACAAAAKAAAAAIAAAACAAAAGXl+EvUAAAAJcEhZcwAADsEAAA7BAbiRa+0AAAATSURBVAgdY/zP" +
        "wMDwn4GBgQEXAQAVMQEBeA8YOAAAAABJRU5ErkJggg==");

    /// <summary>یک GIF معتبر (۱۲ بایت حداقلی).</summary>
    private static readonly byte[] GifBytes =
        "GIF89a\u0001\u0000\u0001\u0000\u0080\u0000\u0000\u0000\u0000\u00ff"u8.ToArray();

    /// <summary>یک WebP با سرصفحه‌ی معتبر RIFF/WEBP.</summary>
    private static readonly byte[] WebpBytes = [
        0x52, 0x49, 0x46, 0x46, // «RIFF»
        0x1A, 0x00, 0x00, 0x00, // اندازه‌ی بدنه
        0x57, 0x45, 0x42, 0x50, // «WEBP»
        0x56, 0x50, 0x38, 0x4C, // «VP8L»
        0x0D, 0x00, 0x00, 0x00, // اندازه‌ی chunk
        0x2F, 0x00, 0x00, 0x00  // شروع بدنه‌ی فشرده
    ];

    /// <summary>ساخت کاربر آزمون فعال.</summary>
    private static async Task<ApplicationUser> CreateUserAsync(TestEnvironment env, string userName)
    {
        var userManager = env.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@test.local",
            EmailConfirmed = true,
            FirstName = "کاربر",
            LastName = "آزمون",
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, Password);
        result.Succeeded.Should().BeTrue("کاربر آزمون باید ساخته شود");
        return user;
    }

    private static IUserService GetUserService(TestEnvironment env) =>
        env.Services.GetRequiredService<IUserService>();

    [Fact]
    public async Task Upload_Valid_Png_Sets_Public_Avatar_Url_And_Stores_File()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "alice");
        var userService = GetUserService(env);

        var result = await userService.SetAvatarAsync(
            user.Id, new MemoryStream(PngBytes), "photo.png", "image/png");

        result.IsSuccess.Should().BeTrue();
        result.Value!.AvatarUrl.Should()
            .StartWith($"/api/identity/users/{user.Id}/avatar/")
            .And.EndWith(".png");

        // مسیر ذخیره‌شده در پایگاه داده نسبی و در شاخه‌ی آواتارهاست.
        var stored = await env.Services.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(user.Id.ToString());

        stored!.AvatarUrl.Should().StartWith("avatars/");
        stored.AvatarUrl!.Should().EndWith(".png");

        var fileStorage = env.Services.GetRequiredService<IFileStorage>();
        fileStorage.Exists(stored.AvatarUrl).Should().BeTrue("فایل باید در انبار ذخیره شود");
    }

    [Fact]
    public async Task Upload_Replaces_Previous_Avatar_And_Deletes_Old_File()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "bob");
        var userService = GetUserService(env);
        var fileStorage = env.Services.GetRequiredService<IFileStorage>();

        var first = await userService.SetAvatarAsync(
            user.Id, new MemoryStream(PngBytes), "a.png", "image/png");
        var second = await userService.SetAvatarAsync(
            user.Id, new MemoryStream(GifBytes), "b.gif", "image/gif");

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value!.AvatarUrl.Should().NotBe(second.Value!.AvatarUrl,
            "نشانی با هر بارگذاری باید تغییر کند تا حافظه‌ی نهان تازه بماند");

        var userManager = env.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = (await userManager.FindByIdAsync(user.Id.ToString()))!;

        stored.AvatarUrl.Should().EndWith(".gif");
        fileStorage.Exists(first.Value.AvatarUrl!).Should().BeFalse("تصویر قبلی باید حذف شود");
        fileStorage.Exists(stored.AvatarUrl!).Should().BeTrue();
    }

    [Fact]
    public async Task Upload_WebP_And_Gif_Are_Accepted()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "carol");
        var userService = GetUserService(env);

        var webp = await userService.SetAvatarAsync(
            user.Id, new MemoryStream(WebpBytes), "p.webp", "image/webp");
        webp.IsSuccess.Should().BeTrue();
        webp.Value!.AvatarUrl.Should().EndWith(".webp");

        var gif = await userService.SetAvatarAsync(
            user.Id, new MemoryStream(GifBytes), "p.gif", "image/gif");
        gif.IsSuccess.Should().BeTrue();
        gif.Value!.AvatarUrl.Should().EndWith(".gif");
    }

    [Fact]
    public async Task Upload_With_Unsupported_Extension_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "dave");
        var userService = GetUserService(env);

        var result = await userService.SetAvatarAsync(
            user.Id, new MemoryStream("hello"u8.ToArray()), "notes.txt", "text/plain");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("avatar_invalid_type");
    }

    [Fact]
    public async Task Upload_With_Image_Extension_But_Non_Image_Content_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "erin");
        var userService = GetUserService(env);

        var result = await userService.SetAvatarAsync(
            user.Id, new MemoryStream("this is not an image"u8.ToArray()), "fake.png", "image/png");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("avatar_invalid_type");
    }

    [Fact]
    public async Task Upload_With_Non_Image_ContentType_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "frank");
        var userService = GetUserService(env);

        var result = await userService.SetAvatarAsync(
            user.Id, new MemoryStream(PngBytes), "photo.png", "application/pdf");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("avatar_invalid_type");
    }

    [Fact]
    public async Task Upload_Too_Large_Image_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "grace");
        var userService = GetUserService(env);

        var tooLarge = new byte[5 * 1024 * 1024 + 1];

        var result = await userService.SetAvatarAsync(
            user.Id, new MemoryStream(tooLarge), "big.png", "image/png");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("avatar_too_large");
    }

    [Fact]
    public async Task Upload_Empty_Stream_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "heidi");
        var userService = GetUserService(env);

        var result = await userService.SetAvatarAsync(
            user.Id, new MemoryStream(), "photo.png", "image/png");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("avatar_empty");
    }

    [Fact]
    public async Task Upload_For_Missing_User_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var userService = GetUserService(env);

        var result = await userService.SetAvatarAsync(
            Guid.NewGuid(), new MemoryStream(PngBytes), "photo.png", "image/png");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user_not_found");
    }

    [Fact]
    public async Task OpenAvatar_Returns_Stored_Content_And_ContentType()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "ivan");
        var userService = GetUserService(env);

        await userService.SetAvatarAsync(user.Id, new MemoryStream(PngBytes), "photo.png", "image/png");

        var result = await userService.OpenAvatarAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContentType.Should().Be("image/png");
        result.Value.Length.Should().Be(PngBytes.Length);

        await using var stream = result.Value.Content;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        memory.ToArray().Should().Equal(PngBytes);
    }

    [Fact]
    public async Task OpenAvatar_For_User_Without_Avatar_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "judy");
        var userService = GetUserService(env);

        var result = await userService.OpenAvatarAsync(user.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("avatar_not_found");
    }

    [Fact]
    public async Task RemoveAvatar_Clears_Url_And_Deletes_File()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "ken");
        var userService = GetUserService(env);
        var fileStorage = env.Services.GetRequiredService<IFileStorage>();
        var userManager = env.Services.GetRequiredService<UserManager<ApplicationUser>>();

        await userService.SetAvatarAsync(user.Id, new MemoryStream(PngBytes), "photo.png", "image/png");
        var storedPath = (await userManager.FindByIdAsync(user.Id.ToString()))!.AvatarUrl;
        fileStorage.Exists(storedPath!).Should().BeTrue();

        var result = await userService.RemoveAvatarAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        (await userManager.FindByIdAsync(user.Id.ToString()))!.AvatarUrl.Should().BeNull();
        fileStorage.Exists(storedPath!).Should().BeFalse("فایل باید از انبار حذف شود");
    }

    [Fact]
    public async Task GetProfile_And_Summary_Return_Public_Avatar_Url()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var user = await CreateUserAsync(env, "larry");
        var userService = GetUserService(env);

        await userService.SetAvatarAsync(user.Id, new MemoryStream(PngBytes), "photo.png", "image/png");

        var profile = await userService.GetProfileAsync(user.Id);
        profile.IsSuccess.Should().BeTrue();
        profile.Value!.AvatarUrl.Should()
            .StartWith($"/api/identity/users/{user.Id}/avatar/")
            .And.EndWith(".png");

        var summary = await userService.GetByIdAsync(user.Id);
        summary.IsSuccess.Should().BeTrue();
        summary.Value!.AvatarUrl.Should().Be(profile.Value.AvatarUrl);
    }
}
