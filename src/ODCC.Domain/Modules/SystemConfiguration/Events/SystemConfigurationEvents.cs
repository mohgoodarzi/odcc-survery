using ODCC.Domain.Common;
using ODCC.Domain.Modules.SystemConfiguration.Enums;

namespace ODCC.Domain.Modules.SystemConfiguration.Events;

/// <summary>
/// رویدادهای دامنه‌ی ماژول پیکربندی سامانه.
///
/// <b>طراحی:</b> این رویدادها فقط کلید/مقدار عمومی و نوع تغییر را حمل می‌کنند
/// و هرگز مقادیر حساس را در توضیحات خود قرار نمی‌دهند. شنونده‌ی ممیزی فقط
/// کلیدها را لاگ می‌کند، نه مقدار قدیم/جدید را (مگر اینکه غیرحساس باشد).
///
/// شنونده‌ها: ممیزی (ثبت رخداد) و invalidate کردن کش.
/// </summary>
public sealed class SettingChangedEvent : DomainEvent
{
    public Guid SettingId { get; init; }
    public string Key { get; init; } = string.Empty;

    /// <summary>مقدار قبلی (برای لاگ ممیزی — برای تنظیمات حساس null ارسال می‌شود).</summary>
    public string? PreviousValue { get; init; }

    /// <summary>مقدار جدید (برای تنظیمات حساس null ارسال می‌شود).</summary>
    public string? NewValue { get; init; }    public SettingValueType ValueType { get; init; }
    public ConfigurationScope Scope { get; init; }
    public Guid? ModifiedByUserId { get; init; }

    public SettingChangedEvent(
        Guid settingId, string key, string? previousValue, string? newValue,
        SettingValueType valueType, ConfigurationScope scope, Guid? modifiedByUserId)
    {
        SettingId = settingId;
        Key = key;
        PreviousValue = previousValue;
        NewValue = newValue;
        ValueType = valueType;
        Scope = scope;
        ModifiedByUserId = modifiedByUserId;
    }
}

public sealed class FeatureFlagChangedEvent : DomainEvent
{
    public Guid FlagId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public FeatureFlagState State { get; init; }
    public int Percentage { get; init; }
    public ConfigurationScope Scope { get; init; }
    public Guid? ModifiedByUserId { get; init; }

    public FeatureFlagChangedEvent(
        Guid flagId, string key, string name, FeatureFlagState state,
        int percentage, ConfigurationScope scope, Guid? modifiedByUserId)
    {
        FlagId = flagId;
        Key = key;
        Name = name;
        State = state;
        Percentage = percentage;
        Scope = scope;
        ModifiedByUserId = modifiedByUserId;
    }
}

public sealed class SystemPolicyChangedEvent : DomainEvent
{
    public Guid PolicyId { get; init; }
    public SystemPolicyType Type { get; init; }
    public string Key { get; init; } = string.Empty;

    /// <summary>مقدار جدید (برای سیاست‌های حساس null ارسال می‌شود).</summary>
    public string? NewValue { get; init; }
    public bool IsEnabled { get; init; }
    public Guid? ModifiedByUserId { get; init; }

    public SystemPolicyChangedEvent(
        Guid policyId, SystemPolicyType type, string key, string? newValue,
        bool isEnabled, Guid? modifiedByUserId)
    {
        PolicyId = policyId;
        Type = type;
        Key = key;
        NewValue = newValue;
        IsEnabled = isEnabled;
        ModifiedByUserId = modifiedByUserId;
    }
}
