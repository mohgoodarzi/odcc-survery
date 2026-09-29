using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.SystemConfiguration.Enums;
using ODCC.Domain.Modules.SystemConfiguration.Events;

namespace ODCC.Infrastructure.Modules.SystemConfiguration.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول پیکربندی سامانه برای ثبت ممیزی و
/// نامعتبرکردن کش.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول پیکربندی رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه پیکربندی از وجود
/// ممیزی بداند.
/// </summary>
public sealed class SystemConfigurationAuditEventListener(
    IAuditService auditService,
    IMemoryCache cache,
    ICurrentUserService currentUserService,
    ILogger<SystemConfigurationAuditEventListener> logger) :
    IDomainEventListener<SettingChangedEvent>,
    IDomainEventListener<FeatureFlagChangedEvent>,
    IDomainEventListener<SystemPolicyChangedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly IMemoryCache _cache = cache;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<SystemConfigurationAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(16, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(SettingChangedEvent domainEvent, CancellationToken ct = default)
    {
        // نامعتبرکردن کش مقدار تنظیم.
        _cache.Remove(Services.CacheKeys.Setting(domainEvent.Key));

        // اگر مقدار جدید null است، یعنی تنظیم حساس است و مقدار در رویداد نیست.
        var isSensitive = domainEvent.NewValue is null && domainEvent.PreviousValue is null;

        return LogAsync(domainEvent, "update", domainEvent.SettingId, "system_setting",
            $"تغییر تنظیم «{domainEvent.Key}»" +
            (isSensitive ? " (تنظیم حساس)" : $" از «{Truncate(domainEvent.PreviousValue)}» به «{Truncate(domainEvent.NewValue)}»"),
            isSensitive ? "high" : "medium", ct);
    }

    public Task HandleAsync(FeatureFlagChangedEvent domainEvent, CancellationToken ct = default)
    {
        // نامعتبرکردن کش پرچم ویژگی.
        _cache.Remove(Services.CacheKeys.FeatureFlag(domainEvent.Key));

        return LogAsync(domainEvent, "update", domainEvent.FlagId, "feature_flag",
            $"تغییر پرچم ویژگی «{domainEvent.Key}» به {StateLabel(domainEvent.State)}" +
            (domainEvent.State == FeatureFlagState.Percentage ? $" ({domainEvent.Percentage}%)" : string.Empty),
            "medium", ct);
    }

    public Task HandleAsync(SystemPolicyChangedEvent domainEvent, CancellationToken ct = default)
    {
        return LogAsync(domainEvent, "update", domainEvent.PolicyId, "system_policy",
            $"تغییر سیاست {PolicyTypeLabel(domainEvent.Type)} «{domainEvent.Key}»" +
            (domainEvent.IsEnabled ? string.Empty : " (غیرفعال شد)"),
            "high", ct);
    }

    private async Task LogAsync<TEvent>(
        TEvent domainEvent, string action, Guid entityId, string entityType,
        string description, string severity, CancellationToken ct)
        where TEvent : ODCC.Domain.Common.IDomainEvent
    {
        try
        {
            var entry = new AuditEntryDto
            {
                OccurredAt = domainEvent.OccurredAt,
                UserId = _currentUserService.UserId,
                UserName = _currentUserService.UserName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Severity = severity,
                ClientIp = _currentUserService.ClientIpAddress,
                Description = description
            };

            await _auditService.LogAsync(entry, ct);
        }
        catch (Exception ex)
        {
            // شکست ممیزی نباید عملیات اصلی را لغو کند؛ فقط لاگ می‌شود.
            AuditFailed(_logger, domainEvent.GetType().Name, ex);
        }
    }

    private static string Truncate(string? value) =>
        string.IsNullOrEmpty(value) ? "خالی" : (value.Length > 80 ? value[..80] + "…" : value);

    private static string StateLabel(FeatureFlagState state) => state switch
    {
        FeatureFlagState.On => "فعال",
        FeatureFlagState.Off => "غیرفعال",
        FeatureFlagState.Percentage => "درصدی",
        FeatureFlagState.AllowList => "فهرست مجاز",
        _ => state.ToString()
    };

    private static string PolicyTypeLabel(SystemPolicyType type) => type switch
    {
        SystemPolicyType.Password => "رمز عبور",
        SystemPolicyType.Session => "نشست",
        SystemPolicyType.ResponsePrivacy => "حریم خصوصی پاسخ",
        SystemPolicyType.DataRetention => "نگهداری داده",
        SystemPolicyType.LoginSecurity => "امنیت ورود",
        _ => "سیستمی"
    };
}
