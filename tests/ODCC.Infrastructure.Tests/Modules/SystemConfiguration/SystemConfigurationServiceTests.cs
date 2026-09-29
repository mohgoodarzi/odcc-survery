using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.SystemConfiguration.Abstractions;
using ODCC.Application.Modules.SystemConfiguration.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.SystemConfiguration.Entities;
using ODCC.Domain.Modules.SystemConfiguration.Enums;
using ODCC.Infrastructure.Modules.SystemConfiguration.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.SystemConfiguration;

/// <summary>
/// آزمون‌های ماژول پیکربندی سامانه: تنظیمات، پرچم‌های ویژگی و سیاست‌های سیستمی.
///
/// پوشش:
/// - امنیت: تنظیمات حساس هرگز مقدارشان در DTO یا ممیزی ظاهر نمی‌شود.
/// - اعتبارسنجی: مقدار باید با نوع تعریف‌شده سازگار باشد.
/// - fail-closed: پرچم تعریف‌نشده یا منقضی‌شده غیرفعال است.
/// - کش: پس از تغییر، مقدار تازه خوانده می‌شود (invalidation).
/// - ممیزی: هر تغییر یک رخداد ممیزی ثبت می‌کند.
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند.
/// </summary>
public class SystemConfigurationServiceTests
{
    private static readonly string[] ManagePermissions = [Permissions.System.View, Permissions.System.Manage];

    // --- تنظیمات ---------------------------------------------------------------

