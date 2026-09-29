using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Workflow.Abstractions;
using ODCC.Application.Modules.Workflow.Dtos;

namespace ODCC.Api.Controllers.Workflow;

/// <summary>
/// مدیریت نمونه‌های گردش کار: شروع، گذار و لغو.
///
/// <b>مجوزها:</b> خواندن نیازمند <c>workflows.view</c> است. شروع و گذار
/// نمونه‌ها معمولاً توسط سرویس‌های کاربردی ماژول‌های دیگر (با مجوزهای خودشان)
/// انجام می‌شود، ولی این کنترلر نیز برای مدیریت دستی در دسترس است.
/// </summary>
[ApiController]
[Route("api/{culture:language}/workflow-instances")]
public sealed class WorkflowInstancesController(IWorkflowService workflowService) : ControllerBase
{
    private readonly IWorkflowService _workflowService = workflowService;

    /// <summary>جستجوی نمونه‌های گردش کار.</summary>
    [HttpGet]
    [HasPermission(Permissions.Workflows.View)]
    [ProducesResponseType(typeof(PagedResult<WorkflowInstanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<WorkflowInstanceDto>>> Search(
        [FromQuery] WorkflowInstanceSearchRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.SearchInstancesAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک نمونه با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Workflows.View)]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowInstanceDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _workflowService.GetInstanceByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "نمونه‌ی گردش کار یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>شروع یک نمونه‌ی جدید از یک تعریف فعال.</summary>
    [HttpPost]
    [HasPermission(Permissions.Workflows.Manage)]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowInstanceDto>> Start(
        [FromBody] StartWorkflowInstanceRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.StartInstanceAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "شروع نمونه‌ی گردش کار ناموفق بود",
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

    /// <summary>انجام یک گذار روی نمونه (یا ایجاد درخواست تأیید).</summary>
    [HttpPost("{id:guid}/transition")]
    [HasPermission(Permissions.Workflows.Manage)]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowInstanceDto>> Transition(
        Guid id,
        [FromBody] TransitionWorkflowInstanceRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.TransitionInstanceAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "workflow_instance_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "نمونه‌ی گردش کار یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "انجام گذار ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>لغو یک نمونه (و درخواست تأیید باز آن).</summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Workflows.Manage)]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowInstanceDto>> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _workflowService.CancelInstanceAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "workflow_instance_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "نمونه‌ی گردش کار یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "لغو نمونه ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}
