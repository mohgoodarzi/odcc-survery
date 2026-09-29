using ODCC.Application.Abstractions;
using ODCC.Application.Modules.SystemConfiguration.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.SystemConfiguration.Entities;
using ODCC.Domain.Modules.SystemConfiguration.Enums;

namespace ODCC.Application.Modules.SystemConfiguration.Abstractions;

/// <summary>
/// سرویس پیکربندی سامانه: تنظیمات، پرچم‌های ویژگی و سیاست‌های سیستمی.
///
/// <b>مجوزها:</b> مشاهده نیازمند <c>system.view</c> و تغییر نیازمند
/// <c>system.manage</c> است. تنظیمات فقط‌خواندنی از طریق API قابل تغییر نیستند.
///
/// <b>عملکرد:</b> مقادیر پرچم‌های ویژگی و تنظیمات پرکاربرد کش می‌شوند تا
/// هر درخواست به پایگاه داده نیازمند نباشد. کش پس از هر تغییر نامعتبر می‌شود.
/// </summary>
public interface ISystemConfigurationService
{
    // --- تنظیمات ---------------------------------------------------------------

    Task<PagedResult<SettingDto>> SearchSettingsAsync(SettingSearchRequest request, CancellationToken ct = default);

    Task<Result<SettingDto>> GetSettingByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>مقدار تنظیم به‌صورت رشته (یا مقدار پیش‌فرض).</summary>
    Task<Result<string>> GetSettingValueAsync(string key, string? defaultValue = null, CancellationToken ct = default);

    /// <summary>به‌روزرسانی یک تنظیم. تنظیمات فقط‌خواندنی قابل تغییر نیستند.</summary>
    Task<Result<SettingDto>> UpdateSettingAsync(string key, UpdateSettingRequest request, CancellationToken ct = default);

    /// <summary>
    /// ایجاد یک تنظیم جدید. کلید باید یکتا باشد. این متد برای قابل‌استفاده‌بودن
    /// ماژول نیاز است — بدون آن، جدول‌ها همیشه خالی می‌مانند.
    /// </summary>
    Task<Result<SettingDto>> CreateSettingAsync(CreateSettingRequest request, CancellationToken ct = default);

    // --- پرچم‌های ویژگی ---------------------------------------------------------

    Task<PagedResult<FeatureFlagDto>> SearchFeatureFlagsAsync(FeatureFlagSearchRequest request, CancellationToken ct = default);

    Task<Result<FeatureFlagDto>> GetFeatureFlagByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// آیا پرچم ویژگی برای کاربر جاری فعال است؟ روشن بودن، عدم انقضا و در صورت
    /// نیاز فهرست مجاز/درصد را بررسی می‌کند.
    /// </summary>
    Task<Result<bool>> IsFeatureEnabledAsync(string key, CancellationToken ct = default);

    Task<Result<FeatureFlagDto>> TurnFeatureOnAsync(string key, CancellationToken ct = default);

    Task<Result<FeatureFlagDto>> TurnFeatureOffAsync(string key, CancellationToken ct = default);

    Task<Result<FeatureFlagDto>> UpdateFeatureFlagAsync(string key, UpdateFeatureFlagRequest request, CancellationToken ct = default);

    /// <summary>ایجاد یک پرچم ویژگی جدید. کلید باید یکتا باشد.</summary>
    Task<Result<FeatureFlagDto>> CreateFeatureFlagAsync(CreateFeatureFlagRequest request, CancellationToken ct = default);

    // --- سیاست‌های سیستمی -------------------------------------------------------

    Task<PagedResult<SystemPolicyDto>> SearchPoliciesAsync(SystemPolicySearchRequest request, CancellationToken ct = default);

    Task<Result<SystemPolicyDto>> GetPolicyByTypeAndKeyAsync(SystemPolicyType type, string key, CancellationToken ct = default);

    Task<Result<SystemPolicyDto>> UpdatePolicyAsync(SystemPolicyType type, string key, UpdateSystemPolicyRequest request, CancellationToken ct = default);

    /// <summary>ایجاد یک سیاست سیستمی جدید. (type, key) باید یکتا باشد.</summary>
    Task<Result<SystemPolicyDto>> CreatePolicyAsync(CreateSystemPolicyRequest request, CancellationToken ct = default);

    // --- آمار -------------------------------------------------------------------

    Task<Result<SystemConfigurationStatsDto>> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// سرویس کش‌شده‌ی پرچم‌های ویژگی. در لایه‌های کاربردی برای فعال/غیرفعال‌کردن
/// ایمن قابلیت‌ها بدون استقرار مجدد استفاده می‌شود.
///
/// <b>عملکرد:</b> نتایج در حافظه کش می‌شوند و پس از هر تغییر نامعتبر می‌گردند.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>آیا پرچم فعال است؟ (پارامتر اختیاری کاربر برای حالت AllowList).</summary>
    Task<bool> IsEnabledAsync(string key, Guid? userId = null, IReadOnlyCollection<string>? roles = null, CancellationToken ct = default);
}

/// <summary>
/// سرویس کش‌شده‌ی تنظیمات. خواندن سریع مقادیر پیکربندی بدون کوئری پایگاه داده.
/// </summary>
public interface ISettingService
{
    /// <summary>مقدار رشته‌ای تنظیم (یا پیش‌فرض).</summary>
    Task<string> GetValueAsync(string key, string? defaultValue = null, CancellationToken ct = default);

    /// <summary>مقدار بولی.</summary>
    Task<bool> GetBooleanAsync(string key, bool defaultValue = false, CancellationToken ct = default);

    /// <summary>مقدار عدد صحیح.</summary>
    Task<int> GetIntegerAsync(string key, int defaultValue = 0, CancellationToken ct = default);
}

/// <summary>مخزن تنظیمات.</summary>
public interface ISettingRepository
{
    Task<Setting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<Setting>> SearchAsync(SettingSearchRequest request, CancellationToken ct = default);
    Task<int> CountAsync(SettingSearchRequest request, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddAsync(Setting entity, CancellationToken ct = default);
    void Update(Setting entity);
}

/// <summary>مخزن پرچم‌های ویژگی.</summary>
public interface IFeatureFlagRepository
{
    Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> SearchAsync(FeatureFlagSearchRequest request, CancellationToken ct = default);
    Task<int> CountAsync(FeatureFlagSearchRequest request, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountEnabledAsync(CancellationToken ct = default);
    Task AddAsync(FeatureFlag entity, CancellationToken ct = default);
    void Update(FeatureFlag entity);
}

/// <summary>مخزن سیاست‌های سیستمی.</summary>
public interface ISystemPolicyRepository
{
    Task<SystemPolicy?> GetByTypeAndKeyAsync(SystemPolicyType type, string key, CancellationToken ct = default);
    Task<SystemPolicy?> GetEnabledByTypeAndKeyAsync(SystemPolicyType type, string key, CancellationToken ct = default);
    Task<IReadOnlyList<SystemPolicy>> SearchAsync(SystemPolicySearchRequest request, CancellationToken ct = default);
    Task<int> CountAsync(SystemPolicySearchRequest request, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddAsync(SystemPolicy entity, CancellationToken ct = default);
    void Update(SystemPolicy entity);
}

/// <summary>مرز تراکنشی ماژول پیکربندی سامانه.</summary>
public interface ISystemConfigurationUnitOfWork : IUnitOfWork;
