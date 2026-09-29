using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.SystemConfiguration.Abstractions;
using ODCC.Application.Modules.SystemConfiguration.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.SystemConfiguration.Entities;
using ODCC.Domain.Modules.SystemConfiguration.Enums;

namespace ODCC.Infrastructure.Modules.SystemConfiguration.Services;

/// <summary>
/// سرویس پیکربندی سامانه.
///
/// <b>امنیت:</b> مقدار تنظیمات حساس هرگز در خروجی قرار نمی‌گیرد. برای تغییر
/// تنظیمات فقط‌خواندنی خطا برمی‌گرداند.
///
/// <b>عملکرد:</b> پرچم‌های ویژگی و تنظیمات در حافظه کش می‌شوند. پس از هر
/// تغییر، کش نامعتبر می‌شود تا در درخواست بعدی مقدار تازه خوانده شود.
/// </summary>
public sealed class SystemConfigurationService(
    ISettingRepository settingRepository,
    IFeatureFlagRepository featureFlagRepository,
    ISystemPolicyRepository policyRepository,
    ICurrentUserService currentUserService,
    ISystemConfigurationUnitOfWork unitOfWork,
    ILogger<SystemConfigurationService> logger) : ISystemConfigurationService
{
    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IFeatureFlagRepository _featureFlagRepository = featureFlagRepository;
    private readonly ISystemPolicyRepository _policyRepository = policyRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ISystemConfigurationUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<SystemConfigurationService> _logger = logger;

    // --- تنظیمات ---------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<SettingDto>> SearchSettingsAsync(SettingSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _settingRepository.CountAsync(request, ct);
        var settings = await _settingRepository.SearchAsync(request, ct);

        return new PagedResult<SettingDto>
        {
            Items = settings.Select(ToSettingDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<SettingDto>> GetSettingByKeyAsync(string key, CancellationToken ct = default)
    {
        var setting = await _settingRepository.GetByKeyAsync(key, ct);

        if (setting is null)
        {
            return Result.Failure<SettingDto>("setting_not_found", "تنظیمی با این کلید وجود ندارد.");
        }

        return Result.Success(ToSettingDto(setting));
    }

    /// <inheritdoc/>
    public async Task<Result<string>> GetSettingValueAsync(string key, string? defaultValue = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<string>("setting_invalid_key", "کلید تنظیم نمی‌تواند خالی باشد.");
        }

        var setting = await _settingRepository.GetByKeyAsync(key, ct);

        if (setting is null)
        {
            return Result.Success(defaultValue ?? string.Empty);
        }

        return Result.Success(string.IsNullOrWhiteSpace(setting.Value) ? (setting.DefaultValue ?? defaultValue ?? string.Empty) : setting.Value);
    }

    /// <inheritdoc/>
    public async Task<Result<SettingDto>> UpdateSettingAsync(string key, UpdateSettingRequest request, CancellationToken ct = default)
    {
        var setting = await _settingRepository.GetByKeyAsync(key, ct);

        if (setting is null)
        {
            return Result.Failure<SettingDto>("setting_not_found", "تنظیمی با این کلید وجود ندارد.");
        }

        if (setting.IsReadOnly)
        {
            return Result.Failure<SettingDto>("setting_read_only", "این تنظیم فقط‌خواندنی است و از طریق API قابل تغییر نیست.");
        }

        // اعتبارسنجی نوع مقدار — قبل از ذخیره.
        if (!IsValidValueType(setting.ValueType, request.Value))
        {
            return Result.Failure<SettingDto>("setting_invalid_value",
                $"مقدار واردشده با نوع «{GetTypeLabel(setting.ValueType)}» سازگار نیست.");
        }

        var previousValue = setting.Value;

        setting.SetValue(request.Value, _currentUserService.UserId, _currentUserService.UserName);

        // برای تنظیمات حساس، مقدار در رویداد و ممیزی ظاهر نمی‌شود.
        setting.RaiseChangedEvent(setting.IsSensitive ? null : previousValue);

        _settingRepository.Update(setting);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToSettingDto(setting));
    }

    /// <inheritdoc/>
    public async Task<Result<SettingDto>> CreateSettingAsync(CreateSettingRequest request, CancellationToken ct = default)
    {
        var key = request.Key.Trim();

        var existing = await _settingRepository.GetByKeyAsync(key, ct);

        if (existing is not null)
        {
            return Result.Failure<SettingDto>("setting_exists", "تنظیمی با این کلید از قبل وجود دارد.");
        }

        // اعتبارسنجی نوع مقدار — قبل از ذخیره.
        if (!IsValidValueType(request.ValueType, request.Value))
        {
            return Result.Failure<SettingDto>("setting_invalid_value",
                $"مقدار واردشده با نوع «{GetTypeLabel(request.ValueType)}» سازگار نیست.");
        }

        var setting = new Setting
        {
            Key = key,
            Name = request.Name.Trim(),
            Description = request.Description,
            ValueType = request.ValueType,
            Value = request.Value,
            DefaultValue = request.DefaultValue,
            Group = request.Group,
            Scope = ConfigurationScope.System,
            IsSensitive = request.IsSensitive,
            IsReadOnly = false,
            LastModifiedByUserId = _currentUserService.UserId,
            LastModifiedByUserName = _currentUserService.UserName
        };

        // برای تنظیمات حساس، مقدار در رویداد و ممیزی ظاهر نمی‌شود. در ساخت
        // تنظیم، مقدار قبلی وجود ندارد — رشته‌ی خالی نشان‌دهنده‌ی «از خالی» است.
        setting.RaiseChangedEvent(setting.IsSensitive ? null : string.Empty);

        await _settingRepository.AddAsync(setting, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToSettingDto(setting));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<FeatureFlagDto>> SearchFeatureFlagsAsync(FeatureFlagSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _featureFlagRepository.CountAsync(request, ct);
        var flags = await _featureFlagRepository.SearchAsync(request, ct);

        return new PagedResult<FeatureFlagDto>
        {
            Items = flags.Select(ToFeatureFlagDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<FeatureFlagDto>> GetFeatureFlagByKeyAsync(string key, CancellationToken ct = default)
    {
        var flag = await _featureFlagRepository.GetByKeyAsync(key, ct);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>("feature_flag_not_found", "پرچم ویژگی با این کلید وجود ندارد.");
        }

        return Result.Success(ToFeatureFlagDto(flag));
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> IsFeatureEnabledAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<bool>("feature_flag_invalid_key", "کلید پرچم نمی‌تواند خالی باشد.");
        }

        var flag = await _featureFlagRepository.GetByKeyAsync(key, ct);

        if (flag is null)
        {
            // پرچم تعریف‌نشده = غیرفعال (fail-closed).
            return Result.Success(false);
        }

        return Result.Success(IsFlagEnabledForCurrentUser(flag));
    }

    /// <inheritdoc/>
    public async Task<Result<FeatureFlagDto>> TurnFeatureOnAsync(string key, CancellationToken ct = default)
    {
        var flag = await _featureFlagRepository.GetByKeyAsync(key, ct);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>("feature_flag_not_found", "پرچم ویژگی با این کلید وجود ندارد.");
        }

        flag.TurnOn(_currentUserService.UserId, _currentUserService.UserName);
        flag.RaiseChangedEvent();

        _featureFlagRepository.Update(flag);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToFeatureFlagDto(flag));
    }

    /// <inheritdoc/>
    public async Task<Result<FeatureFlagDto>> TurnFeatureOffAsync(string key, CancellationToken ct = default)
    {
        var flag = await _featureFlagRepository.GetByKeyAsync(key, ct);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>("feature_flag_not_found", "پرچم ویژگی با این کلید وجود ندارد.");
        }

        flag.TurnOff(_currentUserService.UserId, _currentUserService.UserName);
        flag.RaiseChangedEvent();

        _featureFlagRepository.Update(flag);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToFeatureFlagDto(flag));
    }

    /// <inheritdoc/>
    public async Task<Result<FeatureFlagDto>> UpdateFeatureFlagAsync(string key, UpdateFeatureFlagRequest request, CancellationToken ct = default)
    {
        var flag = await _featureFlagRepository.GetByKeyAsync(key, ct);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>("feature_flag_not_found", "پرچم ویژگی با این کلید وجود ندارد.");
        }

        var userId = _currentUserService.UserId;
        var userName = _currentUserService.UserName;

        switch (request.State)
        {
            case FeatureFlagState.On:
                flag.TurnOn(userId, userName);
                break;

            case FeatureFlagState.Off:
                flag.TurnOff(userId, userName);
                break;

            case FeatureFlagState.Percentage:
                if (request.Percentage is not { } percentage)
                {
                    return Result.Failure<FeatureFlagDto>("feature_flag_percentage_required",
                        "در حالت درصدی، مقدار Percentage الزامی است.");
                }

                flag.SetPercentage(percentage, userId, userName);
                break;

            case FeatureFlagState.AllowList:
                flag.SetAllowList(
                    request.AllowedUserIds ?? [],
                    request.AllowedRoles ?? [],
                    userId,
                    userName);
                break;

            default:
                return Result.Failure<FeatureFlagDto>("feature_flag_invalid_state", "وضعیت پرچم نامعتبر است.");
        }

        flag.ExpiresAt = request.ExpiresAt;

        flag.RaiseChangedEvent();

        _featureFlagRepository.Update(flag);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToFeatureFlagDto(flag));
    }

    /// <inheritdoc/>
    public async Task<Result<FeatureFlagDto>> CreateFeatureFlagAsync(CreateFeatureFlagRequest request, CancellationToken ct = default)
    {
        var key = request.Key.Trim();

        var existing = await _featureFlagRepository.GetByKeyAsync(key, ct);

        if (existing is not null)
        {
            return Result.Failure<FeatureFlagDto>("feature_flag_exists", "پرچم ویژگی با این کلید از قبل وجود دارد.");
        }

        var userId = _currentUserService.UserId;
        var userName = _currentUserService.UserName;

        var flag = new FeatureFlag
        {
            Key = key,
            Name = request.Name.Trim(),
            Description = request.Description,
            Scope = ConfigurationScope.System,
            ExpiresAt = request.ExpiresAt
        };

        switch (request.State)
        {
            case FeatureFlagState.On:
                flag.TurnOn(userId, userName);
                break;

            case FeatureFlagState.Off:
                flag.TurnOff(userId, userName);
                break;

            case FeatureFlagState.Percentage:
                if (request.Percentage is not { } percentage)
                {
                    return Result.Failure<FeatureFlagDto>("feature_flag_percentage_required",
                        "در حالت درصدی، مقدار Percentage الزامی است.");
                }

                flag.SetPercentage(percentage, userId, userName);
                break;

            case FeatureFlagState.AllowList:
                flag.SetAllowList(request.AllowedUserIds ?? [], request.AllowedRoles ?? [], userId, userName);
                break;

            default:
                return Result.Failure<FeatureFlagDto>("feature_flag_invalid_state", "وضعیت پرچم نامعتبر است.");
        }

        flag.RaiseChangedEvent();

        await _featureFlagRepository.AddAsync(flag, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToFeatureFlagDto(flag));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<SystemPolicyDto>> SearchPoliciesAsync(SystemPolicySearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _policyRepository.CountAsync(request, ct);
        var policies = await _policyRepository.SearchAsync(request, ct);

        return new PagedResult<SystemPolicyDto>
        {
            Items = policies.Select(ToPolicyDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<SystemPolicyDto>> GetPolicyByTypeAndKeyAsync(SystemPolicyType type, string key, CancellationToken ct = default)
    {
        var policy = await _policyRepository.GetByTypeAndKeyAsync(type, key, ct);

        if (policy is null)
        {
            return Result.Failure<SystemPolicyDto>("policy_not_found", "سیاستی با این نوع و کلید وجود ندارد.");
        }

        return Result.Success(ToPolicyDto(policy));
    }

    /// <inheritdoc/>
    public async Task<Result<SystemPolicyDto>> UpdatePolicyAsync(SystemPolicyType type, string key, UpdateSystemPolicyRequest request, CancellationToken ct = default)
    {
        var policy = await _policyRepository.GetByTypeAndKeyAsync(type, key, ct);

        if (policy is null)
        {
            return Result.Failure<SystemPolicyDto>("policy_not_found", "سیاستی با این نوع و کلید وجود ندارد.");
        }

        policy.SetValue(request.Value, _currentUserService.UserId, _currentUserService.UserName);

        if (request.IsEnabled)
        {
            policy.Enable();
        }
        else
        {
            policy.Disable();
        }

        policy.RaiseChangedEvent();

        _policyRepository.Update(policy);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToPolicyDto(policy));
    }

    /// <inheritdoc/>
    public async Task<Result<SystemPolicyDto>> CreatePolicyAsync(CreateSystemPolicyRequest request, CancellationToken ct = default)
    {
        var key = request.Key.Trim();

        var existing = await _policyRepository.GetByTypeAndKeyAsync(request.Type, key, ct);

        if (existing is not null)
        {
            return Result.Failure<SystemPolicyDto>("policy_exists", "سیاستی با این نوع و کلید از قبل وجود دارد.");
        }

        var policy = new SystemPolicy
        {
            Type = request.Type,
            Key = key,
            Name = request.Name.Trim(),
            Description = request.Description,
            Value = request.Value,
            DefaultValue = request.DefaultValue,
            LastModifiedByUserId = _currentUserService.UserId,
            LastModifiedByUserName = _currentUserService.UserName
        };

        if (request.IsEnabled)
        {
            policy.Enable();
        }
        else
        {
            policy.Disable();
        }

        policy.RaiseChangedEvent();

        await _policyRepository.AddAsync(policy, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToPolicyDto(policy));
    }

    /// <inheritdoc/>
    public async Task<Result<SystemConfigurationStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new SystemConfigurationStatsDto
        {
            TotalSettings = await _settingRepository.CountAsync(ct),
            SensitiveSettings = await _settingRepository.CountAsync(
                new SettingSearchRequest { IsSensitive = true }, ct),
            TotalFeatureFlags = await _featureFlagRepository.CountAsync(ct),
            EnabledFeatureFlags = await _featureFlagRepository.CountEnabledAsync(ct),
            TotalPolicies = await _policyRepository.CountAsync(ct),
            EnabledPolicies = await _policyRepository.CountAsync(
                new SystemPolicySearchRequest { IsEnabled = true }, ct)
        };

        return Result.Success(stats);
    }

    // --- کمک‌ها ------------------------------------------------------------------

    /// <summary>
    /// آیا پرچم برای کاربر جاری فعال است؟ منطق: On + عدم انقضا + AllowList/Percentage.
    /// </summary>
    private bool IsFlagEnabledForCurrentUser(FeatureFlag flag)
    {
        if (flag.ExpiresAt is { } expires && expires <= DateTime.UtcNow)
        {
            return false;
        }

        return flag.State switch
        {
            FeatureFlagState.On => true,
            FeatureFlagState.Off => false,
            FeatureFlagState.Percentage => IsInPercentage(flag.Key, flag.Percentage),
            FeatureFlagState.AllowList => IsInAllowList(flag),
            _ => false
        };
    }

    /// <summary>
    /// آیا کاربر جاری داخل درصد مجاز است؟ از هش تعیینیِ کلید+کاربر استفاده
    /// می‌کند تا یک کاربر همیشه نتیجه‌ی یکسانی بگیرد (جلوگیری از پرش تجربه).
    /// هش تعیینی است تا با ری‌استارت یا مقیاس‌افزایی bucket کاربر تغییر نکند.
    /// </summary>
    private bool IsInPercentage(string flagKey, int percentage)
    {
        if (percentage <= 0)
        {
            return false;
        }

        if (percentage >= 100)
        {
            return true;
        }

        var bucket = DeterministicHash($"{flagKey}:{_currentUserService.UserId}");
        return bucket % 100 < (uint)percentage;
    }

    /// <summary>
    /// هش تعیینی FNV-1a (۳۲ بیتی). پایدار بین پردازش‌ها، معماری‌ها و ری‌استارت‌ها
    /// (برخلاف <c>string.GetHashCode</c> که به ازای هر پردازش seed تصادفی می‌گیرد).
    /// </summary>
    private static uint DeterministicHash(string value)
    {
        var hash = 2166136261u;

        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }

    private bool IsInAllowList(FeatureFlag flag)
    {
        var userId = _currentUserService.UserId?.ToString();

        if (!string.IsNullOrEmpty(userId) && flag.AllowedUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return flag.AllowedRoles.Count > 0
            && _currentUserService.Roles.Any(role => flag.AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>آیا مقدار رشته‌ای با نوع تعریف‌شده سازگار است؟</summary>
    private static bool IsValidValueType(SettingValueType type, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return type switch
            {
                SettingValueType.Text => value.Length <= 4000,
                SettingValueType.WholeNumber => int.TryParse(value, CultureInfo.InvariantCulture, out _),
                SettingValueType.FractionalNumber => decimal.TryParse(value, CultureInfo.InvariantCulture, out _),
                SettingValueType.TrueFalse => bool.TryParse(value, out _),
                SettingValueType.Date => DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                SettingValueType.Email => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value.Trim()),
                SettingValueType.Url => Uri.TryCreate(value.Trim(), UriKind.Absolute, out _),
                SettingValueType.Duration => TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out _),
                SettingValueType.Json => IsValidJson(value),
                _ => true
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetTypeLabel(SettingValueType type) => type switch
    {
        SettingValueType.Text => "رشته",
        SettingValueType.WholeNumber => "عدد صحیح",
        SettingValueType.FractionalNumber => "عدد اعشاری",
        SettingValueType.TrueFalse => "بولی",
        SettingValueType.Date => "تاریخ",
        SettingValueType.Email => "ایمیل",
        SettingValueType.Url => "آدرس اینترنتی",
        SettingValueType.Duration => "مدت زمان",
        SettingValueType.Json => "JSON",
        _ => "نامشخص"
    };

    /// <summary>
    /// تبدیل به DTO. مقدار تنظیمات حساس هرگز برگردانده نمی‌شود.
    /// </summary>
    private static SettingDto ToSettingDto(Setting setting) => new()
    {
        Id = setting.Id,
        Key = setting.Key,
        Name = setting.Name,
        Description = setting.Description,
        ValueType = setting.ValueType,
        Scope = setting.Scope,
        Group = setting.Group,
        IsReadOnly = setting.IsReadOnly,
        IsSensitive = setting.IsSensitive,
        OrgUnitId = setting.OrgUnitId,
        CreatedAt = setting.CreatedAt,
        UpdatedAt = setting.UpdatedAt,
        LastModifiedByUserId = setting.LastModifiedByUserId,
        LastModifiedByUserName = setting.LastModifiedByUserName,
        // تنظیم حساس: فقط «دارای مقدار» اعلام می‌شود، خود مقدار نه.
        Value = setting.IsSensitive ? null : setting.Value,
        HasValue = !string.IsNullOrWhiteSpace(setting.Value),
        DefaultValue = setting.IsSensitive ? null : setting.DefaultValue
    };

    private static FeatureFlagDto ToFeatureFlagDto(FeatureFlag flag) => new()
    {
        Id = flag.Id,
        Key = flag.Key,
        Name = flag.Name,
        Description = flag.Description,
        State = flag.State,
        Percentage = flag.Percentage,
        AllowedUserIds = flag.AllowedUserIds,
        AllowedRoles = flag.AllowedRoles,
        Scope = flag.Scope,
        OrgUnitId = flag.OrgUnitId,
        ExpiresAt = flag.ExpiresAt,
        IsEnabled = flag.IsEnabled,
        CreatedAt = flag.CreatedAt,
        UpdatedAt = flag.UpdatedAt,
        LastModifiedByUserId = flag.LastModifiedByUserId,
        LastModifiedByUserName = flag.LastModifiedByUserName
    };

    private static SystemPolicyDto ToPolicyDto(SystemPolicy policy) => new()
    {
        Id = policy.Id,
        Type = policy.Type,
        Key = policy.Key,
        Name = policy.Name,
        Description = policy.Description,
        Value = policy.Value,
        DefaultValue = policy.DefaultValue,
        IsEnabled = policy.IsEnabled,
        CreatedAt = policy.CreatedAt,
        UpdatedAt = policy.UpdatedAt,
        LastModifiedByUserId = policy.LastModifiedByUserId,
        LastModifiedByUserName = policy.LastModifiedByUserName
    };
}

/// <summary>
/// تنظیمات کش ماژول پیکربندی سامانه.
/// </summary>
public sealed class SystemConfigurationOptions
{
    public const string SectionName = "SystemConfiguration";

    /// <summary>
    /// مدت زمان ماندگاری کش پرچم‌های ویژگی و تنظیمات (ثانیه). مقادیر پرکاربرد
    /// برای جلوگیری از کوئری پایگاه داده در هر درخواست کش می‌شوند.
    /// </summary>
    public int CacheExpirationSeconds { get; set; } = 30;
}

/// <summary>
/// پیاده‌سازی کش‌شده‌ی پرچم‌های ویژگی. خواندن سریع برای لایه‌های کاربردی.
///
/// <b>کش امن:</b> به‌جای کش‌کردن موجودیتِ ردیابی‌شده‌ی EF Core (که قابل تغییر
/// است و می‌تواند قبل از commit به کش نشت کند)، یک snapshot تغییرناپذیر در کش
/// قرار می‌گیرد. این امر اطمینان می‌دهد که مسیر نوشتن (که همان کلید را از
/// مخزن با ردیابی بارگذاری و تغییر می‌دهد) نمی‌تواند مقدار کش‌شده را قبل از
/// invalid شدن پس از commit آلوده کند.
/// </summary>
public sealed class CachedFeatureFlagService(
    IFeatureFlagRepository featureFlagRepository,
    ICurrentUserService currentUserService,
    IMemoryCache cache,
    IOptions<SystemConfigurationOptions> options) : IFeatureFlagService
{
    private readonly IFeatureFlagRepository _featureFlagRepository = featureFlagRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMemoryCache _cache = cache;
    private readonly SystemConfigurationOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(string key, Guid? userId = null, IReadOnlyCollection<string>? roles = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var flag = await GetCachedFlagAsync(key, ct);

        if (flag is null)
        {
            // پرچم تعریف‌نشده = غیرفعال (fail-closed).
            return false;
        }

        if (flag.ExpiresAt is { } expires && expires <= DateTime.UtcNow)
        {
            return false;
        }

        // اگر فراخوان صریحاً کاربر/نقش‌ها را مشخص نکرده، کاربر جاری استفاده می‌شود
        // تا یک پرچم AllowList مبتنی بر نقش به‌اشتباه همیشه غیرفعال ارزیابی نشود.
        var resolvedUser = userId ?? _currentUserService.UserId;
        var resolvedRoles = roles ?? _currentUserService.Roles;

        return flag.State switch
        {
            FeatureFlagState.On => true,
            FeatureFlagState.Off => false,
            FeatureFlagState.Percentage => IsInPercentage(flag.Key, flag.Percentage, resolvedUser),
            FeatureFlagState.AllowList => IsInAllowList(flag, resolvedUser, resolvedRoles),
            _ => false
        };
    }

    private async Task<CachedFlag?> GetCachedFlagAsync(string key, CancellationToken ct)
    {
        var cacheKey = CacheKeys.FeatureFlag(key);

        if (_cache.TryGetValue(cacheKey, out CachedFlag? cached) && cached is not null)
        {
            return cached;
        }

        var flag = await _featureFlagRepository.GetByKeyAsync(key, ct);

        CachedFlag? snapshot = flag is null ? null : CachedFlag.From(flag);

        if (snapshot is not null)
        {
            _cache.Set(cacheKey, snapshot, TimeSpan.FromSeconds(Math.Max(1, _options.CacheExpirationSeconds)));
        }

        return snapshot;
    }

    private static bool IsInPercentage(string flagKey, int percentage, Guid? userId)
    {
        if (percentage <= 0)
        {
            return false;
        }

        if (percentage >= 100)
        {
            return true;
        }

        // هش تعیینی (نه GetHashCode رشته‌ی دات‌نت که به ازای هر پردازش seed
        // می‌خورد): با تغییرات ری‌استارت یا مقیاس‌افزایی، کاربر همیشه در همان
        // bucket می‌افتد.
        var bucket = DeterministicHash($"{flagKey}:{userId}");
        return bucket % 100 < (uint)percentage;
    }

    private static bool IsInAllowList(CachedFlag flag, Guid? userId, IReadOnlyCollection<string>? roles)
    {
        var userIdText = userId?.ToString();

        if (!string.IsNullOrEmpty(userIdText) && flag.AllowedUserIds.Contains(userIdText, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return roles is not null && roles.Count > 0
            && roles.Any(role => flag.AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// هش تعیینی FNV-1a (۳۲ بیتی). پایدار بین پردازش‌ها، معماری‌ها و ری‌استارت‌ها.
    /// </summary>
    private static uint DeterministicHash(string value)
    {
        var hash = 2166136261u;

        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }

    /// <summary>snapshot تغییرناپذیر از پرچم برای کش امن.</summary>
    private sealed record CachedFlag(
        string Key,
        FeatureFlagState State,
        int Percentage,
        IReadOnlyList<string> AllowedUserIds,
        IReadOnlyList<string> AllowedRoles,
        DateTime? ExpiresAt)
    {
        public static CachedFlag From(FeatureFlag flag) => new(
            flag.Key,
            flag.State,
            flag.Percentage,
            flag.AllowedUserIds.AsReadOnly(),
            flag.AllowedRoles.AsReadOnly(),
            flag.ExpiresAt);
    }
}

/// <summary>
/// پیاده‌سازی کش‌شده‌ی تنظیمات. خواندن سریع مقادیر پیکربندی.
/// </summary>
public sealed class CachedSettingService(
    ISettingRepository settingRepository,
    IMemoryCache cache,
    IOptions<SystemConfigurationOptions> options) : ISettingService
{
    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IMemoryCache _cache = cache;
    private readonly SystemConfigurationOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task<string> GetValueAsync(string key, string? defaultValue = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var value = await GetCachedValueAsync(key, ct);

        return string.IsNullOrWhiteSpace(value) ? (defaultValue ?? string.Empty) : value;
    }

    /// <inheritdoc/>
    public async Task<bool> GetBooleanAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, null, ct);

        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    /// <inheritdoc/>
    public async Task<int> GetIntegerAsync(string key, int defaultValue = 0, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, null, ct);

        return int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
    }

    private async Task<string?> GetCachedValueAsync(string key, CancellationToken ct)
    {
        var cacheKey = CacheKeys.Setting(key);

        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        var setting = await _settingRepository.GetByKeyAsync(key, ct);
        var value = setting is null
            ? null
            : (string.IsNullOrWhiteSpace(setting.Value) ? setting.DefaultValue : setting.Value);

        // حتی null هم کش می‌شود تا کوئری پایگاه داده تکرار نشود.
        _cache.Set(cacheKey, value, TimeSpan.FromSeconds(Math.Max(1, _options.CacheExpirationSeconds)));

        return value;
    }
}

/// <summary>کلیدهای کش ماژول پیکربندی (برای invalidation).</summary>
public static class CacheKeys
{
    private const string Prefix = "system-config";

    public static string FeatureFlag(string key) => $"{Prefix}:flag:{key}";
    public static string Setting(string key) => $"{Prefix}:setting:{key}";
}
