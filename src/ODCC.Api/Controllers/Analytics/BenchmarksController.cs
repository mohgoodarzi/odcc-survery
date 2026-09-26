using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;

namespace ODCC.Api.Controllers.Analytics;

/// <summary>
/// مدیریت بنچمارک‌ها (اهداف مرجع شاخص‌های تحلیلی).
///
/// بنچمارک‌ها فقط یک عدد مرجع هستند و هیچ داده‌ی پاسخ‌گو ندارند.
/// </summary>
[ApiController]
[Route("api/{culture:language}/benchmarks")]
public sealed class BenchmarksController(IBenchmarkService benchmarkService) : ControllerBase
{
    private readonly IBenchmarkService _benchmarkService = benchmarkService;

    /// <summary>جستجوی بنچمارک‌ها.</summary>
    [HttpGet]
    [HasPermission(Permissions.Analytics.CompanyView)]
    [ProducesResponseType(typeof(PagedResult<BenchmarkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BenchmarkDto>>> Search(
        [FromQuery] string? searchText,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await _benchmarkService.SearchAsync(searchText, includeInactive, ct);
        return Ok(result);
    }

    /// <summary>دریافت یک بنچمارک با شناسه.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Analytics.CompanyView)]
    [ProducesResponseType(typeof(BenchmarkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BenchmarkDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _benchmarkService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "بنچمارک یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد بنچمارک جدید.</summary>
    [HttpPost]
    [HasPermission(Permissions.Analytics.CompanyView)]
    [ProducesResponseType(typeof(BenchmarkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BenchmarkDto>> Create(
        [FromBody] SaveBenchmarkRequest request,
        CancellationToken ct)
    {
        var result = await _benchmarkService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد بنچمارک ناموفق بود",
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

    /// <summary>ویرایش یک بنچمارک.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Analytics.CompanyView)]
    [ProducesResponseType(typeof(BenchmarkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BenchmarkDto>> Update(
        Guid id,
        [FromBody] SaveBenchmarkRequest request,
        CancellationToken ct)
    {
        var result = await _benchmarkService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "benchmark_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "بنچمارک یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "ویرایش بنچمارک ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>غیرفعال‌سازی یک بنچمارک (حذف نرم).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Analytics.CompanyView)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _benchmarkService.DeleteAsync(id, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "بنچمارک یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}
