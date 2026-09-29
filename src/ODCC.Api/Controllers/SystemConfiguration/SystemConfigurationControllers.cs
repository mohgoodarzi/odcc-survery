using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.SystemConfiguration.Abstractions;
using ODCC.Application.Modules.SystemConfiguration.Dtos;
using ODCC.Domain.Modules.SystemConfiguration.Enums;

namespace ODCC.Api.Controllers.SystemConfiguration;

/// <summary>
/// مدیریت پیکربندی سامانه: تنظیمات، پرچم‌های ویژگی و سیاست‌های سیستمی.
///
/// <b>مجوزها:</b> مشاهده نیازمند <c>system.view</c> و تغییر نیازمند
/// <c>system.manage</c> است. مقادیر تنظیمات حساس هرگز برگردانده نمی‌شوند.
/// </summary>
[ApiController]
[Route("api/{culture:language}/system/settings")]
public sealed class SettingsController(ISystemConfigurationService configurationService) : ControllerBase
{
    private readonly ISystemConfigurationService _configurationService = configurationService;

    /// <summary>جستجوی تنظیمات سامانه.</summary>
    [HttpGet]
    [HasPermission(Permissions.System.View)]
    [ProducesResponseType(typeof(PagedResult<SettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SettingDto>>> Search(
        [FromQuery] SettingSearchRequest request, CancellationToken ct)
    {
        var result = await _configurationService.SearchSettingsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک تنظیم با کلید.</summary>
    [HttpGet("{key}")]
    [HasPermission(Permissions.System.View)]
    [ProducesResponseType(typeof(SettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingDto>> GetByKey(string key, CancellationToken ct)
    {
        var result = await _configurationService.GetSettingByKeyAsync(key, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "تنظیم یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد یک تنظیم جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.System.Manage)]
    [EnableRateLimiting("Critical")]
    [ProducesResponseType(typeof(SettingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SettingDto>> Create(
        [FromBody] CreateSettingRequest request, CancellationToken ct)
    {
        var result = await _configurationService.CreateSettingAsync(request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "setting_exists")
            {
                return Conflict(new ProblemDetails
                {
                    Title = "تنظیم تکراری",
                    Status = StatusCodes.Status409Conflict,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد تنظیم ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetByKey),
            new { key = result.GetValueOrThrow().Key, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>به‌روزرسانی مقدار یک تنظیم.</summary>
    [HttpPut("{key}")]
    [HasPermission(Permissions.System.Manage)]
    [ProducesResponseType(typeof(SettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingDto>> Update(
        string key, [FromBody] UpdateSettingRequest request, CancellationToken ct)
    {
        var result = await _configurationService.UpdateSettingAsync(key, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "setting_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "تنظیم یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "به‌روزرسانی تنظیم ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}

/// <summary>
/// مدیریت پرچم‌های ویژگی (Feature Flags).
/// </summary>
[ApiController]
[Route("api/{culture:language}/system/feature-flags")]
public sealed class FeatureFlagsController(ISystemConfigurationService configurationService) : ControllerBase
{
    private readonly ISystemConfigurationService _configurationService = configurationService;

    /// <summary>جستجوی پرچم‌های ویژگی.</summary>
    [HttpGet]
    [HasPermission(Permissions.System.View)]
    [ProducesResponseType(typeof(PagedResult<FeatureFlagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FeatureFlagDto>>> Search(
        [FromQuery] FeatureFlagSearchRequest request, CancellationToken ct)
    {
        var result = await _configurationService.SearchFeatureFlagsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک پرچم با کلید.</summary>
    [HttpGet("{key}")]
    [HasPermission(Permissions.System.View)]
    [ProducesResponseType(typeof(FeatureFlagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagDto>> GetByKey(string key, CancellationToken ct)
    {
        var result = await _configurationService.GetFeatureFlagByKeyAsync(key, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "پرچم ویژگی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>بررسی فعال‌بودن پرچم برای کاربر جاری.</summary>
    [HttpGet("{key}/enabled")]
    [HasPermission(Permissions.System.View)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> IsEnabled(string key, CancellationToken ct)
    {
        var result = await _configurationService.IsFeatureEnabledAsync(key, ct);
        return Ok(result.Value);
    }

    /// <summary>فعال‌کردن یک پرچم ویژگی.</summary>
    [HttpPost("{key}/turn-on")]
    [HasPermission(Permissions.System.Manage)]
    [ProducesResponseType(typeof(FeatureFlagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagDto>> TurnOn(string key, CancellationToken ct)
    {
        var result = await _configurationService.TurnFeatureOnAsync(key, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "پرچم ویژگی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>غیرفعال‌کردن یک پرچم ویژگی.</summary>
    [HttpPost("{key}/turn-off")]
    [HasPermission(Permissions.System.Manage)]
    [ProducesResponseType(typeof(FeatureFlagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagDto>> TurnOff(string key, CancellationToken ct)
    {
        var result = await _configurationService.TurnFeatureOffAsync(key, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "پرچم ویژگی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد یک پرچم ویژگی جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.System.Manage)]
    [EnableRateLimiting("Critical")]
    [ProducesResponseType(typeof(FeatureFlagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeatureFlagDto>> Create(
        [FromBody] CreateFeatureFlagRequest request, CancellationToken ct)
    {
        var result = await _configurationService.CreateFeatureFlagAsync(request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "feature_flag_exists")
            {
                return Conflict(new ProblemDetails
                {
                    Title = "پرچم تکراری",
                    Status = StatusCodes.Status409Conflict,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد پرچم ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetByKey),
            new { key = result.GetValueOrThrow().Key, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>به‌روزرسانی پیکربندی کامل یک پرچم (حالت، درصد، فهرست مجاز، انقضا).</summary>
    [HttpPut("{key}")]
    [HasPermission(Permissions.System.Manage)]
    [ProducesResponseType(typeof(FeatureFlagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeatureFlagDto>> Update(
        string key, [FromBody] UpdateFeatureFlagRequest request, CancellationToken ct)
    {
        var result = await _configurationService.UpdateFeatureFlagAsync(key, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "feature_flag_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "پرچم ویژگی یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "به‌روزرسانی پرچم ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}

/// <summary>
/// مدیریت سیاست‌های سیستمی (رمز عبور، نشست، حریم خصوصی، نگهداری داده).
/// </summary>
[ApiController]
[Route("api/{culture:language}/system/policies")]
public sealed class SystemPoliciesController(ISystemConfigurationService configurationService) : ControllerBase
{
    private readonly ISystemConfigurationService _configurationService = configurationService;

    /// <summary>جستجوی سیاست‌های سیستمی.</summary>
    [HttpGet]
    [HasPermission(Permissions.System.View)]
    [ProducesResponseType(typeof(PagedResult<SystemPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SystemPolicyDto>>> Search(
        [FromQuery] SystemPolicySearchRequest request, CancellationToken ct)
    {
        var result = await _configurationService.SearchPoliciesAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک سیاست با نوع و کلید.</summary>
    [HttpGet("{type:int}/{key}")]
    [HasPermission(Permissions.System.View)]
    [ProducesResponseType(typeof(SystemPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemPolicyDto>> GetByTypeAndKey(SystemPolicyType type, string key, CancellationToken ct)
    {
        var result = await _configurationService.GetPolicyByTypeAndKeyAsync(type, key, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "سیاست یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد یک سیاست سیستمی جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.System.Manage)]
    [EnableRateLimiting("Critical")]
    [ProducesResponseType(typeof(SystemPolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SystemPolicyDto>> Create(
        [FromBody] CreateSystemPolicyRequest request, CancellationToken ct)
    {
        var result = await _configurationService.CreatePolicyAsync(request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "policy_exists")
            {
                return Conflict(new ProblemDetails
                {
                    Title = "سیاست تکراری",
                    Status = StatusCodes.Status409Conflict,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد سیاست ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetByTypeAndKey),
            new { type = (int)result.GetValueOrThrow().Type, key = result.GetValueOrThrow().Key, culture = RouteData.Values["culture"] },
            result.GetValueOrThrow());
    }

    /// <summary>به‌روزرسانی یک سیاست سیستمی.</summary>
    [HttpPut("{type:int}/{key}")]
    [HasPermission(Permissions.System.Manage)]
    [ProducesResponseType(typeof(SystemPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SystemPolicyDto>> Update(
        SystemPolicyType type, string key, [FromBody] UpdateSystemPolicyRequest request, CancellationToken ct)
    {
        var result = await _configurationService.UpdatePolicyAsync(type, key, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "policy_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "سیاست یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "به‌روزرسانی سیاست ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}

/// <summary>آمار پیکربندی سامانه.</summary>
[ApiController]
[Route("api/{culture:language}/system/configuration")]
public sealed class SystemConfigurationController(ISystemConfigurationService configurationService) : ControllerBase
{
    private readonly ISystemConfigurationService _configurationService = configurationService;

    /// <summary>آمار کلی پیکربندی سامانه.</summary>
    [HttpGet("stats")]
    [HasPermission(Permissions.System.View)]
    [EnableRateLimiting("Critical")]
    [OutputCache(PolicyName = "Dashboard")]
    [ProducesResponseType(typeof(SystemConfigurationStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemConfigurationStatsDto>> GetStats(CancellationToken ct)
    {
        var result = await _configurationService.GetStatsAsync(ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "دریافت آمار پیکربندی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}
