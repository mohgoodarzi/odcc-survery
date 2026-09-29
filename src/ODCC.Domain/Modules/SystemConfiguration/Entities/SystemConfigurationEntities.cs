using ODCC.Domain.Common;
using ODCC.Domain.Modules.SystemConfiguration.Enums;
using ODCC.Domain.Modules.SystemConfiguration.Events;

namespace ODCC.Domain.Modules.SystemConfiguration.Entities;

/// <summary>
/// یک تنظیم سامانه با کلید یکتا و مقدار رشته‌ای نوع‌دار.
///
/// <b>طراحی:</b> مقدار همیشه به‌صورت رشته ذخیره می‌شود و <see cref="SettingValueType"/>
/// نحوه‌ی تفسیر آن را تعیین می‌کند. این الگو امکان پیکربندی یکپارچه را بدون
/// مهاجرت برای انواع جدید فراهم می‌کند.
///
/// <b>امنیت:</b> این موجودیت هرگز نباید مقدار راز (رمز عبور، توکن، کلید) را
/// نگه دارد؛ رازها باید در پیکربندی ایمن (user secrets / متغیرهای محیطی)
/// باشند. مقدار «آیا پیکربندی شده؟» در سرویس بررسی می‌شود.
/// </summary>
public sealed class Setting : BaseEntity
{
    /// <summary>کلید یکتا (مثلاً «analytics.min_responses»).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>نام نمایشی.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>توضیح هدف و مقدار پیش‌فرض.</summary>
    public string? Description { get; set; }

    /// <summary>نوع مقدار برای اعتبارسنجی و تبدیل.</summary>
    public SettingValueType ValueType { get; set; } = SettingValueType.Text;

    /// <summary>مقدار جاری (رشته). تبدیل بر اساس <see cref="ValueType"/>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>مقدار پیش‌فرض (در صورت خالی بودن جاری).</summary>
    public string? DefaultValue { get; set; }

    /// <summary>داخل گروه چه چیزی قرار می‌گیرد (برای نمایش).</summary>
    public string? Group { get; set; }

    /// <summary>دامنه‌ی اعمال.</summary>
    public ConfigurationScope Scope { get; set; } = ConfigurationScope.System;

    /// <summary>آیا فقط خواندنی است (تنها کد آن را تغییر می‌دهد)؟</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>آیا در زمان بازیابی باید رمزگشایی شود (داده‌ی حساس پیکربندی)؟</summary>
    public bool IsSensitive { get; set; }

    /// <summary>شناسه‌ی سازمان در صورتی که <see cref="Scope"/> سازمانی باشد.</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>کاربری که آخرین بار تغییر داده.</summary>
    public Guid? LastModifiedByUserId { get; set; }

    /// <summary>نام نمایشی آخرین تغییردهنده (snapshot).</summary>
    public string? LastModifiedByUserName { get; set; }

    // --- چرخه‌ی عمر -------------------------------------------------------------

    /// <summary>
    /// تنظیم مقدار جدید. اعتبارسنجی نوع باید پیش از این متد در سرویس انجام شود؛
    /// این متد فقط مقدار را جایگزین می‌کند و رویداد را منتشر می‌کند.
    /// </summary>
    public void SetValue(string value, Guid? modifiedByUserId, string? modifiedByUserName)
    {
        Value = value;
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedByUserName = modifiedByUserName;
    }

    // --- رویدادهای دامنه --------------------------------------------------------

    /// <param name="previousValue">
    /// مقدار قبلی برای ممیزی. ارسال <c>null</c> یعنی «تنظیم حساس است» — در این
    /// حالت مقدار جدید هم وارد رویداد نمی‌شود تا نشتی رخ ندهد. ارسال رشته‌ی
    /// خالی یعنی «از حالت بدون مقدار» (مثلاً ساخت تنظیم جدید).
    /// </param>
    public void RaiseChangedEvent(string? previousValue)
    {
        var newValue = previousValue is null ? null : Value;

        RaiseDomainEvent(new SettingChangedEvent(Id, Key, previousValue, newValue, ValueType, Scope, LastModifiedByUserId));
    }
}

/// <summary>
/// یک پرچم ویژگی (Feature Flag): روش ایمن برای فعال/غیرفعال‌کردن قابلیت‌ها
/// بدون استقرار مجدد. وضعیت می‌تواند روشن، خاموش، درصدی یا فهرست مجاز باشد.
/// </summary>
public sealed class FeatureFlag : BaseEntity
{
    /// <summary>کلید یکتا (مثلاً «new-dashboard»).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>نام نمایشی.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>توضیح قابلیتی که این پرچم کنترل می‌کند.</summary>
    public string? Description { get; set; }

