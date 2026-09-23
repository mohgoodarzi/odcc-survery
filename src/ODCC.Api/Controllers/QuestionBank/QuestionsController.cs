using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;

namespace ODCC.Api.Controllers.QuestionBank;

/// <summary>
/// مدیریت کتابخانه‌ی سؤالات: سؤال‌های قابل‌استفاده‌ی مجدد با تگ، گزینه و نسخه.
/// </summary>
[ApiController]
[Route("api/{culture:language}/question-bank/questions")]
public sealed class QuestionsController(IQuestionService questionService) : ControllerBase
{
    private readonly IQuestionService _questionService = questionService;

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی سؤال‌های کتابخانه.</summary>
    [HttpGet]
    [HasPermission(Permissions.QuestionBank.View)]
    [ProducesResponseType(typeof(PagedResult<QuestionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<QuestionDto>>> Search(
        [FromQuery] QuestionSearchRequest request,
        CancellationToken ct)
    {
        var result = await _questionService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک سؤال با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.QuestionBank.View)]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuestionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _questionService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "سؤال یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>تاریخچه‌ی نسخه‌های یک سؤال.</summary>
    [HttpGet("{id:guid}/versions")]
    [HasPermission(Permissions.QuestionBank.View)]
    [ProducesResponseType(typeof(IReadOnlyList<QuestionVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<QuestionVersionDto>>> GetVersions(Guid id, CancellationToken ct)
    {
        var result = await _questionService.GetVersionsAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "سؤال یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد سؤال جدید در کتابخانه.</summary>
    [HttpPost]
    [HasPermission(Permissions.QuestionBank.Manage)]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuestionDto>> Create(
        [FromBody] SaveQuestionRequest request,
        CancellationToken ct)
    {
        var result = await _questionService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد سؤال ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>ویرایش سؤال کتابخانه.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.QuestionBank.Manage)]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuestionDto>> Update(
        Guid id,
        [FromBody] SaveQuestionRequest request,
        CancellationToken ct)
    {
        var result = await _questionService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "question_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "سؤال یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش سؤال ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>بایگانی سؤال (کنار گذاشتن از پرسشنامه‌های جدید).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.QuestionBank.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _questionService.ArchiveAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "سؤال یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return NoContent();
    }
}
