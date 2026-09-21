using Microsoft.AspNetCore.Mvc;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;

namespace ODCC.Api.Controllers;

/// <summary>
/// کنترلر ماژول ممیزی — به‌عنوان ماژول مرجع پیاده‌سازی شده است.
/// در فاز ۱ به کنترلرهای محافظت‌شده با JWT و Authorization اضافه خواهد شد.
/// </summary>
[ApiController]
[Route("api/{culture:language}/audit")]
public sealed class AuditController(IAuditService auditService) : ControllerBase
{
    private readonly IAuditService _auditService = auditService;

    /// <summary>
    /// جستجوی صفحه‌بندی‌شده‌ی رخدادهای ممیزی.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> Search(
        [FromQuery] AuditSearchRequest request,
        CancellationToken ct)
    {
        var entries = await _auditService.SearchAsync(request, ct);
        return Ok(entries);
    }
}
