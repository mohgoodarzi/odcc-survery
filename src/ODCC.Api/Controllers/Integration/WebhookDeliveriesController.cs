using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;

namespace ODCC.Api.Controllers.Integration;

/// <summary>
/// تاریخچه‌ی تحویل وب‌هوک‌های خروجی و تلاش مجدد آن‌ها.
/// </summary>
[ApiController]
[Route("api/{culture:language}/integrations/deliveries")]
public sealed class WebhookDeliveriesController(IIntegrationService integrationService) : ControllerBase
{
    private readonly IIntegrationService _integrationService = integrationService;

    /// <summary>جستجوی تحویل‌های وب‌هوک.</summary>
    [HttpGet]
    [HasPermission(Permissions.Integrations.View)]
    [ProducesResponseType(typeof(PagedResult<WebhookDeliveryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<WebhookDeliveryDto>>> Search(
        [FromQuery] WebhookDeliverySearchRequest request, CancellationToken ct)
    {
        var result = await _integrationService.SearchDeliveriesAsync(request, ct);
        return Ok(result);
    }

    /// <summary>دریافت جزئیات یک تحویل (شامل payload).</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Integrations.View)]
    [ProducesResponseType(typeof(WebhookDeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookDeliveryDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _integrationService.GetDeliveryByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "تحویل یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>تلاش مجدد دستی یک تحویل ناموفق.</summary>
    [HttpPost("{id:guid}/retry")]
    [HasPermission(Permissions.Integrations.Retry)]
    [ProducesResponseType(typeof(WebhookDeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WebhookDeliveryDto>> Retry(Guid id, CancellationToken ct)
    {
        var result = await _integrationService.RetryDeliveryAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "webhook_delivery_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "تحویل یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "تلاش مجدد ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}
