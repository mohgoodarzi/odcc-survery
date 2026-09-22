using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Identity.Abstractions;

/// <summary>
/// سرویس احراز هویت: ورود، خروج، تازه‌سازی و ابطال توکن.
/// هیچ منطق کسب‌وکاری در کنترلرها قرار ندارد؛ همه‌چیز از این قرارداد می‌گذرد.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// ورود با نام کاربری و رمز عبور.
    /// در صورت نیاز به MFA، نتیجه حاوی چالش MFA است (نه توکن).
    /// </summary>
    Task<Result<LoginResult>> LoginAsync(LoginRequest request, string? clientIp, CancellationToken ct = default);

    /// <summary>
    /// تازه‌سازی توکن دسترسی با توکن تازه‌سازی.
    /// توکن تازه‌سازی مصرف‌شده باطل و یک توکن جدید در همان خانواده صادر می‌شود (چرخش).
    /// </summary>
    Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, string? clientIp, CancellationToken ct = default);

    /// <summary>
    /// خروج: ابطال خانواده‌ی توکن تازه‌سازی جاری.
    /// </summary>
    Task<Result> LogoutAsync(string refreshToken, string? clientIp, CancellationToken ct = default);

    /// <summary>
    /// ابطال تمام نشست‌های فعال یک کاربر (مثلاً پس از تغییر رمز یا غیرفعال‌سازی حساب).
    /// </summary>
    Task<Result> RevokeAllSessionsAsync(Guid userId, CancellationToken ct = default);
}
