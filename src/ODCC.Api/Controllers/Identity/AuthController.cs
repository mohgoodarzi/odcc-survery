using Microsoft.AspNetCore.Mvc;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;

namespace ODCC.Api.Controllers.Identity;

/// <summary>
/// احراز هویت: ورود، تازه‌سازی توکن و خروج.
/// این اندپوینت‌ها ناشناس (بدون نیاز به توکن) هستند.
/// </summary>
[ApiController]
[Route("api/{culture:language}/auth")]
public sealed class AuthController(
    IAuthService authService,
    ITokenService tokenService) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly ITokenService _tokenService = tokenService;

    /// <summary>
    /// ورود به سامانه. در صورت موفقیت، جفت توکن دسترسی و تازه‌سازی برگردانده می‌شود.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResult>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _authService.LoginAsync(request, clientIp, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "خطا در ورود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                // کد مستقل از زبان برای کلاینت.
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// تازه‌سازی توکن دسترسی با توکن تازه‌سازی.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TokenResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _authService.RefreshAsync(request, clientIp, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "خطا در تازه‌سازی توکن",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// خروج: ابطال خانواده‌ی توکن تازه‌سازی جاری. idempotent است.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _authService.LogoutAsync(request.RefreshToken, clientIp, ct);

        return NoContent();
    }

    /// <summary>
    /// اعتبارسنجی یک توکن دسترسی (برای کلاینت برای بررسی محلی).
    /// توکن در بدنه‌ی درخواست ارسال می‌شود تا در URL و لاگ‌های سرور ثبت نشود.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Validate([FromBody] ValidateTokenRequest request)
    {
        var principal = _tokenService.ValidateAccessToken(request.AccessToken);

        if (principal is null)
        {
            return Unauthorized();
        }

        var permissions = principal.FindAll(Permissions.ClaimType).Select(c => c.Value).ToList();
        var roles = principal.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new
        {
            isValid = true,
            userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            userName = principal.Identity?.Name,
            roles,
            permissions
        });
    }
}
