using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;

namespace ODCC.Api.Controllers.Organization;

/// <summary>
/// مدیریت واحدهای سازمانی و ساختار درختی.
/// </summary>
[ApiController]
[Route("api/{culture:language}/organization/units")]
public sealed class OrgUnitsController(IOrgUnitService orgUnitService) : ControllerBase
{
    private readonly IOrgUnitService _orgUnitService = orgUnitService;

    /// <summary>لیست واحدهای سازمانی.</summary>
    [HttpGet]
    [HasPermission(Permissions.Organization.UnitsView)]
    [ProducesResponseType(typeof(IReadOnlyList<OrgUnitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrgUnitDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var result = await _orgUnitService.ListAsync(activeOnly, ct);
        return Ok(result.Value);
    }

    /// <summary>دریافت کل درخت سازمانی به‌صورت سلسله‌مراتبی.</summary>
    [HttpGet("tree")]
    [HasPermission(Permissions.Organization.UnitsView)]
    [ProducesResponseType(typeof(IReadOnlyList<OrgUnitTreeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrgUnitTreeDto>>> GetTree(CancellationToken ct)
    {
        var result = await _orgUnitService.GetTreeAsync(ct);
        return Ok(result.Value);
    }

    /// <summary>دریافت یک واحد با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Organization.UnitsView)]
    [ProducesResponseType(typeof(OrgUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _orgUnitService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "واحد سازمانی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>کلیه زیرمجموعه‌های یک واحد (شامل خودش).</summary>
    [HttpGet("{id:guid}/descendants")]
    [HasPermission(Permissions.Organization.UnitsView)]
    [ProducesResponseType(typeof(IReadOnlyList<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetDescendants(Guid id, CancellationToken ct)
    {
        var result = await _orgUnitService.GetDescendantUnitIdsAsync(id, ct);
        return Ok(result.Value);
    }

    /// <summary>ایجاد واحد سازمانی.</summary>
    [HttpPost]
    [HasPermission(Permissions.Organization.UnitsManage)]
    [ProducesResponseType(typeof(OrgUnitDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrgUnitDto>> Create(
        [FromBody] SaveOrgUnitRequest request,
        CancellationToken ct)
    {
        var result = await _orgUnitService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد واحد سازمانی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>ویرایش واحد سازمانی.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Organization.UnitsManage)]
    [ProducesResponseType(typeof(OrgUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitDto>> Update(
        Guid id,
        [FromBody] SaveOrgUnitRequest request,
        CancellationToken ct)
    {
        var result = await _orgUnitService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "واحد سازمانی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>حذف نرم واحد سازمانی.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Organization.UnitsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _orgUnitService.DeleteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "org_unit_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "واحد سازمانی یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "حذف واحد سازمانی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}
