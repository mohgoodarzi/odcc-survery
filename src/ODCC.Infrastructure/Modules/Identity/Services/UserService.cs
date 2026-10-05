using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.FileStorage.Abstractions;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Domain.Common;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;

namespace ODCC.Infrastructure.Modules.Identity.Services;

/// <summary>
/// پیاده‌سازی سرویس مدیریت کاربران.
/// رمزهای عبور هرگز به‌صورت متن‌وضوح پردازش یا لاگ نمی‌شوند.
/// </summary>
public sealed class UserService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IRefreshTokenStore refreshTokenStore,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> fileStorageOptions,
    IdentityDbContext identityDbContext) : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
    private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly FileStorageOptions _fileStorageOptions = fileStorageOptions.Value;
    private readonly IdentityDbContext _identityDbContext = identityDbContext;

    /// <summary>شاخه‌ی منطقی انبار فایل برای تصاویر آواتار.</summary>
    private const string AvatarBucket = "avatars";

    /// <summary>حداکثر اندازه‌ی تصویر آواتار (مگابایت).</summary>
    private const int MaxAvatarSizeMb = 5;

    private static readonly long MaxAvatarBytes = MaxAvatarSizeMb * 1024L * 1024L;

    /// <summary>پسوندهای تصویری مجاز برای آواتار (deny-by-default).</summary>
    private static readonly HashSet<string> AllowedAvatarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "gif", "webp"
    };

    /// <summary>امضای (magic bytes) انواع تصویر مجاز برای تأیید اینکه محتوا واقعاً تصویر است.</summary>
    private static readonly Dictionary<string, byte[]> ImageSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
        ["jpg"] = [0xFF, 0xD8, 0xFF],
        ["jpeg"] = [0xFF, 0xD8, 0xFF],
        ["gif"] = [0x47, 0x49, 0x46, 0x38] // «GIF8»
    };

    public async Task<Result<PagedResult<UserSummaryDto>>> SearchAsync(UserSearchRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _userManager.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();

            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(text)) ||
                (u.Email != null && u.Email.Contains(text)) ||
                u.FirstName.Contains(text) ||
                u.LastName.Contains(text));
        }

        if (request.IsActive is { } isActive)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        if (request.RoleId is { } roleId)
        {
            query = query.Where(u => _identityDbContext.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        }

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // استخراج دسته‌ای نقش‌ها برای جلوگیری از پرس‌وجوی N+1.
        var userIds = users.Select(u => u.Id).ToList();
        var roleNamesByUser = await GetRoleNamesByUserAsync(userIds, ct);

        var dtos = users
            .Select(u => ToSummary(u, roleNamesByUser.TryGetValue(u.Id, out var names) ? names : []))
            .ToList();

        return Result.Success(new PagedResult<UserSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>نقش‌های چند کاربر در یک پرس‌وجو (به جای N+1).</summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> GetRoleNamesByUserAsync(List<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var pairs = await (
            from ur in _identityDbContext.UserRoles
            join role in _identityDbContext.Roles on ur.RoleId equals role.Id
            where userIds.Contains(ur.UserId) && !role.IsDeleted
            select new { ur.UserId, RoleName = role.Name }
        ).ToListAsync(ct);

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.RoleName ?? string.Empty).ToList());
    }

    public async Task<Result<UserSummaryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<UserSummaryDto>("user_not_found", "کاربر یافت نشد.");
        }

        var roleNames = await GetRoleNamesByUserAsync([user.Id], ct);
        return Result.Success(ToSummary(user, roleNames.TryGetValue(user.Id, out var names) ? names : []));
    }

    public async Task<Result<UserSummaryDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByNameAsync(request.UserName);
        if (existing is not null)
        {
            return Result.Failure<UserSummaryDto>("username_taken", "این نام کاربری قبلاً استفاده شده است.");
        }

        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail is not null)
        {
            return Result.Failure<UserSummaryDto>("email_taken", "این ایمیل قبلاً استفاده شده است.");
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            NationalCode = request.NationalCode,
            OrgUnitId = request.OrgUnitId,
            DataScope = request.DataScope,
            IsActive = request.IsActive
        };

        // Identity رمز عبور را هش می‌کند؛ هرگز متن‌وضوح ذخیره نمی‌شود.
        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var code = createResult.Errors.First().Code;
            return Result.Failure<UserSummaryDto>(code, "ایجاد کاربر ناموفق بود.");
        }

        if (request.RoleIds.Count > 0)
        {
            await AssignRolesInternalAsync(user, request.RoleIds, ct);
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Result.Success(ToSummary(user, roles.AsReadOnly()));
    }

    public async Task<Result<UserSummaryDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<UserSummaryDto>("user_not_found", "کاربر یافت نشد.");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.OrgUnitId = request.OrgUnitId;
        user.DataScope = request.DataScope;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(user, request.Email);
            if (!emailResult.Succeeded)
            {
                return Result.Failure<UserSummaryDto>(emailResult.Errors.First().Code, "به‌روزرسانی ایمیل ناموفق بود.");
            }
            await _userManager.UpdateNormalizedEmailAsync(user);
        }

        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return Result.Success(ToSummary(user, roles.AsReadOnly()));
    }

    public async Task<Result> SetActiveAsync(SetUserActiveRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        user.IsActive = request.IsActive;
        user.DeactivatedAt = request.IsActive ? null : DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (!request.IsActive)
        {
            // غیرفعال‌سازی حساب: تمام نشست‌های فعال او باطل می‌شود.
            await _refreshTokenStore.RevokeAllForUserAsync(user.Id, revokedByIp: null, ct);
        }

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Failure("password_change_failed", "تغییر رمز عبور ناموفق بود. رمز فعلی را بررسی کنید.");
        }

        // تغییر رمز: تمام نشست‌های دیگر باید باطل شوند.
        await _refreshTokenStore.RevokeAllForUserAsync(userId, revokedByIp: null, ct);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Failure("password_reset_failed", "بازنشانی رمز عبور ناموفق بود.");
        }

        await _refreshTokenStore.RevokeAllForUserAsync(user.Id, revokedByIp: null, ct);

        return Result.Success();
    }

    public async Task<Result> AssignRolesAsync(AssignRolesRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        await AssignRolesInternalAsync(user, request.RoleIds, ct);
        return Result.Success();
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<UserProfileDto>("user_not_found", "کاربر یافت نشد.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsAsync(user, ct);

        return Result.Success(new UserProfileDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            AvatarUrl = AvatarUrlBuilder.Build(user.Id, user.AvatarUrl),
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            OrgUnitId = user.OrgUnitId,
            DataScope = user.DataScope,
            Roles = roles.AsReadOnly(),
            Permissions = permissions
        });
    }

    /// <summary>
    /// بارگذاری (یا تعویض) تصویر آواتار. تصویر اعتبارسنجی می‌شود، در انبار
    /// فایل‌ها با نامی تصادفی ذخیره می‌شود و مسیر آن در پایگاه داده می‌ماند.
    /// </summary>
    public async Task<Result<UserSummaryDto>> SetAvatarAsync(
        Guid userId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<UserSummaryDto>("user_not_found", "کاربر یافت نشد.");
        }

        if (content.Length <= 0)
        {
            return Result.Failure<UserSummaryDto>("avatar_empty", "فایل تصویر خالی است.");
        }

        if (content.Length > MaxAvatarBytes)
        {
            return Result.Failure<UserSummaryDto>("avatar_too_large",
                $"اندازه‌ی تصویر نباید بیشتر از {MaxAvatarSizeMb} مگابایت باشد.");
        }

        var extension = ExtractImageExtension(fileName);
        if (extension is null)
        {
            return Result.Failure<UserSummaryDto>("avatar_invalid_type",
                "تنها تصاویر PNG، JPEG، GIF یا WebP قابل بارگذاری هستند.");
        }

        // نوع محتوای اعلام‌شده توسط مرورگر باید تصویر باشد (لایه‌ی اول دفاع).
        if (!string.IsNullOrWhiteSpace(contentType)
            && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<UserSummaryDto>("avatar_invalid_type",
                "تنها تصاویر PNG، JPEG، GIF یا WebP قابل بارگذاری هستند.");
        }

        // محتوا در حافظه‌ی موقت کپی می‌شود تا بتوان امضای فایل را بررسی کرد
        // و سپس دقیقاً همان بایت‌ها در انبار ذخیره شوند.
        await using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, ct);

        if (!IsValidImageSignature(buffered, extension))
        {
            return Result.Failure<UserSummaryDto>("avatar_invalid_type",
                "محتوای فایل تصویر معتبر نیست یا با پسوند آن همخوانی ندارد.");
        }

        buffered.Position = 0;

        StoredFile stored;

        try
        {
            stored = await _fileStorage.SaveAsync(buffered, extension, AvatarBucket, ct);
        }
        catch (Exception)
        {
            return Result.Failure<UserSummaryDto>("avatar_upload_failed", "ذخیره‌ی تصویر آواتار ناموفق بود.");
        }

        var previousPath = user.AvatarUrl;

        user.AvatarUrl = stored.RelativePath;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            // پایگاه داده به‌روز نشد؛ فایل تازه‌نوشته‌شده پاک می‌شود تا یتیم نماند.
            await DeleteAvatarFileAsync(stored.RelativePath, ct);

            return Result.Failure<UserSummaryDto>("avatar_upload_failed", "ذخیره‌ی تصویر آواتار ناموفق بود.");
        }

        // تصویر قبلی دیگر مورد نیاز نیست؛ بهترین‌حالت حذف می‌شود.
        await DeleteAvatarFileAsync(previousPath, ct);

        var roles = await _userManager.GetRolesAsync(user);
        return Result.Success(ToSummary(user, roles.AsReadOnly()));
    }

    /// <summary>حذف تصویر آواتار کاربر.</summary>
    public async Task<Result> RemoveAvatarAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure("user_not_found", "کاربر یافت نشد.");
        }

        var previousPath = user.AvatarUrl;

        user.AvatarUrl = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await DeleteAvatarFileAsync(previousPath, ct);

        return Result.Success();
    }

    /// <summary>باز کردن تصویر آواتار کاربر برای خواندن.</summary>
    public async Task<Result<AvatarFileDto>> OpenAvatarAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            return Result.Failure<AvatarFileDto>("user_not_found", "کاربر یافت نشد.");
        }

        if (string.IsNullOrWhiteSpace(user.AvatarUrl) || !_fileStorage.Exists(user.AvatarUrl))
        {
            return Result.Failure<AvatarFileDto>("avatar_not_found", "تصویر آواتار یافت نشد.");
        }

        var stream = await _fileStorage.OpenReadAsync(user.AvatarUrl, ct);

        var contentType = _fileStorageOptions.ContentTypeMapping.TryGetValue(
            ExtractImageExtension(user.AvatarUrl) ?? string.Empty, out var mapped)
                ? mapped
                : "application/octet-stream";

        return Result.Success(new AvatarFileDto(stream, contentType, stream.Length));
    }

    /// <summary>حذف بهترین‌حالت فایل آواتار از انبار؛ خطا نباید عملیات اصلی را لغو کند.</summary>
    private async Task DeleteAvatarFileAsync(string? relativePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        try
        {
            await _fileStorage.DeleteAsync(relativePath, ct);
        }
        catch
        {
            // فایل ممکن است قبلاً حذف شده باشد یا در دسترس نباشد.
        }
    }

    /// <summary>استخراج پسوند تصویری مجاز از نام فایل (بدون نقطه و کوچک).</summary>
    private static string? ExtractImageExtension(string? fileName)
    {
        string? extension;

        try
        {
            extension = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
        }
        catch
        {
            return null;
        }

        return !string.IsNullOrEmpty(extension) && AllowedAvatarExtensions.Contains(extension)
            ? extension
            : null;
    }

    /// <summary>
    /// تطبیق امضای (magic bytes) فایل با نوع تصویر اعلام‌شده. این بررسی در کنار
    /// پسوند و نوع محتوا، لایه‌ی سوم دفاع در برابر بارگذاری محتوای غیرتصویری است.
    /// </summary>
    private static bool IsValidImageSignature(Stream content, string extension)
    {
        // WebP یک کانتینر RIFF است: «RIFF» + ۴ بایت اندازه + «WEBP».
        var isWebp = string.Equals(extension, "webp", StringComparison.OrdinalIgnoreCase);

        var signature = isWebp
            ? new byte[] { 0x52, 0x49, 0x46, 0x46 } // «RIFF»
            : ImageSignatures.TryGetValue(extension, out var known) ? known : null;

        if (signature is null)
        {
            return false;
        }

        // سرصفندهای طولانی‌تر از امضا (مثل ۱۲ بایت WebP) خوانده می‌شوند.
        var headerLength = Math.Max(signature.Length, isWebp ? 12 : 0);

        content.Position = 0;

        var header = new byte[headerLength];
        var read = content.Read(header, 0, headerLength);

        content.Position = 0;

        if (read < signature.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (header[i] != signature[i])
            {
                return false;
            }
        }

        if (isWebp)
        {
            return read >= 12
                && header[8] == 0x57  // W
                && header[9] == 0x45  // E
                && header[10] == 0x42 // B
                && header[11] == 0x50; // P
        }

        return true;
    }

    /// <summary>انتصاب نقش‌ها به کاربر (جایگزینی کامل).</summary>
    private async Task AssignRolesInternalAsync(ApplicationUser user, IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
    {
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        if (roleIds.Count == 0)
        {
            return;
        }

        var roleNames = await _identityDbContext.Roles
            .Where(r => roleIds.Contains(r.Id) && !r.IsDeleted)
            .Select(r => r.Name!)
            .ToListAsync(ct);

        if (roleNames.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, roleNames);
        }
    }

    /// <summary>مجوزهای کاربر از کلیم نقش‌ها.</summary>
    private async Task<IReadOnlyCollection<string>> GetPermissionsAsync(ApplicationUser user, CancellationToken ct)
    {
        var roleIds = await _identityDbContext.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        return await _identityDbContext.RoleClaims
            .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == Permissions.ClaimType)
            .Select(rc => rc.ClaimValue!)
            .Distinct()
            .ToListAsync(ct);
    }

    private static UserSummaryDto ToSummary(ApplicationUser user, IReadOnlyList<string> roles) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        DisplayName = user.DisplayName,
        AvatarUrl = AvatarUrlBuilder.Build(user.Id, user.AvatarUrl),
        IsActive = user.IsActive,
        EmailConfirmed = user.EmailConfirmed,
        OrgUnitId = user.OrgUnitId,
        DataScope = user.DataScope,
        CreatedAt = user.CreatedAt,
        Roles = roles
    };
}
