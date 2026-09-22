using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;

namespace ODCC.Api.Controllers.Identity;

/// <summary>
/// مدیریت کاربران. نیازمند مجوزهای ماژول هویت است.
/// </summary>
[ApiController]
[Route("api/{culture:language}/identity/users")]
public sealed class UsersController(IUserService userService, ICurrentUserService currentUserService)
    : ControllerBase
{
    private readonly IUserService _userService = userService;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی کاربران.</summary>
    [HttpGet]
    [HasPermission(Permissions.Identity.UsersView)]
    [ProducesResponseType(typeof(PagedResult<UserSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> Search(
        [FromQuery] string? searchText,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var request = new UserSearchRequest(searchText, isActive, roleId, page, pageSize);
        var result = await _userService.SearchAsync(request, ct);
        return Ok(result.Value);
    }

    /// <summary>دریافت یک کاربر با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Identity.UsersView)]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserSummaryDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _userService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کاربر یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>پروفایل کاربر جاری (احراز هویت‌شده).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> GetProfile(CancellationToken ct)
    {
        if (_currentUserService.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _userService.GetProfileAsync(userId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "پروفایل یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد کاربر جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Identity.UsersCreate)]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserSummaryDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var result = await _userService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد کاربر ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>ویرایش کاربر.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Identity.UsersEdit)]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserSummaryDto>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
    {
        var result = await _userService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کاربر یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>فعال یا غیرفعال کردن حساب کاربری.</summary>
    [HttpPost("activate")]
    [HasPermission(Permissions.Identity.UsersDeactivate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActive(
        [FromBody] SetUserActiveRequest request,
        CancellationToken ct)
    {
        var result = await _userService.SetActiveAsync(request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کاربر یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return NoContent();
    }

    /// <summary>بازنشانی رمز عبور توسط مدیر.</summary>
    [HttpPost("reset-password")]
    [HasPermission(Permissions.Identity.UsersResetPassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        var result = await _userService.ResetPasswordAsync(request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کاربر یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return NoContent();
    }

    /// <summary>تغییر رمز عبور توسط خود کاربر.</summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        if (_currentUserService.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _userService.ChangePasswordAsync(userId, request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "تغییر رمز عبور ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message
            });
        }

        return NoContent();
    }

    /// <summary>انتصاب نقش‌ها به یک کاربر (جایگزینی کامل).</summary>
    [HttpPost("assign-roles")]
    [HasPermission(Permissions.Identity.UsersEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRoles(
        [FromBody] AssignRolesRequest request,
        CancellationToken ct)
    {
        var result = await _userService.AssignRolesAsync(request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کاربر یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return NoContent();
    }
}
