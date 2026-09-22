using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;

namespace ODCC.Api.Controllers.Identity;

/// <summary>
/// مدیریت نقش‌ها و مجوزها. نیازمند مجوزهای ماژول هویت است.
/// </summary>
[ApiController]
[Route("api/{culture:language}/identity/roles")]
public sealed class RolesController(IRoleService roleService) : ControllerBase
{
    private readonly IRoleService _roleService = roleService;

    /// <summary>
    /// کاتالوگ تمام مجوزهای تعریف‌شده به‌صورت گروهی. برای رابط کاربری
    /// انتخاب مجوز هنگام ویرایش نقش استفاده می‌شود.
    /// </summary>
    [HttpGet("permissions/catalog")]
    [HasPermission(Permissions.Identity.RolesView)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionGroupDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PermissionGroupDto>> GetPermissionCatalog()
    {
        return Ok(PermissionCatalog.Groups);
    }

    /// <summary>لیست تمام نقش‌ها.</summary>
    [HttpGet]
    [HasPermission(Permissions.Identity.RolesView)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleSummaryDto>>> List(CancellationToken ct)
    {
        var result = await _roleService.ListAsync(ct);
        return Ok(result.Value);
    }

    /// <summary>دریافت یک نقش با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Identity.RolesView)]
    [ProducesResponseType(typeof(RoleSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleSummaryDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _roleService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "نقش یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد نقش جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Identity.RolesManage)]
    [ProducesResponseType(typeof(RoleSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleSummaryDto>> Create(
        [FromBody] SaveRoleRequest request,
        CancellationToken ct)
    {
        var result = await _roleService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد نقش ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>ویرایش نقش.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Identity.RolesManage)]
    [ProducesResponseType(typeof(RoleSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleSummaryDto>> Update(
        Guid id,
        [FromBody] SaveRoleRequest request,
        CancellationToken ct)
    {
        var result = await _roleService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "نقش یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>انتصاب مجوزها به یک نقش (جایگزینی کامل).</summary>
    [HttpPost("{id:guid}/permissions")]
    [HasPermission(Permissions.Identity.RolesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignPermissions(
        Guid id,
        [FromBody] AssignRolePermissionsRequest request,
        CancellationToken ct)
    {
        var effectiveRequest = request with { RoleId = id };

        var result = await _roleService.AssignPermissionsAsync(effectiveRequest, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "نقش یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return NoContent();
    }
}
