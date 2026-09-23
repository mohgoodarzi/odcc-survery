using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;

namespace ODCC.Api.Controllers.Questionnaire;

/// <summary>
/// مدیریت پرسشنامه‌ها: ساختار بخش‌ها، آیتم‌ها و قوانین انشعاب.
/// </summary>
[ApiController]
[Route("api/{culture:language}/questionnaires")]
public sealed class QuestionnairesController(IQuestionnaireService questionnaireService) : ControllerBase
{
    private readonly IQuestionnaireService _questionnaireService = questionnaireService;

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی پرسشنامه‌ها.</summary>
    [HttpGet]
    [HasPermission(Permissions.Questionnaire.View)]
    [ProducesResponseType(typeof(PagedResult<QuestionnaireSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<QuestionnaireSummaryDto>>> Search(
        [FromQuery] QuestionnaireSearchRequest request,
        CancellationToken ct)
    {
        var result = await _questionnaireService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک پرسشنامه با شناسه (کل ساختار درختی).</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Questionnaire.View)]
    [ProducesResponseType(typeof(QuestionnaireDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuestionnaireDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _questionnaireService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "پرسشنامه یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد پرسشنامه جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Questionnaire.Manage)]
    [ProducesResponseType(typeof(QuestionnaireDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuestionnaireDto>> Create(
        [FromBody] SaveQuestionnaireRequest request,
        CancellationToken ct)
    {
        var result = await _questionnaireService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد پرسشنامه ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>ذخیره‌ی کامل ساختار پرسشنامه (بخش‌ها، آیتم‌ها، قوانین انشعاب).</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Questionnaire.Manage)]
    [ProducesResponseType(typeof(QuestionnaireDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuestionnaireDto>> Update(
        Guid id,
        [FromBody] SaveQuestionnaireRequest request,
        CancellationToken ct)
    {
        var result = await _questionnaireService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "questionnaire_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "پرسشنامه یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش پرسشنامه ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>انتشار پرسشنامه (فعال‌سازی برای استفاده در نظرسنجی‌ها).</summary>
    [HttpPost("{id:guid}/publish")]
    [HasPermission(Permissions.Questionnaire.Manage)]
    [ProducesResponseType(typeof(QuestionnaireDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuestionnaireDto>> Publish(Guid id, CancellationToken ct)
    {
        var result = await _questionnaireService.PublishAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "questionnaire_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "پرسشنامه یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "انتشار پرسشنامه ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>بایگانی پرسشنامه.</summary>
    [HttpPost("{id:guid}/archive")]
    [HasPermission(Permissions.Questionnaire.Manage)]
    [ProducesResponseType(typeof(QuestionnaireDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuestionnaireDto>> Archive(Guid id, CancellationToken ct)
    {
        var result = await _questionnaireService.ArchiveAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "پرسشنامه یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>حذف نرم پرسشنامه (فقط در حالت پیش‌نویس).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Questionnaire.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _questionnaireService.DeleteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "questionnaire_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "پرسشنامه یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "حذف پرسشنامه ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}
