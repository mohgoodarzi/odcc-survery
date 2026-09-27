using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;

namespace ODCC.Api.Controllers.Notification;

/// <summary>
/// مدیریت قالب‌های اعلان (پنل مدیریت). قالب‌ها در پایگاه داده نگه‌داری می‌شوند
/// تا متن دعوت‌نامه‌ها و یادآورها بدون تغییر کد قابل ویرایش باشند. اگر قالبی
/// فعال نباشد، رندر از متن پیش‌فرض توکار استفاده می‌کند.
/// </summary>
[ApiController]
[Route("api/{culture:language}/notification-templates")]
public sealed class NotificationTemplatesController(INotificationTemplateService templateService) : ControllerBase
{
    private readonly INotificationTemplateService _templateService = templateService;

    /// <summary>جستجوی قالب‌های اعلان.</summary>
    [HttpGet]
    [HasPermission(Permissions.Notifications.Manage)]
    [ProducesResponseType(typeof(PagedResult<NotificationTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationTemplateDto>>> Search(
        [FromQuery] NotificationTemplateSearchRequest request,
        CancellationToken ct)
    {
        var result = await _templateService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک قالب با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Notifications.Manage)]
    [ProducesResponseType(typeof(NotificationTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationTemplateDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _templateService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "قالب یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد قالب اعلان جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Notifications.Manage)]
    [ProducesResponseType(typeof(NotificationTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationTemplateDto>> Create(
        [FromBody] SaveNotificationTemplateRequest request,
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

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] },
            result.GetValueOrThrow());
    }

    /// <summary>ویرایش یک قالب اعلان.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Notifications.Manage)]
    [ProducesResponseType(typeof(NotificationTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationTemplateDto>> Update(
        Guid id,
        [FromBody] SaveNotificationTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _templateService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "template_not_found" or "template_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "قالب یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
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

    /// <summary>بایگانی قالب (غیرفعال کردن نرم؛ رندر به پیش‌فرض توکار برمی‌گردد).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Notifications.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _templateService.ArchiveAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "قالب یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}
