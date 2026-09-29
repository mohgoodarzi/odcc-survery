using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Workflow.Abstractions;
using ODCC.Application.Modules.Workflow.Dtos;

namespace ODCC.Api.Controllers.Workflow;

/// <summary>
/// مدیریت تعاریف گردش کار: ساخت ماشین وضعیت، وضعیت‌ها و گذارها.
///
/// <b>مجوزها:</b> خواندن نیازمند <c>workflows.view</c> و مدیریت نیازمند
/// <c>workflows.manage</c> است.
/// </summary>
[ApiController]
[Route("api/{culture:language}/workflows")]
public sealed class WorkflowsController(IWorkflowService workflowService) : ControllerBase
{
    private readonly IWorkflowService _workflowService = workflowService;

    /// <summary>جستجوی تعاریف گردش کار.</summary>
    [HttpGet]
    [HasPermission(Permissions.Workflows.View)]
    [ProducesResponseType(typeof(PagedResult<WorkflowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<WorkflowDto>>> Search(
        [FromQuery] WorkflowSearchRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.SearchWorkflowsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>آمار گردش کار برای داشبورد.</summary>
    [HttpGet("stats")]
    [HasPermission(Permissions.Workflows.View)]
    [EnableRateLimiting("Critical")]
    [OutputCache(PolicyName = "Dashboard")]
    [ProducesResponseType(typeof(WorkflowStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkflowStatsDto>> GetStats(CancellationToken ct)
    {
        var result = await _workflowService.GetStatsAsync(ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "دریافت آمار گردش کار ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>دریافت یک تعریف گردش کار با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Workflows.View)]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _workflowService.GetWorkflowByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "تعریف گردش کار یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد تعریف گردش کار جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Workflows.Manage)]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowDto>> Create(
        [FromBody] SaveWorkflowRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.CreateWorkflowAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد گردش کار ناموفق بود",
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

    /// <summary>ویرایش یک تعریف گردش کار.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Workflows.Manage)]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowDto>> Update(
        Guid id,
        [FromBody] SaveWorkflowRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.UpdateWorkflowAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "workflow_not_found" or "workflow_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "تعریف گردش کار یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش گردش کار ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>فعال‌سازی تعریف گردش کار (انتشار).</summary>
    [HttpPost("{id:guid}/activate")]
    [HasPermission(Permissions.Workflows.Manage)]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowDto>> Activate(Guid id, CancellationToken ct)
    {
        var result = await _workflowService.ActivateWorkflowAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "workflow_not_found" or "workflow_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "تعریف گردش کار یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "فعال‌سازی گردش کار ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>بایگانی تعریف گردش کار (حذف نرم).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Workflows.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _workflowService.ArchiveWorkflowAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "workflow_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "تعریف گردش کار یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "بایگانی گردش کار ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}