    [Fact]
    public async Task CreateSetting_WithValidValue_SucceedsAndLogsAudit()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.CreateSettingAsync(new CreateSettingRequest
        {
            Key = "analytics.min_responses",
            Name = "حداقل تعداد پاسخ",
            ValueType = SettingValueType.WholeNumber,
            Value = "30",
            DefaultValue = "10",
            Group = "analytics"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Key.Should().Be("analytics.min_responses");
        result.Value.Value.Should().Be("30");
        result.Value.DefaultValue.Should().Be("10");
        result.Value.IsReadOnly.Should().BeFalse();
        result.Value.LastModifiedByUserId.Should().Be(userId);

        var row = await env.SystemConfigurationDbContext.Settings.SingleAsync();
        row.Value.Should().Be("30");
        row.Key.Should().Be("analytics.min_responses");

        (await AuditActionsAsync(env)).Should().Contain(a => a.EntityType == "system_setting");
    }

    [Fact]
    public async Task CreateSetting_DuplicateKey_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateSettingAsync(NewSettingRequest(key: "duplicate.key"));

        var second = await service.CreateSettingAsync(NewSettingRequest(key: "duplicate.key"));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("setting_exists");

        (await env.SystemConfigurationDbContext.Settings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateSetting_WithValueIncompatibleWithType_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.CreateSettingAsync(NewSettingRequest(key: "bad.number") with
        {
            ValueType = SettingValueType.WholeNumber,
            Value = "not-a-number"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("setting_invalid_value");

        (await env.SystemConfigurationDbContext.Settings.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(SettingValueType.WholeNumber, "42", true)]
    [InlineData(SettingValueType.WholeNumber, "4.2", false)]
    [InlineData(SettingValueType.FractionalNumber, "4.2", true)]
    [InlineData(SettingValueType.TrueFalse, "true", true)]
    [InlineData(SettingValueType.TrueFalse, "maybe", false)]
    [InlineData(SettingValueType.Email, "admin@test.local", true)]
    [InlineData(SettingValueType.Email, "not-an-email", false)]
    [InlineData(SettingValueType.Url, "https://example.test", true)]
    [InlineData(SettingValueType.Url, "example", false)]
    [InlineData(SettingValueType.Duration, "00:30:00", true)]
    [InlineData(SettingValueType.Duration, "soon", false)]
    [InlineData(SettingValueType.Json, """{"a":1}""", true)]
    [InlineData(SettingValueType.Json, "not-json", false)]
    public async Task ValueTypeValidation_AcceptsOrRejectsAccordingToType(SettingValueType type, string value, bool expected)
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.CreateSettingAsync(NewSettingRequest(key: $"validation.{type}") with
        {
            ValueType = type,
            Value = value
        });

        result.IsSuccess.Should().Be(expected, $"مقدار «{value}» برای نوع {type} باید {(expected ? "پذیرفته" : "رد")} شود");
    }

    [Fact]
    public async Task GetSettingByKey_UnknownKey_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.GetSettingByKeyAsync("no.such.key");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("setting_not_found");
    }

    [Fact]
    public async Task GetSettingValue_UsesValueThenDefault()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateSettingAsync(NewSettingRequest(key: "with.value") with { Value = "actual", DefaultValue = "fallback" });
        await service.CreateSettingAsync(NewSettingRequest(key: "has.default") with { Value = "explicit", DefaultValue = "fallback" });

        var actual = await service.GetSettingValueAsync("with.value");
        actual.IsSuccess.Should().BeTrue();
        actual.Value.Should().Be("actual");

        // مقدار خالی → مقدار پیش‌فرض ذخیره‌شده.
        var row = await env.SystemConfigurationDbContext.Settings.SingleAsync(s => s.Key == "has.default");
        row.Value = "   ";
        await env.SystemConfigurationDbContext.SaveChangesAsync();

        var fallback = await service.GetSettingValueAsync("has.default");
        fallback.Value.Should().Be("fallback");

        // کلید ناموجود → پیش‌فرض ارسالی.
        var missing = await service.GetSettingValueAsync("missing", "sentinel");
        missing.Value.Should().Be("sentinel");
    }

    [Fact]
    public async Task GetSettingValue_EmptyKey_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.GetSettingValueAsync("   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("setting_invalid_key");
    }

    [Fact]
    public async Task UpdateSetting_NotFound_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.UpdateSettingAsync("no.such.key", new UpdateSettingRequest { Value = "x" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("setting_not_found");
    }

    [Fact]
    public async Task UpdateSetting_ReadOnlySetting_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateSettingAsync(NewSettingRequest(key: "readonly.key"));

        // فقط از طریق کد قابل تغییر است.
        var row = await env.SystemConfigurationDbContext.Settings.SingleAsync();
        row.IsReadOnly = true;
        await env.SystemConfigurationDbContext.SaveChangesAsync();

        var result = await service.UpdateSettingAsync("readonly.key", new UpdateSettingRequest { Value = "changed" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("setting_read_only");

        // مقدار دست‌نخورده است.
        (await env.SystemConfigurationDbContext.Settings.AsNoTracking().SingleAsync()).Value.Should().Be("initial");
    }

    [Fact]
    public async Task UpdateSetting_WithIncompatibleValue_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateSettingAsync(NewSettingRequest(key: "numeric.key") with
        {
            ValueType = SettingValueType.WholeNumber,
            Value = "10"
        });

        var result = await service.UpdateSettingAsync("numeric.key", new UpdateSettingRequest { Value = "ten" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("setting_invalid_value");
    }

    [Fact]
    public async Task UpdateSetting_PersistsNewValue_WithAuditTrail()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateSettingAsync(NewSettingRequest(key: "plain.key") with { Value = "old" });

        var result = await service.UpdateSettingAsync("plain.key", new UpdateSettingRequest { Value = "new" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("new");
        result.Value.LastModifiedByUserId.Should().Be(userId);

        (await env.SystemConfigurationDbContext.Settings.AsNoTracking().SingleAsync()).Value.Should().Be("new");

        // ممیزی: توضیح شامل مقدار قدیم و جدید است (تنظیم غیرحساس).
        var audit = await AuditActionsAsync(env);
        audit.Should().Contain(a => a.EntityType == "system_setting" && a.Description != null && a.Description.Contains("old"));
    }

    [Fact]
    public async Task UpdateSetting_OnSensitiveSetting_NeverExposesValue()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var created = await service.CreateSettingAsync(NewSettingRequest(key: "secret.key") with
        {
            Value = "top-secret-value",
            IsSensitive = true
        });

        // DTO فقط اعلام «دارای مقدار» می‌کند — خود مقدار نه.
        created.Value!.Value.Should().BeNull();
        created.Value.DefaultValue.Should().BeNull();
        created.Value.HasValue.Should().BeTrue();
        created.Value.IsSensitive.Should().BeTrue();

        // در پایگاه داده ذخیره می‌شود، اما فقط نام منطقی در خروجی‌هاست.
        var row = await env.SystemConfigurationDbContext.Settings.SingleAsync();
        row.Value.Should().Be("top-secret-value");

        var updated = await service.UpdateSettingAsync("secret.key", new UpdateSettingRequest { Value = "rotated-value" });

        updated.Value!.Value.Should().BeNull();
        updated.Value.HasValue.Should().BeTrue();
        (await env.SystemConfigurationDbContext.Settings.AsNoTracking().SingleAsync()).Value.Should().Be("rotated-value");

        // ممیزی هم نباید مقدار را فاش کند.
        var json = System.Text.Json.JsonSerializer.Serialize(updated.Value);
        json.Should().NotContain("rotated-value");

        var audit = await AuditActionsAsync(env);
        audit.Should().NotContain(a => a.Description != null && a.Description.Contains("rotated-value"));
        audit.Should().NotContain(a => a.Description != null && a.Description.Contains("top-secret-value"));
    }

    [Fact]
    public async Task SearchSettings_FiltersByGroupAndSensitive()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        await service.CreateSettingAsync(NewSettingRequest(key: "g1.first") with { Group = "group-a" });
        await service.CreateSettingAsync(NewSettingRequest(key: "g1.second") with { Group = "group-a" });
        await service.CreateSettingAsync(NewSettingRequest(key: "g2.third") with { Group = "group-b", IsSensitive = true });

        var byGroup = await service.SearchSettingsAsync(new SettingSearchRequest { Group = "group-a" });
        byGroup.TotalCount.Should().Be(2);
        byGroup.Items.Should().OnlyContain(s => s.Group == "group-a");

        var sensitive = await service.SearchSettingsAsync(new SettingSearchRequest { IsSensitive = true });
        sensitive.TotalCount.Should().Be(1);
        sensitive.Items.Single().Key.Should().Be("g2.third");

        var byText = await service.SearchSettingsAsync(new SettingSearchRequest { SearchText = "g2" });
        byText.TotalCount.Should().Be(1);
    }

    // --- پرچم‌های ویژگی ---------------------------------------------------------

    [Fact]
    public async Task CreateFeatureFlag_WithStateOn_IsEnabled()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest
        {
            Key = "new-dashboard",
            Name = "داشبورد جدید",
            State = FeatureFlagState.On
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be(FeatureFlagState.On);
        result.Value.IsEnabled.Should().BeTrue();

        var enabled = await service.IsFeatureEnabledAsync("new-dashboard");
        enabled.IsSuccess.Should().BeTrue();
        enabled.Value.Should().BeTrue();
    }

    [Fact]
    public async Task IsFeatureEnabled_UndefinedFlag_ReturnsFalse_FailClosed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.IsFeatureEnabledAsync("never-defined");

        result.IsSuccess.Should().BeTrue();
        // پرچم تعریف‌نشده = غیرفعال.
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task IsFeatureEnabled_ExpiredFlag_ReturnsFalse()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest
        {
            Key = "expired-flag",
            Name = "منقضی‌شده",
            State = FeatureFlagState.On,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });

        var result = await service.IsFeatureEnabledAsync("expired-flag");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task IsFeatureEnabled_EmptyKey_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.IsFeatureEnabledAsync("   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("feature_flag_invalid_key");
    }

    [Fact]
    public async Task TurnFeature_OnOff_TogglesState()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "toggle-me", Name = "نمونه" });

