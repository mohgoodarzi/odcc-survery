using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.ActionManagement.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Api.Controllers.ActionManagement;

/// <summary>
/// مدیریت آیتم‌های اقدام: چرخه‌ی عمر، انتصاب، ارزیابی اثربخشی، دیدگاه‌ها و پیوست‌ها.
///
/// <b>مرز سازمانی:</b> کاربر آیتم‌های داخل دامنه‌ی سازمانی خودش را می‌بیند،
/// به‌علاوه‌ی آیتم‌هایی که مستقیماً به خودش منتسب شده‌اند (استثناء‌ی مستند).
/// این مرز در سرویس اعمال می‌شود، نه در کنترلر.
///
/// <b>حریم خصوصی:</b> آیتم‌ها هرگز محتوای پاسخ یک پاسخ‌گوی نظرسنجی را ذخیره
/// نمی‌کنند. دیدگاه‌ها و پیوست‌ها توسط کاربران سامانه (با نام خودشان) تولید
/// می‌شوند.
/// </summary>
[ApiController]
[Route("api/{culture:language}/actions/items")]
public sealed class ActionItemsController(
    IActionManagementService actionManagementService,
    IValidator<UploadActionEvidenceRequest> evidenceValidator) : ControllerBase
{
    private readonly IActionManagementService _actionManagementService = actionManagementService;
    private readonly IValidator<UploadActionEvidenceRequest> _evidenceValidator = evidenceValidator;

    /// <summary>جستجوی آیتم‌های اقدام (با احترام به دامنه‌ی سازمانی کاربر).</summary>
    [HttpGet]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(PagedResult<ActionItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ActionItemDto>>> Search(
        [FromQuery] ActionItemSearchRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.SearchItemsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک آیتم اقدام با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(ActionItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionItemDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _actionManagementService.GetItemByIdAsync(id, ct);

        if (result.IsFailure)
        {
            // نبودن دسترسی هم ۴۰۴ برمی‌گرداند تا وجود آیتمِ خارج از دامنه فاش نشود.
            return NotFound(new ProblemDetails
            {
                Title = "آیتم اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ویرایش یک آیتم اقدام (عنوان، مسئول، اولویت، مهلت، یادآور).</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionItemDto>> Update(
        Guid id,
        [FromBody] SaveActionItemRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.UpdateItemAsync(id, request, ct);

        if (result.IsFailure)
        {
            return FailureResult(result);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// تغییر وضعیت یک آیتم (شروع/تکمیل/ازسرگیری/لغو). مسئول آیتم می‌تواند وضعیت
    /// کار خودش را تغییر دهد؛ لغو نیازمند مجوز مدیریت است.
    /// </summary>
    [HttpPost("{id:guid}/transition")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(ActionItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionItemDto>> Transition(
        Guid id,
        [FromBody] TransitionActionItemRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.TransitionItemAsync(id, request, ct);

        if (result.IsFailure)
        {
            return FailureResult(result);
        }

        return Ok(result.Value);
    }

    /// <summary>ثبت ارزیابی اثربخشی یک آیتم (فقط پس از تکمیل/لغو).</summary>
    [HttpPost("{id:guid}/effectiveness")]
    [HasPermission(Permissions.Actions.Manage)]
    [ProducesResponseType(typeof(ActionItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionItemDto>> AssessEffectiveness(
        Guid id,
        [FromBody] AssessEffectivenessRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.AssessItemEffectivenessAsync(id, request, ct);

        if (result.IsFailure)
        {
            return FailureResult(result);
        }

        return Ok(result.Value);
    }

    // --- دیدگاه‌ها ---------------------------------------------------------------

    /// <summary>فهرست دیدگاه‌های یک آیتم (از قدیم به جدید).</summary>
    [HttpGet("{id:guid}/comments")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(IReadOnlyList<ActionCommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ActionCommentDto>>> ListComments(Guid id, CancellationToken ct)
    {
        var result = await _actionManagementService.ListCommentsAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "آیتم اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>افزودن یک دیدگاه به آیتم.</summary>
    [HttpPost("{id:guid}/comments")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(ActionCommentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionCommentDto>> AddComment(
        Guid id,
        [FromBody] AddActionCommentRequest request,
        CancellationToken ct)
    {
        var result = await _actionManagementService.AddCommentAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "آیتم اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    // --- پیوست‌ها -----------------------------------------------------------------

    /// <summary>فهرست پیوست‌های یک آیتم.</summary>
    [HttpGet("{id:guid}/evidence")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(IReadOnlyList<ActionEvidenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ActionEvidenceDto>>> ListEvidence(Guid id, CancellationToken ct)
    {
        var result = await _actionManagementService.ListEvidenceAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "آیتم اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// آپلود یک پیوست برای آیتم. نوع و اندازه‌ی فایل اعتبارسنجی می‌شود و محتوا
    /// در انبار فایل‌ها (بیرون از پایگاه داده) ذخیره می‌شود.
    /// </summary>
    [HttpPost("{id:guid}/evidence")]
    [HasPermission(Permissions.Actions.View)]
    [RequestSizeLimit(UploadEvidenceForm.MaxFileBytes)]
    [ProducesResponseType(typeof(ActionEvidenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionEvidenceDto>> UploadEvidence(
        Guid id,
        [FromForm] UploadEvidenceForm form,
        CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "فایل پیوست نامعتبر است",
                Status = StatusCodes.Status400BadRequest,
                Detail = "یک فایل غیرخالی انتخاب کنید.",
                Extensions = { ["code"] = "evidence_file_empty" }
            });
        }

        var request = new UploadActionEvidenceRequest
        {
            Content = form.File.OpenReadStream(),
            FileName = form.FileName ?? form.File.FileName,
            ContentType = string.IsNullOrWhiteSpace(form.ContentType) ? form.File.ContentType : form.ContentType,
            FileSizeBytes = form.File.Length
        };

        // اعتبارسنجی با پیام‌های فارسی (فیلتر خودکار روی [FromBody] کار می‌کند؛
        // این درخواست از فرم ساخته می‌شود، پس دستی اعتبارسنجی می‌شود).
        var validation = await _evidenceValidator.ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

            return BadRequest(new ValidationProblemDetails(errors)
            {
                Title = "اعتبارسنجی فایل پیوست ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["code"] = "validation_failed" }
            });
        }

        var result = await _actionManagementService.UploadEvidenceAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "آیتم اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>دانلود محتوای یک پیوست (فقط کاربران مجاز).</summary>
    [HttpGet("~/api/{culture:language}/actions/evidence/{evidenceId:guid}")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadEvidence(Guid evidenceId, CancellationToken ct)
    {
        var result = await _actionManagementService.DownloadEvidenceAsync(evidenceId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "پیوست یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        var evidence = result.GetValueOrThrow();

        return File(evidence.Content, evidence.ContentType, evidence.FileName);
    }

    /// <summary>حذف یک پیوست (فقط آپلودکننده یا مدیر اقدامات).</summary>
    [HttpDelete("~/api/{culture:language}/actions/evidence/{evidenceId:guid}")]
    [HasPermission(Permissions.Actions.View)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteEvidence(Guid evidenceId, CancellationToken ct)
    {
        var result = await _actionManagementService.DeleteEvidenceAsync(evidenceId, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "action_evidence_not_found" or "action_item_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "پیوست یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "حذف پیوست مجاز نیست",
                Status = StatusCodes.Status403Forbidden,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }

    /// <summary>تبدیل نتیجه‌ی ناموفق سرویس به پاسخ HTTP مناسب.</summary>
    private static ActionResult<ActionItemDto> FailureResult(Result<ActionItemDto> result)
    {
        if (result.Error.Code is "action_item_not_found" or "action_plan_not_found")
        {
            return new NotFoundObjectResult(new ProblemDetails
            {
                Title = "آیتم اقدام یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        if (result.Error.Code is "action_not_authorized" or "action_plan_closed")
        {
            return new ObjectResult(new ProblemDetails
            {
                Title = "عملیات مجاز نیست",
                Status = StatusCodes.Status403Forbidden,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return new BadRequestObjectResult(new ProblemDetails
        {
            Title = "عملیات روی آیتم ناموفق بود",
            Status = StatusCodes.Status400BadRequest,
            Detail = result.Error.Message,
            Extensions = { ["code"] = result.Error.Code }
        });
    }
}

/// <summary>فرم آپلود پیوست (multipart/form-data).</summary>
public sealed class UploadEvidenceForm
{
    /// <summary>حداکثر اندازه‌ی مجاز پیوست (۲۰ مگابایت — همگام با اعتبارسنجی).</summary>
    public const long MaxFileBytes = 20 * 1024 * 1024;

    public IFormFile? File { get; init; }

    /// <summary>نام فایل اختیاری (در صورت نبودن، نام فایل آپلودشده استفاده می‌شود).</summary>
    public string? FileName { get; init; }

    /// <summary>نوع محتوای اختیاری (در صورت نبودن، از نوع تشخیص‌داده‌شده استفاده می‌شود).</summary>
    public string? ContentType { get; init; }
}
