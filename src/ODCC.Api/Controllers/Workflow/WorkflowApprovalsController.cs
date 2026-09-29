using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Workflow.Abstractions;
using ODCC.Application.Modules.Workflow.Dtos;

namespace ODCC.Api.Controllers.Workflow;

/// <summary>
/// مدیریت درخواست‌های تأیید گردش کار.
///
/// <b>مجوزها:</b> همه‌ی کاربران احراز هویت‌شده می‌توانند درخواست‌های
/// «قابل تصمیم‌گیری توسط خودشان» را ببینند (فیلتر در سرویس اعمال می‌شود:
/// کاربر باید مجوز مشخص‌شده روی گذار را داشته باشد). مرز واقعی در سرویس است،
/// نه در کنترلر.
/// </summary>
[ApiController]
[Route("api/{culture:language}/workflow-approvals")]
public sealed class WorkflowApprovalsController(IWorkflowService workflowService) : ControllerBase
{
    private readonly IWorkflowService _workflowService = workflowService;

    /// <summary>جستجوی درخواست‌های تأیید.</summary>
    [HttpGet]
    [HasPermission(Permissions.Workflows.View)]
    [ProducesResponseType(typeof(PagedResult<WorkflowApprovalRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<WorkflowApprovalRequestDto>>> Search(
        [FromQuery] WorkflowApprovalSearchRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.SearchApprovalsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک درخواست تأیید با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Workflows.View)]
    [ProducesResponseType(typeof(WorkflowApprovalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowApprovalRequestDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _workflowService.GetApprovalByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "درخواست تأیید یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>تأیید یک درخواست. کاربر باید مجوز مشخص‌شده روی گذار را داشته باشد.</summary>
    [HttpPost("{id:guid}/approve")]
    [HasPermission(Permissions.Workflows.Approve)]
    [ProducesResponseType(typeof(WorkflowApprovalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowApprovalRequestDto>> Approve(
        Guid id,
        [FromBody] DecideWorkflowApprovalRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.ApproveAsync(id, request, ct);

        if (result.IsFailure)
        {
            // نبودن دسترسی هم ۴۰۴ برمی‌گرداند تا وجود درخواست فاش نشود.
            if (result.Error.Code is "workflow_approval_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "درخواست تأیید یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "تأیید درخواست ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>رد یک درخواست. کاربر باید مجوز مشخص‌شده روی گذار را داشته باشد.</summary>
    [HttpPost("{id:guid}/reject")]
    [HasPermission(Permissions.Workflows.Approve)]
    [ProducesResponseType(typeof(WorkflowApprovalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowApprovalRequestDto>> Reject(
        Guid id,
        [FromBody] DecideWorkflowApprovalRequest request,
        CancellationToken ct)
    {
        var result = await _workflowService.RejectAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "workflow_approval_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "درخواست تأیید یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "رد درخواست ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}
