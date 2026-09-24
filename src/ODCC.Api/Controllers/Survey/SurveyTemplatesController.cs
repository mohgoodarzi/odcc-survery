using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;

namespace ODCC.Api.Controllers.Survey;

/// <summary>
/// مدیریت قالب‌های نظرسنجی: مجموعه‌های قابل‌استفاده‌ی مجدد از پرسشنامه + تنظیمات.
/// </summary>
[ApiController]
[Route("api/{culture:language}/survey-templates")]
public sealed class SurveyTemplatesController(ISurveyTemplateService templateService) : ControllerBase
{
    private readonly ISurveyTemplateService _templateService = templateService;

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی قالب‌ها.</summary>
    [HttpGet]
    [HasPermission(Permissions.Survey.View)]
    [ProducesResponseType(typeof(PagedResult<SurveyTemplateSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SurveyTemplateSummaryDto>>> Search(
        [FromQuery] SurveyTemplateSearchRequest request,
        CancellationToken ct)
    {
        var result = await _templateService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک قالب با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Survey.View)]
    [ProducesResponseType(typeof(SurveyTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SurveyTemplateDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _templateService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "قالب یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد قالب جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Survey.Create)]
    [ProducesResponseType(typeof(SurveyTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyTemplateDto>> Create(
        [FromBody] SaveSurveyTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _templateService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد قالب ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>به‌روزرسانی قالب.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Survey.Edit)]
    [ProducesResponseType(typeof(SurveyTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyTemplateDto>> Update(
        Guid id,
        [FromBody] SaveSurveyTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _templateService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "template_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "قالب یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش قالب ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>بایگانی قالب.</summary>
    [HttpPost("{id:guid}/archive")]
    [HasPermission(Permissions.Survey.Edit)]
    [ProducesResponseType(typeof(SurveyTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SurveyTemplateDto>> Archive(Guid id, CancellationToken ct)
    {
        var result = await _templateService.ArchiveAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "قالب یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>حذف نرم قالب.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Survey.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _templateService.DeleteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "template_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "قالب یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "حذف قالب ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}
