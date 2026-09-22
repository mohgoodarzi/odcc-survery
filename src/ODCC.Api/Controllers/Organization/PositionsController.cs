using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;

namespace ODCC.Api.Controllers.Organization;

/// <summary>
/// مدیریت موقعیت‌های شغلی.
/// </summary>
[ApiController]
[Route("api/{culture:language}/organization/positions")]
public sealed class PositionsController(IPositionService positionService) : ControllerBase
{
    private readonly IPositionService _positionService = positionService;

    /// <summary>لیست موقعیت‌های شغلی.</summary>
    [HttpGet]
    [HasPermission(Permissions.Organization.PositionsView)]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PositionDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var result = await _positionService.ListAsync(activeOnly, ct);
        return Ok(result.Value);
    }

    /// <summary>دریافت یک موقعیت با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Organization.PositionsView)]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _positionService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "موقعیت شغلی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد موقعیت شغلی.</summary>
    [HttpPost]
    [HasPermission(Permissions.Organization.PositionsManage)]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PositionDto>> Create(
        [FromBody] SavePositionRequest request,
        CancellationToken ct)
    {
        var result = await _positionService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد موقعیت شغلی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>ویرایش موقعیت شغلی.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Organization.PositionsManage)]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> Update(
        Guid id,
        [FromBody] SavePositionRequest request,
        CancellationToken ct)
    {
        var result = await _positionService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "موقعیت شغلی یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>حذف نرم موقعیت شغلی.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Organization.PositionsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _positionService.DeleteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "position_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "موقعیت شغلی یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "حذف موقعیت شغلی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}
