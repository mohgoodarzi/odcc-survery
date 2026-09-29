using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;

namespace ODCC.Api.Controllers.Integration;

/// <summary>
/// مدیریت اندپوینت‌های یکپارچه‌سازی خارجی (وب‌هوک، همگام‌سازی HR، SSO،
/// ارائه‌دهنده‌ی هوش مصنوعی).
///
/// <b>امنیت:</b> مقدار رازها هرگز از این کنترلر خارج نمی‌شود. فقط نام منطقی
/// راز و وضعیت «پیکربندی‌شده» برگردانده می‌شود.
///
/// <b>مجوزها:</b> خواندن نیازمند <c>integrations.view</c> و مدیریت نیازمند
/// <c>integrations.manage</c> است.
/// </summary>
[ApiController]
[Route("api/{culture:language}/integrations/endpoints")]
public sealed class IntegrationEndpointsController(IIntegrationService integrationService) : ControllerBase
{
    private readonly IIntegrationService _integrationService = integrationService;

    /// <summary>جستجوی اندپوینت‌های یکپارچه‌سازی.</summary>
    [HttpGet]
    [HasPermission(Permissions.Integrations.View)]
    [ProducesResponseType(typeof(PagedResult<IntegrationEndpointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<IntegrationEndpointDto>>> Search(
        [FromQuery] IntegrationEndpointSearchRequest request,
        CancellationToken ct)
    {
        var result = await _integrationService.SearchEndpointsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>آمار یکپارچه‌سازی برای داشبورد.</summary>
    [HttpGet("stats")]
    [HasPermission(Permissions.Integrations.View)]
    [EnableRateLimiting("Critical")]
    [OutputCache(PolicyName = "Dashboard")]
    [ProducesResponseType(typeof(IntegrationStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IntegrationStatsDto>> GetStats(CancellationToken ct)
    {
        var result = await _integrationService.GetStatsAsync(ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "دریافت آمار یکپارچه‌سازی ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>دریافت یک اندپوینت با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Integrations.View)]
    [ProducesResponseType(typeof(IntegrationEndpointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntegrationEndpointDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _integrationService.GetEndpointByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "اندپوینت یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد اندپوینت یکپارچه‌سازی جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Integrations.Manage)]
    [ProducesResponseType(typeof(IntegrationEndpointDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IntegrationEndpointDto>> Create(
        [FromBody] SaveIntegrationEndpointRequest request,
        CancellationToken ct)
    {
        var result = await _integrationService.CreateEndpointAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد اندپوینت ناموفق بود",
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

    /// <summary>ویرایش یک اندپوینت.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Integrations.Manage)]
    [ProducesResponseType(typeof(IntegrationEndpointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IntegrationEndpointDto>> Update(
        Guid id,
        [FromBody] SaveIntegrationEndpointRequest request,
        CancellationToken ct)
    {
        var result = await _integrationService.UpdateEndpointAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "integration_endpoint_not_found" or "integration_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "اندپوینت یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش اندپوینت ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>فعال‌سازی یک اندپوینت.</summary>
    [HttpPost("{id:guid}/activate")]
    [HasPermission(Permissions.Integrations.Manage)]
    [ProducesResponseType(typeof(IntegrationEndpointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntegrationEndpointDto>> Activate(Guid id, CancellationToken ct)
    {
        var result = await _integrationService.ActivateEndpointAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "اندپوینت یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>غیرفعال‌سازی موقت یک اندپوینت.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(Permissions.Integrations.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await _integrationService.DeactivateEndpointAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "اندپوینت یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }

    /// <summary>بایگانی (حذف نرم) یک اندپوینت.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Integrations.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _integrationService.ArchiveEndpointAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "اندپوینت یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }

    /// <summary>آزمون اتصال یک اندپوینت با payload آزمونی.</summary>
    [HttpPost("{id:guid}/test")]
    [HasPermission(Permissions.Integrations.Manage)]
    [ProducesResponseType(typeof(TestEndpointResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestEndpointResultDto>> Test(
        Guid id,
        [FromBody] TestIntegrationEndpointRequest request,
        CancellationToken ct)
    {
        var result = await _integrationService.TestEndpointAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "اندپوینت یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}