    /// <summary>وضعیت پرچم.</summary>
    public FeatureFlagState State { get; set; } = FeatureFlagState.Off;

    /// <summary>
    /// درصد فعال‌سازی (۰ تا ۱۰۰) وقتی <see cref="State"/> برابر
    /// <see cref="FeatureFlagState.Percentage"/> است.
    /// </summary>
    public int Percentage { get; set; }

    /// <summary>کلیدهای کاربران مجاز وقتی State برابر AllowList است.</summary>
    public List<string> AllowedUserIds { get; set; } = [];

    /// <summary>نقش‌های مجاز وقتی State برابر AllowList است.</summary>
    public List<string> AllowedRoles { get; set; } = [];

    /// <summary>دامنه‌ی اعمال.</summary>
    public ConfigurationScope Scope { get; set; } = ConfigurationScope.System;

    /// <summary>شناسه‌ی سازمان در صورت سازمانی‌بودن دامنه.</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>تاریخ انقضای اختیاری فعال‌سازی (UTC).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>کاربری که آخرین بار تغییر داده.</summary>
    public Guid? LastModifiedByUserId { get; set; }

    /// <summary>نام نمایشی آخرین تغییردهنده (snapshot).</summary>
    public string? LastModifiedByUserName { get; set; }

    // --- محاسبات ----------------------------------------------------------------

    /// <summary>آیا پرچم فعال است؟ تاریخ انقضا بررسی می‌شود.</summary>
    public bool IsEnabled => State == FeatureFlagState.On
        && (ExpiresAt is null || ExpiresAt > DateTime.UtcNow);

    // --- چرخه‌ی عمر -------------------------------------------------------------

    public void TurnOn(Guid? modifiedByUserId, string? modifiedByUserName)
    {
        State = FeatureFlagState.On;
        Percentage = 100;
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedByUserName = modifiedByUserName;
    }

    public void TurnOff(Guid? modifiedByUserId, string? modifiedByUserName)
    {
        State = FeatureFlagState.Off;
        Percentage = 0;
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedByUserName = modifiedByUserName;
    }

    public void SetPercentage(int percentage, Guid? modifiedByUserId, string? modifiedByUserName)
    {
        State = FeatureFlagState.Percentage;
        Percentage = Math.Clamp(percentage, 0, 100);
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedByUserName = modifiedByUserName;
    }

    public void SetAllowList(IReadOnlyList<string> userIds, IReadOnlyList<string> roles, Guid? modifiedByUserId, string? modifiedByUserName)
    {
        State = FeatureFlagState.AllowList;
        AllowedUserIds = userIds.ToList();
        AllowedRoles = roles.ToList();
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedByUserName = modifiedByUserName;
    }

    // --- رویدادهای دامنه --------------------------------------------------------

    public void RaiseChangedEvent()
    {
        RaiseDomainEvent(new FeatureFlagChangedEvent(Id, Key, Name, State, Percentage, Scope, LastModifiedByUserId));
    }
}

/// <summary>
/// یک سیاست سیستمی قابل پیکربندی (رمز عبور، نشست، حریم خصوصی، نگهداری داده).
/// سیاست‌ها به‌صورت کلید/مقدار درون یک گروه سیاست ذخیره می‌شوند.
/// </summary>
public sealed class SystemPolicy : BaseEntity
{
    /// <summary>نوع سیاست.</summary>
    public SystemPolicyType Type { get; set; } = SystemPolicyType.Custom;

    /// <summary>کلید یکتای سیاست (مثلاً «password.minLength»).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>نام نمایشی.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>توضیح.</summary>
    public string? Description { get; set; }

    /// <summary>مقدار سیاست (رشته؛ تفسیر بر اساس کلید در سرویس).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>مقدار پیش‌فرض توصیه‌شده.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>آیا فعال است؟</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>کاربری که آخرین بار تغییر داده.</summary>
    public Guid? LastModifiedByUserId { get; set; }

    /// <summary>نام نمایشی آخرین تغییردهنده (snapshot).</summary>
    public string? LastModifiedByUserName { get; set; }

    // --- چرخه‌ی عمر -------------------------------------------------------------

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void SetValue(string value, Guid? modifiedByUserId, string? modifiedByUserName)
    {
        Value = value;
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedByUserName = modifiedByUserName;
    }

    // --- رویدادهای دامنه --------------------------------------------------------

    public void RaiseChangedEvent()
    {
        RaiseDomainEvent(new SystemPolicyChangedEvent(Id, Type, Key, Value, IsEnabled, LastModifiedByUserId));
    }
}
