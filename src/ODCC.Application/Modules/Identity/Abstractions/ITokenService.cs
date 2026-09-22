using System.Security.Claims;
using ODCC.Application.Modules.Identity.Dtos;

namespace ODCC.Application.Modules.Identity.Abstractions;

/// <summary>
/// صدور و اعتبارسنجی توکن‌ها (JWT + Refresh).
/// پیاده‌سازی این قرارداد در لایه‌ی زیرساخت از JWT استفاده می‌کند.
/// توکن تازه‌سازی به‌صورت هش‌شده ذخیره می‌شود و هرگز توکن خام لاگ نمی‌شود.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// صدور جفت توکن دسترسی و تازه‌سازی برای یک کاربر.
    /// </summary>
    Task<IssuedTokens> IssueAsync(TokenIssueContext context, CancellationToken ct = default);

    /// <summary>
    /// اعتبارسنجی توکن دسترسی و استخراج کلیم‌ها.
    /// </summary>
    /// <param name="accessToken">توکن دسترسی.</param>
    /// <param name="validateLifetime">
    /// هنگام تازه‌سازی، <c>false</c> پاس داده می‌شود تا هویت کاربر از یک توکن منقضی
    /// استخراج شود (انقضا فقط با توکن تازه‌سازی بررسی می‌شود).
    /// </param>
    ClaimsPrincipal? ValidateAccessToken(string accessToken, bool validateLifetime = true);

    /// <summary>
    /// هش یک توکن تازه‌سازی (SHA-256). برای جستجوی امن در مخزن توکن‌ها.
    /// </summary>
    string HashRefreshToken(string refreshToken);
}

/// <summary>
/// زمینه‌ی صدور توکن: اطلاعات کاربر و نشست که باید داخل توکن قرار گیرند.
/// </summary>
public sealed record TokenIssueContext
{
    public required Guid UserId { get; init; }
    public required string UserName { get; init; }
    public string? Email { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public required Guid? OrgUnitId { get; init; }
    public required ODCC.Domain.Common.DataScope DataScope { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public IReadOnlyCollection<string> Permissions { get; init; } = [];
    public string? DeviceInfo { get; init; }
    public string? ClientIp { get; init; }

    /// <summary>
    /// خانواده‌ی توکنی که باید توکن تازه‌سازی جدید به آن تعلق گیرد.
    /// در چرخش توکن این مقدار برابر خانواده‌ی توکن مصرف‌شده است تا زنجیره‌ی
    /// ابطال خانواده به‌درستی کار کند. <c>null</c> یعنی نشست جدید (ورود).
    /// </summary>
    public Guid? FamilyId { get; init; }
}

/// <summary>
/// نتیجه‌ی صدور توکن: پاسخ قابل‌بازگشت به کلاینت به‌همراه هش توکن تازه‌سازی
/// و شناسه‌ی خانواده. هش هرگز به کلاینت ارسال نمی‌شود.
/// </summary>
public sealed record IssuedTokens(TokenResponse Response, string RefreshTokenHash, Guid FamilyId);
