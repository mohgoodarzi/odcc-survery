using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.ActionManagement.Dtos;

namespace ODCC.Api.Controllers.ActionManagement;

/// <summary>
/// مدیریت برنامه‌های اقدام: ساخت، ویرایش، چرخه‌ی عمر و سنجش اثربخشی.
///
/// <b>مرز سازمانی:</b> کاربر فقط برنامه‌های داخل دامنه‌ی سازمانی خودش را
/// می‌بیند (fail-closed). این مرز در سرویس اعمال می‌شود، نه در کنترلر.
///
/// <b>حریم خصوصی:</b> برنامه‌ها هرگز شناسه‌ی پاسخ‌گوی یک نظرسنجی را ذخیره یا
/// برنمی‌گردانند — حتی برای نظرسنجی‌های غیرناشناس. فقط شاخص‌های تجمعی
/// (NPS/CSAT/CES) و متادیتای عمومی منتقل می‌شوند.
/// </summary>
[ApiController]
[Route("api/{culture:language}/actions/plans")]
public sealed class ActionPlansController(IActionManagementService actionManagementService) : ControllerBase
{
    private readonly IActionManagementService _actionManagementService = actionManagementService;

    /// <summary>جستجوی برنامه‌های اقدام (با احترام به دامنه‌ی سازمانی کاربر).</summary>
    [HttpGet]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(PagedResult<ActionPlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ActionPlanDto>>> Search(
        [FromQuery] ActionPlanSearchRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.SearchPlansAsync(request, ct);
        return Ok(result);
    }

    /// <summary>خلاصه‌ی آماری اقدامات برای داشبورد (با احترام به دامنه‌ی سازمانی).</summary>
    [HttpGet("stats")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(ActionStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionStatsDto>> GetStats(CancellationToken ct)
    {
        var result = await _actionManagementService.GetStatsAsync(ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "دریافت آمار اقدامات ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>دریافت یک برنامه‌ی اقدام با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(ActionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionPlanDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _actionManagementService.GetPlanByIdAsync(id, ct);

        if (result.IsFailure)
        {
            // نبودن دسترسی هم ۴۰۴ برمی‌گرداند تا وجود برنامه‌ی خارج از دامنه فاش نشود.
            return NotFound(new ProblemDetails
            {
                Title = "برنامه‌ی اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد برنامه‌ی اقدام جدید (به‌صورت پیش‌نویس یا فعال).</summary>
    [HttpPost]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionPlanDto>> Create(
        [FromBody] SaveActionPlanRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.CreatePlanAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد برنامه‌ی اقدام ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] },
            result.GetValueOrThrow());
    }

    /// <summary>ویرایش یک برنامه‌ی اقدام.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionPlanDto>> Update(
        Guid id,
        [FromBody] SaveActionPlanRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.UpdatePlanAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "action_plan_not_found" or "action_plan_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "برنامه‌ی اقدام یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش برنامه‌ی اقدام ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>فعال‌سازی برنامه (شروع پیگیری واقعی آیتم‌ها).</summary>
    [HttpPost("{id:guid}/activate")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionPlanDto>> Activate(Guid id, CancellationToken ct)
    {
        var result = await _actionManagementService.ActivatePlanAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "برنامه‌ی اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>تکمیل برنامه (فقط در حالت فعال).</summary>
    [HttpPost("{id:guid}/complete")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionPlanDto>> Complete(Guid id, CancellationToken ct)
    {
        var result = await _actionManagementService.CompletePlanAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "action_plan_not_found" or "action_plan_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "برنامه‌ی اقدام یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "تکمیل برنامه ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>لغو برنامه (در حالت پیش‌نویس یا فعال).</summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionPlanDto>> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _actionManagementService.CancelPlanAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "action_plan_not_found" or "action_plan_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "برنامه‌ی اقدام یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "لغو برنامه ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>بایگانی برنامه (حذف نرم).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _actionManagementService.ArchivePlanAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "برنامه‌ی اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }

    /// <summary>ثبت مقدار شاخصِ خروجی برنامه برای سنجش اثربخشی.</summary>
    [HttpPost("{id:guid}/outcome")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionPlanDto>> RecordOutcome(
        Guid id,
        [FromBody] RecordOutcomeRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.RecordPlanOutcomeAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "برنامه‌ی اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>افزودن یک آیتم جدید به یک برنامه.</summary>
    [HttpPost("{planId:guid}/items")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionItemDto>> CreateItem(
        Guid planId,
        [FromBody] SaveActionItemRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.CreateItemAsync(planId, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "action_plan_not_found" or "action_plan_closed")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "برنامه‌ی اقدام یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد آیتم ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(
            nameof(ActionItemsController.GetById),
            "ActionItems",
            new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] },
            result.GetValueOrThrow());
    }
}
