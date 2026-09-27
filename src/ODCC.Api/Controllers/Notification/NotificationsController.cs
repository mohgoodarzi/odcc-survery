using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Api.Controllers.Notification;

/// <summary>
/// صندوق ورودی اعلان‌ها و ترجیحات تحویل کاربر جاری.
///
/// <b>حریم خصوصی:</b> هر کاربر فقط اعلان‌های <i>خودش</i> را می‌بیند و فقط
/// می‌تواند آن‌ها را خوانده‌شده علامت بزند. این مرز در سرویس اعمال می‌شود،
/// نه در کنترلر. مدیران با مجوز <c>notifications.manage</c> می‌توانند اعلان‌های
/// همه را ببینند.
/// </summary>
[ApiController]
[Route("api/{culture:language}/notifications")]
public sealed class NotificationsController(
    INotificationService notificationService,
    INotificationPreferenceService preferenceService) : ControllerBase
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly INotificationPreferenceService _preferenceService = preferenceService;

    /// <summary>جستجوی اعلان‌های کاربر جاری (یا همه، برای مدیر).</summary>
    [HttpGet]
    [HasPermission(Permissions.Notifications.View)]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> Search(
        [FromQuery] NotificationSearchRequest request,
        CancellationToken ct)
    {
        var result = await _notificationService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>تعداد اعلان‌های خوانده‌نشده‌ی کاربر جاری (برای نشانگر نوار بالا).</summary>
    [HttpGet("unread-count")]
    [HasPermission(Permissions.Notifications.View)]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken ct)
    {
        var count = await _notificationService.GetUnreadCountAsync(ct);
        return Ok(new UnreadCountResponse(count));
    }

    /// <summary>دریافت یک اعلان با شناسه (فقط مالک یا مدیر).</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Notifications.View)]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "اعلان یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>خوانده‌شده علامت زدن یک اعلان درون‌برنامه‌ای.</summary>
    [HttpPost("{id:guid}/read")]
    [HasPermission(Permissions.Notifications.View)]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NotificationDto>> MarkRead(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.MarkReadAsync(id, ct);

        if (result.IsFailure)
        {
            // مالک نبودن هم به‌صورت ۴۰۴ برمی‌گردد تا وجود اعلانِ دیگران فاش نشود.
            return NotFound(new ProblemDetails
            {
                Title = "اعلان یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>خوانده‌شده علامت زدن تمام اعلان‌های خوانده‌نشده‌ی کاربر جاری.</summary>
    [HttpPost("read-all")]
    [HasPermission(Permissions.Notifications.View)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead([FromQuery] NotificationChannel? channel, CancellationToken ct)
    {
        await _notificationService.MarkAllReadAsync(channel, ct);
        return NoContent();
    }

    // --- ترجیحات تحویل ---------------------------------------------------------

    /// <summary>دریافت ترجیحات تحویل کاربر جاری.</summary>
    [HttpGet("preferences")]
    [HasPermission(Permissions.Notifications.View)]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationPreferenceDto>>> GetPreferences(CancellationToken ct)
    {
        var result = await _preferenceService.ListAsync(ct);
        return Ok(result);
    }

    /// <summary>به‌روزرسانی یک ترجیح تحویل (فعال/غیرفعال کردن یک کانال/دسته).</summary>
    [HttpPut("preferences")]
    [HasPermission(Permissions.Notifications.View)]
    [ProducesResponseType(typeof(NotificationPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationPreferenceDto>> UpdatePreference(
        [FromBody] UpdateNotificationPreferenceRequest request,
        CancellationToken ct)
    {
        var result = await _preferenceService.UpdateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "به‌روزرسانی ترجیح ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}

/// <summary>پاسخ شمارش اعلان‌های خوانده‌نشده.</summary>
public sealed record UnreadCountResponse(int Count);