        var on = await service.TurnFeatureOnAsync("toggle-me");
        on.IsSuccess.Should().BeTrue();
        on.Value!.State.Should().Be(FeatureFlagState.On);
        (await service.IsFeatureEnabledAsync("toggle-me")).Value.Should().BeTrue();

        var off = await service.TurnFeatureOffAsync("toggle-me");
        off.IsSuccess.Should().BeTrue();
        off.Value!.State.Should().Be(FeatureFlagState.Off);
        (await service.IsFeatureEnabledAsync("toggle-me")).Value.Should().BeFalse();
    }

    [Fact]
    public async Task TurnFeature_OnUnknownKey_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.TurnFeatureOnAsync("no-such-flag");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("feature_flag_not_found");
    }

    [Fact]
    public async Task UpdateFeatureFlag_ToPercentageWithoutPercentage_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "percent-flag", Name = "نمونه" });

        var result = await service.UpdateFeatureFlagAsync("percent-flag", new UpdateFeatureFlagRequest
        {
            State = FeatureFlagState.Percentage
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("feature_flag_percentage_required");
    }

    [Fact]
    public async Task UpdateFeatureFlag_ToPercentageWithFullPercentage_IsEnabledForEveryone()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "percent-flag", Name = "نمونه" });

        var result = await service.UpdateFeatureFlagAsync("percent-flag", new UpdateFeatureFlagRequest
        {
            State = FeatureFlagState.Percentage,
            Percentage = 100
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Percentage.Should().Be(100);

        // ۱۰۰٪ یعنی همه — نتیجه نباید تصادفی باشد.
        (await service.IsFeatureEnabledAsync("percent-flag")).Value.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateFeatureFlag_ToAllowList_OnlyListedUserIsEnabled()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (current, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "allowlist-flag", Name = "نمونه" });

        var result = await service.UpdateFeatureFlagAsync("allowlist-flag", new UpdateFeatureFlagRequest
        {
            State = FeatureFlagState.AllowList,
            AllowedUserIds = [userId.ToString()]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.AllowedUserIds.Should().Contain(userId.ToString());

        // کاربرِ فهرست‌شده فعال است.
        (await service.IsFeatureEnabledAsync("allowlist-flag")).Value.Should().BeTrue();

        // کاربر دیگر فعال نیست.
        current.UserId = Guid.NewGuid();
        (await service.IsFeatureEnabledAsync("allowlist-flag")).Value.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateFeatureFlag_ToRoleAllowList_OnlyListedRoleIsEnabled()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (current, _) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "role-flag", Name = "نمونه" });

        await service.UpdateFeatureFlagAsync("role-flag", new UpdateFeatureFlagRequest
        {
            State = FeatureFlagState.AllowList,
            AllowedRoles = ["manager"]
        });

        current.Roles = ["manager"];
        (await service.IsFeatureEnabledAsync("role-flag")).Value.Should().BeTrue();

        current.Roles = ["employee"];
        (await service.IsFeatureEnabledAsync("role-flag")).Value.Should().BeFalse();
    }

    [Fact]
    public async Task CreateFeatureFlag_DuplicateKey_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "dupe-flag", Name = "نمونه" });

        var second = await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "dupe-flag", Name = "نمونه" });

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("feature_flag_exists");
    }

    [Fact]
    public async Task UpdateFeatureFlag_OnUnknownKey_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.UpdateFeatureFlagAsync("no-such-flag", new UpdateFeatureFlagRequest { State = FeatureFlagState.On });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("feature_flag_not_found");
    }

    [Fact]
    public async Task FeatureFlag_Lifecycle_LogsAuditEntries()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "audited-flag", Name = "نمونه" });

        await service.TurnFeatureOnAsync("audited-flag");

        var audit = await AuditActionsAsync(env);
        audit.Should().Contain(a => a.EntityType == "feature_flag" && a.Action == "update");
    }

    // --- سیاست‌های سیستمی -------------------------------------------------------

    [Fact]
    public async Task CreatePolicy_SucceedsAndLogsAudit()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.CreatePolicyAsync(new CreateSystemPolicyRequest
        {
            Type = SystemPolicyType.Password,
            Key = "password.minLength",
            Name = "حداقل طول رمز عبور",
            Value = "12",
            DefaultValue = "8",
            IsEnabled = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("12");
        result.Value.IsEnabled.Should().BeTrue();
        result.Value.LastModifiedByUserId.Should().Be(userId);

        (await AuditActionsAsync(env)).Should().Contain(a => a.EntityType == "system_policy");
    }

    [Fact]
    public async Task CreatePolicy_DuplicateTypeAndKey_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreatePolicyAsync(NewPolicyRequest(key: "password.minLength"));

        var second = await service.CreatePolicyAsync(NewPolicyRequest(key: "password.minLength"));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("policy_exists");

        // همین کلید در نوع دیگر مجاز است.
        var otherType = await service.CreatePolicyAsync(NewPolicyRequest(key: "password.minLength") with { Type = SystemPolicyType.Session });
        otherType.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetPolicyByTypeAndKey_NotFound_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.GetPolicyByTypeAndKeyAsync(SystemPolicyType.Password, "no.such.key");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("policy_not_found");
    }

    [Fact]
    public async Task UpdatePolicy_NotFound_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        var result = await service.UpdatePolicyAsync(SystemPolicyType.Password, "no.such.key",
            new UpdateSystemPolicyRequest { Value = "20", IsEnabled = true });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("policy_not_found");
    }

    [Fact]
    public async Task UpdatePolicy_ChangesValueAndEnabledState()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        await service.CreatePolicyAsync(NewPolicyRequest(key: "password.maxLength"));

        var result = await service.UpdatePolicyAsync(SystemPolicyType.Password, "password.maxLength",
            new UpdateSystemPolicyRequest { Value = "128", IsEnabled = false });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("128");
        result.Value.IsEnabled.Should().BeFalse();

        var row = await env.SystemConfigurationDbContext.SystemPolicies.AsNoTracking().SingleAsync();
        row.Value.Should().Be("128");
        row.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SearchPolicies_FiltersByTypeAndEnabled()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        await service.CreatePolicyAsync(NewPolicyRequest(key: "session.timeout") with { Type = SystemPolicyType.Session });
        await service.CreatePolicyAsync(NewPolicyRequest(key: "retention.days") with { Type = SystemPolicyType.DataRetention });

        var byType = await service.SearchPoliciesAsync(new SystemPolicySearchRequest { Type = SystemPolicyType.Session });
        byType.TotalCount.Should().Be(1);
        byType.Items.Single().Key.Should().Be("session.timeout");

        var enabled = await service.SearchPoliciesAsync(new SystemPolicySearchRequest { IsEnabled = true });
        enabled.TotalCount.Should().Be(2);
    }

    // --- کش ----------------------------------------------------------------------

    [Fact]
    public async Task CachedSettingService_ReadsValueAndInvalidatesAfterChange()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        var cached = env.Services.GetRequiredService<ISettingService>();

        await service.CreateSettingAsync(NewSettingRequest(key: "cached.key") with { Value = "first" });

        (await cached.GetValueAsync("cached.key")).Should().Be("first");
        (await cached.GetBooleanAsync("cached.key")).Should().BeFalse();

        await service.UpdateSettingAsync("cached.key", new UpdateSettingRequest { Value = "second" });

        // شنونده‌ی ممیزی کش را نامعتبر کرد → مقدار تازه.
        (await cached.GetValueAsync("cached.key")).Should().Be("second");
    }

    [Fact]
    public async Task CachedSettingService_UndefinedKey_ReturnsDefault_WithoutThrowing()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var cached = env.Services.GetRequiredService<ISettingService>();

        (await cached.GetValueAsync("undefined.key", "fallback")).Should().Be("fallback");
        (await cached.GetBooleanAsync("undefined.key", true)).Should().BeTrue();
        (await cached.GetIntegerAsync("undefined.key", 42)).Should().Be(42);
    }

    [Fact]
    public async Task CachedSettingService_ParsesTypedValues()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        var cached = env.Services.GetRequiredService<ISettingService>();

        await service.CreateSettingAsync(NewSettingRequest(key: "typed.flag") with
        {
            ValueType = SettingValueType.TrueFalse,
            Value = "true"
        });
        await service.CreateSettingAsync(NewSettingRequest(key: "typed.number") with
        {
            ValueType = SettingValueType.WholeNumber,
            Value = "77"
        });

        (await cached.GetBooleanAsync("typed.flag")).Should().BeTrue();
        (await cached.GetIntegerAsync("typed.number")).Should().Be(77);
        // مقدار غیرقابل‌تجزیه → پیش‌فرض.
        (await cached.GetIntegerAsync("typed.flag", 5)).Should().Be(5);
    }

    [Fact]
    public async Task CachedFeatureFlagService_IsEnabled_FollowsStateAndCacheInvalidation()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        var cached = env.Services.GetRequiredService<IFeatureFlagService>();

        // پرچم تعریف‌نشده = غیرفعال.
        (await cached.IsEnabledAsync("undefined-flag")).Should().BeFalse();

        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "cached-flag", Name = "نمونه" });
        (await cached.IsEnabledAsync("cached-flag")).Should().BeFalse();

        // تغییر از طریق سرویس کش را نامعتبر می‌کند.
        await service.TurnFeatureOnAsync("cached-flag");
        (await cached.IsEnabledAsync("cached-flag")).Should().BeTrue();
    }

    [Fact]
    public async Task CachedFeatureFlagService_PercentageAndAllowList_ResolveForCaller()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (current, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();
        var cached = env.Services.GetRequiredService<IFeatureFlagService>();

        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest
        {
            Key = "percent-flag",
            Name = "نمونه",
            State = FeatureFlagState.Percentage,
            Percentage = 100
        });
        (await cached.IsEnabledAsync("percent-flag")).Should().BeTrue();

        await service.UpdateFeatureFlagAsync("percent-flag", new UpdateFeatureFlagRequest
        {
            State = FeatureFlagState.AllowList,
            AllowedUserIds = [userId.ToString()]
        });

        // پارامتر صریح کاربر.
        (await cached.IsEnabledAsync("percent-flag", userId)).Should().BeTrue();
        (await cached.IsEnabledAsync("percent-flag", Guid.NewGuid())).Should().BeFalse();

        // کاربر جاری (بدون پارامتر صریح).
        (await cached.IsEnabledAsync("percent-flag")).Should().BeTrue();
        current.UserId = Guid.NewGuid();
        (await cached.IsEnabledAsync("percent-flag")).Should().BeFalse();
    }

    // --- آمار --------------------------------------------------------------------

    [Fact]
    public async Task GetStats_ReflectsState()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<ISystemConfigurationService>();

        await service.CreateSettingAsync(NewSettingRequest(key: "stats.plain"));
        await service.CreateSettingAsync(NewSettingRequest(key: "stats.secret") with { IsSensitive = true });
        await service.CreateFeatureFlagAsync(new CreateFeatureFlagRequest { Key = "stats.flag", Name = "نمونه", State = FeatureFlagState.On });
        await service.CreatePolicyAsync(NewPolicyRequest(key: "stats.policy"));

        var stats = await service.GetStatsAsync();

        stats.IsSuccess.Should().BeTrue();
        stats.Value!.TotalSettings.Should().Be(2);
        stats.Value.SensitiveSettings.Should().Be(1);
        stats.Value.TotalFeatureFlags.Should().Be(1);
        stats.Value.EnabledFeatureFlags.Should().Be(1);
        stats.Value.TotalPolicies.Should().Be(1);
        stats.Value.EnabledPolicies.Should().Be(1);
    }

    // --- کمک‌ها ------------------------------------------------------------------

    private static CreateSettingRequest NewSettingRequest(string key) => new()
    {
        Key = key,
        Name = "تنظیم آزمون",
        ValueType = SettingValueType.Text,
        Value = "initial",
        Group = "test"
    };

    private static CreateSystemPolicyRequest NewPolicyRequest(string key) => new()
    {
        Type = SystemPolicyType.Password,
        Key = key,
        Name = "سیاست آزمون",
        Value = "10",
        DefaultValue = "5",
        IsEnabled = true
    };

    private static async Task<IReadOnlyList<AuditEntryDto>> AuditActionsAsync(TestEnvironment env)
    {
        var auditService = env.Services.GetRequiredService<IAuditService>();

        return await auditService.SearchAsync(new AuditSearchRequest(
            EntityType: null, Action: null, UserId: null,
            FromUtc: null, ToUtc: null, Page: 1, PageSize: 100));
    }
}
