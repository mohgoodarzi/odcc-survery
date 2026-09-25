using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Application.Modules.Response.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Api.Controllers.Response;

/// <summary>
/// مدیریت پاسخ‌ها: چرخه‌ی عمر نشست پاسخ‌گویی (شروع، ذخیره‌ی جزئی، ارسال)،
/// فهرست نظرسنجی‌های پاسخ‌پذیر و مدیریت نشست‌ها.
/// </summary>
[ApiController]
[Route("api/{culture:language}/responses")]
public sealed class ResponsesController(IResponseService responseService) : ControllerBase
{
    private readonly IResponseService _responseService = responseService;

    // --- پاسخ‌گو ---------------------------------------------------------------

    /// <summary>نظرسنجی‌هایی که کاربر جاری می‌تواند به آن‌ها پاسخ دهد.</summary>
    [HttpGet("my-surveys")]
    [ProducesResponseType(typeof(PagedResult<RespondableSurveyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RespondableSurveyDto>>> MySurveys(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _responseService.GetMySurveysAsync(page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// بسته‌ی کامل یک نظرسنجی برای پاسخ‌گویی: تنظیمات، ساختار پرسشنامه
    /// (بخش‌ها/آیتم‌ها/گزینه‌ها) و نشست قبلی پاسخ‌گو (در صورت وجود).
    /// </summary>
    [HttpGet("surveys/{surveyId:guid}/context")]
    [ProducesResponseType(typeof(RespondentSurveyContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespondentSurveyContextDto>> GetRespondentContext(Guid surveyId, CancellationToken ct)
    {
        var result = await _responseService.GetRespondentContextAsync(surveyId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "بسته‌ی پاسخ‌گویی در دسترس نیست",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>نشست فعلی پاسخ‌گو برای یک نظرسنجی (در صورت وجود).</summary>
    [HttpGet("surveys/{surveyId:guid}/session")]
    [ProducesResponseType(typeof(ResponseSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResponseSessionDto>> GetMySession(Guid surveyId, CancellationToken ct)
    {
        var result = await _responseService.GetMySessionAsync(surveyId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "نشست پاسخ‌گویی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>شروع یا از سرگیری یک نشست پاسخ‌گویی.</summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(ResponseSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResponseSessionDto>> StartSession(
        [FromBody] StartSessionRequest request,
        CancellationToken ct)
    {
        var result = await _responseService.StartSessionAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "شروع نشست پاسخ‌گویی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(
            nameof(GetMySession),
            new { surveyId = result.GetValueOrThrow().SurveyId, culture = RouteData.Values["culture"] },
            result.GetValueOrThrow());
    }

    /// <summary>ذخیره‌ی جزئی پاسخ‌ها (بدون اعتبارسنجی سؤال‌های اجباری).</summary>
    [HttpPut("sessions/{sessionId:guid}/answers")]
    [ProducesResponseType(typeof(ResponseSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResponseSessionDto>> SaveAnswers(
        Guid sessionId,
        [FromBody] SaveAnswersRequest request,
        CancellationToken ct)
    {
        var result = await _responseService.SaveAnswersAsync(sessionId, request, ct);
        return MapResult(result, "ذخیره‌ی پاسخ‌ها ناموفق بود");
    }

    /// <summary>ارسال نهایی پاسخ‌ها. سؤال‌های اجباری اعتبارسنجی می‌شوند.</summary>
    [HttpPost("sessions/{sessionId:guid}/submit")]
    [ProducesResponseType(typeof(ResponseSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResponseSessionDto>> Submit(
        Guid sessionId,
        [FromBody] SubmitResponseRequest request,
        CancellationToken ct)
    {
        var result = await _responseService.SubmitAsync(sessionId, request, ct);
        return MapResult(result, "ارسال پاسخ ناموفق بود");
    }

    // --- مدیریت ---------------------------------------------------------------

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی نشست‌های پاسخ.</summary>
    [HttpGet]
    [HasPermission(Permissions.Response.View)]
    [ProducesResponseType(typeof(PagedResult<ResponseSessionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ResponseSessionSummaryDto>>> Search(
        [FromQuery] ResponseSearchRequest request,
        CancellationToken ct)
    {
        var result = await _responseService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک نشست با شناسه (به‌همراه پاسخ‌ها).</summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [HasPermission(Permissions.Response.View)]
    [ProducesResponseType(typeof(ResponseSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResponseSessionDto>> GetById(Guid sessionId, CancellationToken ct)
    {
        var result = await _responseService.GetByIdAsync(sessionId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "نشست پاسخ‌گویی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>حذف نرم یک نشست (فقط مدیران).</summary>
    [HttpDelete("sessions/{sessionId:guid}")]
    [HasPermission(Permissions.Response.Export)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid sessionId, CancellationToken ct)
    {
        var result = await _responseService.DeleteAsync(sessionId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "نشست پاسخ‌گویی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return NoContent();
    }

    /// <summary>نگاشت یک‌نتیجه به پاسخ HTTP مناسب.</summary>
    private ActionResult<ResponseSessionDto> MapResult(Result<ResponseSessionDto> result, string failureTitle)
    {
        if (result.IsFailure)
        {
            if (result.Error.Code is "response_session_not_found" or "survey_not_open")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "نشست پاسخ‌گویی یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
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
