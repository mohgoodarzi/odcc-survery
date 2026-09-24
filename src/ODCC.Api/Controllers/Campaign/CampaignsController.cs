using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;

namespace ODCC.Api.Controllers.Campaign;

/// <summary>
/// مدیریت کمپین‌ها: زمان‌بندی، هدف‌گیری مخاطب، توزیع، یادآوری‌ها و پیگیری.
/// </summary>
[ApiController]
[Route("api/{culture:language}/campaigns")]
public sealed class CampaignsController(ICampaignService campaignService) : ControllerBase
{
    private readonly ICampaignService _campaignService = campaignService;

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی کمپین‌ها.</summary>
    [HttpGet]
    [HasPermission(Permissions.Campaign.View)]
    [ProducesResponseType(typeof(PagedResult<CampaignSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CampaignSummaryDto>>> Search(
        [FromQuery] CampaignSearchRequest request,
        CancellationToken ct)
    {
        var result = await _campaignService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک کمپین با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Campaign.View)]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کمپین یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد کمپین جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CampaignDto>> Create(
        [FromBody] SaveCampaignRequest request,
        CancellationToken ct)
    {
        var result = await _campaignService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد کمپین ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>به‌روزرسانی تنظیمات کمپین (فقط در حالت پیش‌نویس).</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CampaignDto>> Update(
        Guid id,
        [FromBody] SaveCampaignRequest request,
        CancellationToken ct)
    {
        var result = await _campaignService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "campaign_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "کمپین یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش کمپین ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>زمان‌بندی کمپین برای اجرای خودکار (Draft → Scheduled).</summary>
    [HttpPost("{id:guid}/schedule")]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CampaignDto>> Schedule(Guid id, [FromBody] ScheduleCampaignRequest request, CancellationToken ct)
    {
        var result = await _campaignService.ScheduleAsync(id, request.ScheduledAt, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "campaign_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "کمپین یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "زمان‌بندی کمپین ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// اجرای کمپین: حل جمعیت هدف، ساخت ردیف‌های توزیع و گذار به «در حال اجرا».
    /// </summary>
    [HttpPost("{id:guid}/launch")]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(typeof(CampaignLaunchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CampaignLaunchResultDto>> Launch(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.LaunchAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "campaign_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "کمپین یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "اجرای کمپین ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>تکمیل کمپین (Running → Completed).</summary>
    [HttpPost("{id:guid}/complete")]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CampaignDto>> Complete(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.CompleteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "campaign_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "کمپین یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "تکمیل کمپین ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>بایگانی کمپین.</summary>
    [HttpPost("{id:guid}/archive")]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignDto>> Archive(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.ArchiveAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کمپین یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>حذف نرم کمپین (فقط در حالت پیش‌نویس).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.DeleteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "campaign_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "کمپین یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "حذف کمپین ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }

    /// <summary>فهرست صفحه‌بندی‌شده‌ی ردیف‌های توزیع یک کمپین.</summary>
    [HttpGet("{id:guid}/distributions")]
    [HasPermission(Permissions.Campaign.View)]
    [ProducesResponseType(typeof(PagedResult<DistributionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<DistributionDto>>> GetDistributions(
        Guid id,
        [FromQuery] DistributionSearchRequest request,
        CancellationToken ct)
    {
        var exists = await _campaignService.GetByIdAsync(id, ct);
        if (exists.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کمپین یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = exists.Error.Message
            });
        }

        var result = await _campaignService.GetDistributionsAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// پردازش یادآورهای سررسیده‌ی همه‌ی کمپین‌های «در حال اجرا».
    /// ارسال واقعی پیام در ماژول اعلان‌ها انجام می‌شود.
    /// </summary>
    [HttpPost("reminders:process-due")]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(typeof(ReminderProcessResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReminderProcessResultDto>> ProcessDueReminders(CancellationToken ct)
    {
        var result = await _campaignService.ProcessDueRemindersAsync(ct);
        return Ok(result.Value);
    }

    /// <summary>لغو یک یادآور برنامه‌ریزی‌شده.</summary>
    [HttpPost("{id:guid}/reminders/{reminderId:guid}:cancel")]
    [HasPermission(Permissions.Campaign.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelReminder(Guid id, Guid reminderId, CancellationToken ct)
    {
        var result = await _campaignService.CancelReminderAsync(id, reminderId, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "campaign_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "کمپین یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "لغو یادآور ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}

/// <summary>
/// درخواست زمان‌بندی یک کمپین.
/// </summary>
public sealed record ScheduleCampaignRequest
{
    /// <summary>زمان شروع برنامه‌ریزی‌شده (UTC).</summary>
    public DateTime ScheduledAt { get; init; }
}
