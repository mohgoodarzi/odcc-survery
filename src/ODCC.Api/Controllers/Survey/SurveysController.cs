using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Api.Controllers.Survey;

/// <summary>
/// مدیریت نظرسنجی‌ها: چرخه‌ی عمر، تنظیمات و پرچم ناشناس.
/// </summary>
[ApiController]
[Route("api/{culture:language}/surveys")]
public sealed class SurveysController(ISurveyService surveyService) : ControllerBase
{
    private readonly ISurveyService _surveyService = surveyService;

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی نظرسنجی‌ها.</summary>
    [HttpGet]
    [HasPermission(Permissions.Survey.View)]
    [ProducesResponseType(typeof(PagedResult<SurveySummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SurveySummaryDto>>> Search(
        [FromQuery] SurveySearchRequest request,
        CancellationToken ct)
    {
        var result = await _surveyService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک نظرسنجی با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Survey.View)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SurveyDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _surveyService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "نظرسنجی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد نظرسنجی جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Survey.Create)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyDto>> Create(
        [FromBody] SaveSurveyRequest request,
        CancellationToken ct)
    {
        var result = await _surveyService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد نظرسنجی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>به‌روزرسانی تنظیمات نظرسنجی (فقط در حالت پیش‌نویس).</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Survey.Edit)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyDto>> Update(
        Guid id,
        [FromBody] SaveSurveyRequest request,
        CancellationToken ct)
    {
        var result = await _surveyService.UpdateAsync(id, request, ct);

        return MapResult(result, "ویرایش نظرسنجی ناموفق بود");
    }

    /// <summary>انتشار نظرسنجی (Draft → Scheduled یا Active).</summary>
    [HttpPost("{id:guid}/publish")]
    [HasPermission(Permissions.Survey.Publish)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyDto>> Publish(Guid id, CancellationToken ct)
    {
        var result = await _surveyService.PublishAsync(id, ct);
        return MapResult(result, "انتشار نظرسنجی ناموفق بود");
    }

    /// <summary>شروع پنجره‌ی پاسخ‌گویی (Scheduled → Active).</summary>
    [HttpPost("{id:guid}/start")]
    [HasPermission(Permissions.Survey.Publish)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyDto>> Start(Guid id, CancellationToken ct)
    {
        var result = await _surveyService.StartAsync(id, ct);
        return MapResult(result, "شروع نظرسنجی ناموفق بود");
    }

    /// <summary>توقف موقت پاسخ‌گویی (Active → Paused).</summary>
    [HttpPost("{id:guid}/pause")]
    [HasPermission(Permissions.Survey.Publish)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyDto>> Pause(Guid id, CancellationToken ct)
    {
        var result = await _surveyService.PauseAsync(id, ct);
        return MapResult(result, "توقف نظرسنجی ناموفق بود");
    }

    /// <summary>از سرگیری پاسخ‌گویی (Paused → Active).</summary>
    [HttpPost("{id:guid}/resume")]
    [HasPermission(Permissions.Survey.Publish)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyDto>> Resume(Guid id, CancellationToken ct)
    {
        var result = await _surveyService.ResumeAsync(id, ct);
        return MapResult(result, "از سرگیری نظرسنجی ناموفق بود");
    }

    /// <summary>بستن پنجره‌ی پاسخ‌گویی برای همیشه.</summary>
    [HttpPost("{id:guid}/close")]
    [HasPermission(Permissions.Survey.Publish)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyDto>> Close(Guid id, CancellationToken ct)
    {
        var result = await _surveyService.CloseAsync(id, ct);
        return MapResult(result, "بستن نظرسنجی ناموفق بود");
    }

    /// <summary>بایگانی نظرسنجی.</summary>
    [HttpPost("{id:guid}/archive")]
    [HasPermission(Permissions.Survey.Edit)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SurveyDto>> Archive(Guid id, CancellationToken ct)
    {
        var result = await _surveyService.ArchiveAsync(id, ct);
        return MapResult(result, "بایگانی نظرسنجی ناموفق بود");
    }

    /// <summary>حذف نرم نظرسنجی (فقط در حالت پیش‌نویس).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Survey.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _surveyService.DeleteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "survey_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "نظرسنجی یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "حذف نظرسنجی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }

    /// <summary>ساخت یک نظرسنجی از روی یک قالب.</summary>
    [HttpPost("from-template")]
    [HasPermission(Permissions.Survey.Create)]
    [ProducesResponseType(typeof(SurveyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyDto>> CreateFromTemplate(
        [FromBody] CreateSurveyFromTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _surveyService.CreateFromTemplateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ساخت نظرسنجی از قالب ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>نگاشت یک‌نتیجه به پاسخ HTTP مناسب.</summary>
    private ActionResult<SurveyDto> MapResult(Result<SurveyDto> result, string failureTitle)
    {
        if (result.IsFailure)
        {
            if (result.Error.Code == "survey_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "نظرسنجی یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = failureTitle,
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}
