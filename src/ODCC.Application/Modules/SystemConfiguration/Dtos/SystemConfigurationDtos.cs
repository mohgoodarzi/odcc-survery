using ODCC.Application.Abstractions;
using ODCC.Domain.Modules.SystemConfiguration.Enums;

namespace ODCC.Application.Modules.SystemConfiguration.Dtos;

// --- تنظیمات: درخواست‌ها -------------------------------------------------------

/// <summary>فیلتر جستجوی تنظیمات.</summary>
public sealed record SettingSearchRequest
{
    public string? SearchText { get; init; }
    public string? Group { get; init; }
    public SettingValueType? ValueType { get; init; }
    public ConfigurationScope? Scope { get; init; }

    /// <summary>فقط تنظیمات حساس؟</summary>
    public bool? IsSensitive { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record UpdateSettingRequest
{
    /// <summary>مقدار جدید.</summary>
    public required string Value { get; init; }
}

/// <summary>ایجاد یک تنظیم جدید.</summary>
public sealed record CreateSettingRequest
{
    /// <summary>کلید یکتا (مثلاً «analytics.min_responses»).</summary>
    public required string Key { get; init; }

    /// <summary>نام نمایشی.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>نوع مقدار — نحوه‌ی اعتبارسنجی و تفسیر را تعیین می‌کند.</summary>
    public SettingValueType ValueType { get; init; } = SettingValueType.Text;

    /// <summary>مقدار اولیه (باید با ValueType سازگار باشد).</summary>
    public required string Value { get; init; }

    /// <summary>مقدار پیش‌فرض (در صورت خالی شدن مقدار جاری).</summary>
    public string? DefaultValue { get; init; }

    /// <summary>گروه نمایشی.</summary>
    public string? Group { get; init; }

    /// <summary>
    /// آیا این تنظیم حساس است؟ تنظیمات حساس در خروجی‌ها مقدارشان برنمی‌گردد
    /// و باید از طریق ابزارهای امن تغییر کنند.
    /// </summary>
    public bool IsSensitive { get; init; }
}

// --- پرچم‌های ویژگی: درخواست‌ها -------------------------------------------------

public sealed record FeatureFlagSearchRequest
{
    public string? SearchText { get; init; }
    public FeatureFlagState? State { get; init; }
    public ConfigurationScope? Scope { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record UpdateFeatureFlagRequest
{
    /// <summary>وضعیت جدید.</summary>
    public FeatureFlagState State { get; init; } = FeatureFlagState.Off;

    /// <summary>درصد (۰ تا ۱۰۰) — فقط در حالت Percentage.</summary>
    public int? Percentage { get; init; }

    /// <summary>کلیدهای کاربران مجاز — فقط در حالت AllowList.</summary>
    public IReadOnlyList<string>? AllowedUserIds { get; init; }

    /// <summary>نقش‌های مجاز — فقط در حالت AllowList.</summary>
    public IReadOnlyList<string>? AllowedRoles { get; init; }

    /// <summary>تاریخ انقضا (ISO 8601، UTC). اختیاری.</summary>
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>ایجاد یک پرچم ویژگی جدید.</summary>
public sealed record CreateFeatureFlagRequest
{
    /// <summary>کلید یکتا (مثلاً «new-dashboard»).</summary>
    public required string Key { get; init; }

    /// <summary>نام نمایشی.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>وضعیت اولیه.</summary>
    public FeatureFlagState State { get; init; } = FeatureFlagState.Off;

    /// <summary>درصد (۰ تا ۱۰۰) — فقط در حالت Percentage.</summary>
    public int? Percentage { get; init; }

    /// <summary>کلیدهای کاربران مجاز — فقط در حالت AllowList.</summary>
    public IReadOnlyList<string>? AllowedUserIds { get; init; }

    /// <summary>نقش‌های مجاز — فقط در حالت AllowList.</summary>
    public IReadOnlyList<string>? AllowedRoles { get; init; }

    /// <summary>تاریخ انقضای اختیاری (UTC).</summary>
    public DateTime? ExpiresAt { get; init; }
}

// --- سیاست‌های سیستمی: درخواست‌ها ------------------------------------------------

public sealed record SystemPolicySearchRequest
{
    public SystemPolicyType? Type { get; init; }
    public string? SearchText { get; init; }
    public bool? IsEnabled { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record UpdateSystemPolicyRequest
{
    /// <summary>مقدار جدید.</summary>
    public required string Value { get; init; }

    /// <summary>فعال/غیرفعال‌کردن سیاست.</summary>
    public bool IsEnabled { get; init; } = true;
}

/// <summary>ایجاد یک سیاست سیستمی جدید.</summary>
public sealed record CreateSystemPolicyRequest
{
    /// <summary>نوع سیاست.</summary>
    public SystemPolicyType Type { get; init; }

    /// <summary>کلید یکتا درون نوع.</summary>
    public required string Key { get; init; }

    /// <summary>نام نمایشی.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>مقدار اولیه.</summary>
    public required string Value { get; init; }

    /// <summary>مقدار پیش‌فرض.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>فعال/غیرفعال بودن اولیه.</summary>
    public bool IsEnabled { get; init; } = true;
}

// --- خروجی‌ها --------------------------------------------------------------------

public sealed record SettingDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public SettingValueType ValueType { get; init; }
    public ConfigurationScope Scope { get; init; }
    public string? Group { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsSensitive { get; init; }
    public Guid? OrgUnitId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? LastModifiedByUserId { get; init; }
    public string? LastModifiedByUserName { get; init; }

    /// <summary>
    /// مقدار جاری. برای تنظیمات حساس، مقدار واقعی هرگز برگردانده نمی‌شود
    /// (null) و فقط وضعیت «پیکربندی‌شده» اعلام می‌شود.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>آیا مقدار غیرخالی دارد؟</summary>
    public bool HasValue { get; init; }

    public string? DefaultValue { get; init; }
}

public sealed record FeatureFlagDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public FeatureFlagState State { get; init; }
    public int Percentage { get; init; }
    public IReadOnlyList<string> AllowedUserIds { get; init; } = [];
    public IReadOnlyList<string> AllowedRoles { get; init; } = [];
    public ConfigurationScope Scope { get; init; }
    public Guid? OrgUnitId { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? LastModifiedByUserId { get; init; }
    public string? LastModifiedByUserName { get; init; }
}

public sealed record SystemPolicyDto
{
    public Guid Id { get; init; }
    public SystemPolicyType Type { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Value { get; init; } = string.Empty;
    public string? DefaultValue { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? LastModifiedByUserId { get; init; }
    public string? LastModifiedByUserName { get; init; }
}

/// <summary>آمار پیکربندی سامانه برای داشبورد.</summary>
public sealed record SystemConfigurationStatsDto
{
    public int TotalSettings { get; init; }
    public int SensitiveSettings { get; init; }
    public int TotalFeatureFlags { get; init; }
    public int EnabledFeatureFlags { get; init; }
    public int TotalPolicies { get; init; }
    public int EnabledPolicies { get; init; }
}
