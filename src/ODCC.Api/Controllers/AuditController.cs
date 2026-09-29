using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;

namespace ODCC.Api.Controllers;

/// <summary>
/// کنترلر ماژول ممیزی. مشاهده‌ی رخدادهای ممیزی نیازمند مجوز <c>audit.view</c>
/// است چون این داده‌ها شامل شناسه‌ی کاربر و نشانی IP کلاینت است.
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
    [HasPermission(Permissions.Audit.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> Search(
        [FromQuery] AuditSearchRequest request,
        CancellationToken ct)
    {
        var entries = await _auditService.SearchAsync(request, ct);
        return Ok(entries);
    }
}
