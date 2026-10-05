using Microsoft.AspNetCore.Authorization;
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

    /// <summary>
    /// دریافت تصویر آواتار کاربر جاری. نشانی عمومی این تصویر در پروفایل
    /// (<c>avatarUrl</c>) برگردانده می‌شود و فقط برای کاربر احراز هویت‌شده است.
    /// </summary>
    [HttpGet("me/avatar")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAvatar(CancellationToken ct)
    {
        if (_currentUserService.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return await GetAvatarAsync(userId, ct);
    }

    /// <summary>
    /// بارگذاری یا تعویض تصویر آواتار حساب خود. نوع و اندازه‌ی تصویر اعتبارسنجی
    /// می‌شود و محتوا در انبار فایل‌ها ذخیره می‌شود.
    /// </summary>
    [HttpPost("me/avatar")]
    [RequestSizeLimit(UploadAvatarForm.MaxFileBytes)]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSummaryDto>> UploadMyAvatar(
        [FromForm] UploadAvatarForm form,
        CancellationToken ct)
    {
        if (_currentUserService.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return await UploadAvatarAsync(userId, form, ct);
    }

    /// <summary>حذف تصویر آواتار حساب خود.</summary>
    [HttpDelete("me/avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMyAvatar(CancellationToken ct)
    {
        if (_currentUserService.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return await DeleteAvatarAsync(userId, ct);
    }

    /// <summary>
    /// دریافت تصویر آواتار یک کاربر.
    ///
    /// هر کاربر می‌تواند تصویر آواتارِ خودش را ببیند (به‌اشتراک‌گذاری نشانی در
    /// پروفایل و نوار بالای او نیازمند این است)؛ دیدن آواتار کاربران دیگر
    /// نیازمند مجوز مشاهده‌ی کاربران است.
    ///
    /// بخش اختیاری <c>fileName</c> در نشانی فقط برای مدیریت حافظه‌ی نهان است
    /// و در سمت سرور نادیده گرفته می‌شود؛ تصویر فعلی کاربر همیشه برگردانده می‌شود.
    /// این مسیر مستقل از فرهنگ است چون تصویر آواتار محلی‌سازی ندارد.
    /// </summary>
    [HttpGet("~/api/identity/users/{id:guid}/avatar/{fileName?}")]
    [Authorize]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAvatar(Guid id, CancellationToken ct)
    {
        if (id != _currentUserService.UserId
            && !_currentUserService.HasPermission(Permissions.Identity.UsersView))
        {
            return Forbid();
        }

        return await GetAvatarAsync(id, ct);
    }

    /// <summary>
    /// بارگذاری یا تعویض تصویر آواتار یک کاربر توسط مدیر (مجوز ویرایش کاربران).
    /// </summary>
    [HttpPost("{id:guid}/avatar")]
    [HasPermission(Permissions.Identity.UsersEdit)]
    [RequestSizeLimit(UploadAvatarForm.MaxFileBytes)]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserSummaryDto>> UploadUserAvatar(
        Guid id,
        [FromForm] UploadAvatarForm form,
        CancellationToken ct)
    {
        return await UploadAvatarAsync(id, form, ct);
    }

    /// <summary>حذف تصویر آواتار یک کاربر توسط مدیر.</summary>
    [HttpDelete("{id:guid}/avatar")]
    [HasPermission(Permissions.Identity.UsersEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUserAvatar(Guid id, CancellationToken ct)
    {
        return await DeleteAvatarAsync(id, ct);
    }

    /// <summary>خواندن تصویر آواتار از انبار فایل و بازگرداندن آن به‌عنوان فایل.</summary>
    private async Task<IActionResult> GetAvatarAsync(Guid userId, CancellationToken ct)
    {
        var result = await _userService.OpenAvatarAsync(userId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "تصویر آواتار یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        var avatar = result.GetValueOrThrow();

        return File(avatar.Content, avatar.ContentType);
    }

    /// <summary>ذخیره‌ی تصویر آواتار آپلودشده برای یک کاربر.</summary>
    private async Task<ActionResult<UserSummaryDto>> UploadAvatarAsync(
        Guid userId, UploadAvatarForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "فایل تصویر نامعتبر است",
                Status = StatusCodes.Status400BadRequest,
                Detail = "یک فایل تصویری غیرخالی انتخاب کنید.",
                Extensions = { ["code"] = "avatar_empty" }
            });
        }

        await using var stream = form.File.OpenReadStream();

        var result = await _userService.SetAvatarAsync(userId, stream, form.File.FileName, form.File.ContentType, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "بارگذاری تصویر آواتار ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>حذف تصویر آواتار یک کاربر.</summary>
    private async Task<IActionResult> DeleteAvatarAsync(Guid userId, CancellationToken ct)
    {
        var result = await _userService.RemoveAvatarAsync(userId, ct);

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

        return NoContent();
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

/// <summary>
/// فرم بارگذاری تصویر آواتار. نام اصلی فایل فقط برای استخراج پسوند استفاده
/// می‌شود و هرگز در انبار فایل‌ها ذخیره نمی‌شود.
/// </summary>
public sealed class UploadAvatarForm
{
    /// <summary>سقف حجم درخواست برای جلوگیری از بارگذاری فایل‌های بزرگ.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    public IFormFile? File { get; init; }
}
