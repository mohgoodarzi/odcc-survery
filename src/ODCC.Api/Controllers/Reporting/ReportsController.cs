using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Application.Modules.Reporting.Dtos;
using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Api.Controllers.Reporting;

/// <summary>
/// مدیریت گزارش‌ها: تعاریف، اجرا، تاریخچه و دانلود خروجی.
///
/// **حریم خصوصی:** خروجی گزارش‌ها فقط شامل تجمع‌های تحلیلی است. هیچ شناسه‌ی
/// پاسخ‌گویی (کاربر، کارمند یا نام) در فایل‌های تولیدشده قرار نمی‌گیرد.
/// </summary>
[ApiController]
[Route("api/{culture:language}/reports")]
public sealed class ReportsController(IReportingService reportingService) : ControllerBase
{
    private readonly IReportingService _reportingService = reportingService;

    /// <summary>جستجوی تعاریف گزارش.</summary>
    [HttpGet]
    [HasPermission(Permissions.Reports.View)]
    [ProducesResponseType(typeof(PagedResult<ReportDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReportDefinitionDto>>> Search(
        [FromQuery] ReportSearchRequest request,
        CancellationToken ct)
    {
        var result = await _reportingService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک تعریف گزارش با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Reports.View)]
    [ProducesResponseType(typeof(ReportDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportDefinitionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _reportingService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "گزارش یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد تعریف گزارش جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Reports.Export)]
    [ProducesResponseType(typeof(ReportDefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReportDefinitionDto>> Create(
        [FromBody] SaveReportRequest request,
        CancellationToken ct)
    {
        var result = await _reportingService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد گزارش ناموفق بود",
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

    /// <summary>ویرایش یک تعریف گزارش.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Reports.Export)]
    [ProducesResponseType(typeof(ReportDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReportDefinitionDto>> Update(
        Guid id,
        [FromBody] SaveReportRequest request,
        CancellationToken ct)
    {
        var result = await _reportingService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "report_not_found" or "report_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "گزارش یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش گزارش ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>فعال‌سازی تعریف گزارش (اجرا در زمان‌بندی‌های رسیده).</summary>
    [HttpPost("{id:guid}/activate")]
    [HasPermission(Permissions.Reports.Export)]
    [ProducesResponseType(typeof(ReportDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportDefinitionDto>> Activate(Guid id, CancellationToken ct)
    {
        var result = await _reportingService.ActivateAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "گزارش یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>بایگانی تعریف گزارش (حذف نرم).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Reports.Export)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _reportingService.ArchiveAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "گزارش یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }

    /// <summary>
    /// اجرای فوری یک تعریف گزارش و تولید خروجی آن. ردیف اجرا پیش از شروع
    /// ذخیره می‌شود تا حتی در صورت شکست، تاریخچه باقی بماند.
    /// </summary>
    [HttpPost("{id:guid}/execute")]
    [HasPermission(Permissions.Reports.Export)]
    [ProducesResponseType(typeof(ReportExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReportExecutionDto>> Execute(Guid id, CancellationToken ct)
    {
        var result = await _reportingService.ExecuteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "report_not_found" or "report_archived" or "execution_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "اجرای گزارش ممکن نیست",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "اجرای گزارش ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی اجراهای گزارش.</summary>
    [HttpGet("executions")]
    [HasPermission(Permissions.Reports.View)]
    [ProducesResponseType(typeof(PagedResult<ReportExecutionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReportExecutionDto>>> SearchExecutions(
        [FromQuery] ExecutionSearchRequest request,
        CancellationToken ct)
    {
        var result = await _reportingService.SearchExecutionsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک اجرا با شناسه.</summary>
    [HttpGet("executions/{executionId:guid}")]
    [HasPermission(Permissions.Reports.View)]
    [ProducesResponseType(typeof(ReportExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportExecutionDto>> GetExecution(Guid executionId, CancellationToken ct)
    {
        var result = await _reportingService.GetExecutionAsync(executionId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "اجرای گزارش یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// دانلود فایل خروجی یک اجرای موفق. نوع محتوا بر اساس قالب گزارش تنظیم می‌شود.
    /// </summary>
    [HttpGet("executions/{executionId:guid}/artifact")]
    [HasPermission(Permissions.Reports.Export)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadArtifact(Guid executionId, CancellationToken ct)
    {
        var result = await _reportingService.GetArtifactAsync(executionId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "خروجی گزارش در دسترس نیست",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        var artifact = result.GetValueOrThrow();

        return File(artifact.Content, artifact.ContentType, artifact.FileName);
    }
}
