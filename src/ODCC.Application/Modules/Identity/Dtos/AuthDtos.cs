namespace ODCC.Application.Modules.Identity.Dtos;

/// <summary>
/// درخواست ورود به سامانه.
/// </summary>
public sealed record LoginRequest
{
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>اطلاعات دستگاه/مرورگر برای ردیابی نشست (اختیاری).</summary>
    public string? DeviceInfo { get; init; }
}

/// <summary>
/// درخواست تازه‌سازی توکن دسترسی.
/// </summary>
public sealed record RefreshRequest
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// پاسخ موفقیت‌آمیز احراز هویت: توکن دسترسی + توکن تازه‌سازی.
/// توکن تازه‌سازی فقط یک‌بار در این پاسخ به مشتری نمایش داده می‌شود و نسخه‌ی
/// بعدی آن در چرخش بعدی صادر می‌شود.
/// </summary>
public sealed record TokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public DateTime ExpiresAt { get; init; }
    public UserProfileDto? Profile { get; init; }
}

/// <summary>
/// پروفایل کاربر احراز هویت‌شده.
/// </summary>
public sealed record UserProfileDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
    public bool TwoFactorEnabled { get; init; }
    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitName { get; init; }
    public ODCC.Domain.Common.DataScope DataScope { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public IReadOnlyCollection<string> Permissions { get; init; } = [];
}

/// <summary>
/// نتیجه‌ی عملیات ورود. در صورت نیاز به مرحله‌ی دوم (MFA) فیلد <see cref="RequiresMfa"/>
/// تنظیم می‌شود تا کلاینت مرحله‌ی بعد را نمایش دهد.
/// </summary>
public sealed record LoginResult
{
    public bool RequiresMfa { get; init; }
    public string? MfaChallengeToken { get; init; }
    public TokenResponse? Tokens { get; init; }
}

/// <summary>
/// درخواست اعتبارسنجی توکن دسترسی. توکن در بدنه‌ی درخواست قرار می‌گیرد تا
/// در URL و لاگ‌های دسترسی ثبت نشود.
/// </summary>
public sealed record ValidateTokenRequest
{
    public string AccessToken { get; init; } = string.Empty;
}
