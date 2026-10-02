using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;

namespace ODCC.Api.Controllers.Organization;

/// <summary>
/// جستجو و مدیریت کارمندان.
///
/// نتایج جستجو به‌طور خودکار به دامنه‌ی سازمانی قابل‌مشاهده توسط کاربر جاری
/// محدود می‌شوند (data isolation).
/// </summary>
[ApiController]
[Route("api/{culture:language}/organization/employees")]
public sealed class EmployeesController(IEmployeeService employeeService) : ControllerBase
{
    private readonly IEmployeeService _employeeService = employeeService;

    /// <summary>
    /// جستجوی صفحه‌بندی‌شده‌ی کارمندان.
    /// </summary>
    [HttpGet]
    [HasPermission(Permissions.Organization.EmployeesView)]
    [ProducesResponseType(typeof(PagedResult<EmployeeSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeSummaryDto>>> Search(
        [FromQuery] string? searchText,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] bool includeDescendants = false,
        [FromQuery] ODCC.Domain.Modules.Organization.Enums.EmployeeStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var request = new EmployeeSearchRequest(searchText, orgUnitId, includeDescendants, status, page, pageSize);

        var result = await _employeeService.SearchAsync(request, ct);
        return Ok(result.Value);
    }

    /// <summary>دریافت یک کارمند با شناسه (بررسی دامنه‌ی دسترسی).</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Organization.EmployeesView)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _employeeService.GetByIdAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "access_denied")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "دسترسی غیرمجاز",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return NotFound(new ProblemDetails
            {
                Title = "کارمند یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>دریافت کارمند مرتبط با حساب کاربری جاری.</summary>
    [HttpGet("by-user/{userId:guid}")]
    [HasPermission(Permissions.Organization.EmployeesView)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetByUserId(Guid userId, CancellationToken ct)
    {
        var result = await _employeeService.GetByUserIdAsync(userId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کارمند یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ایجاد کارمند.</summary>
    [HttpPost]
    [HasPermission(Permissions.Organization.EmployeesManage)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] SaveEmployeeRequest request,
        CancellationToken ct)
    {
        var result = await _employeeService.CreateAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ایجاد کارمند ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.GetValueOrThrow().Id, culture = RouteData.Values["culture"] }, result.GetValueOrThrow());
    }

    /// <summary>
    /// ورود گروهی کارمندان از یک فایل اکسل (.xlsx).
    ///
    /// ردیف‌های معتبر ایجاد می‌شوند، ردیف‌های دارای کد پرسنلی تکراری نادیده گرفته
    /// می‌شوند و ردیف‌های نامعتبر در پاسخ گزارش می‌شوند. هیچ جدول یا مدل جدیدی
    /// ساخته نمی‌شود — همه‌ی ردیف‌ها از طریق همان مسیر ایجاد کارمند پردازش می‌شوند.
    /// </summary>
    [HttpPost("import")]
    [HasPermission(Permissions.Organization.EmployeesManage)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [ProducesResponseType(typeof(EmployeeImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeImportResultDto>> Import(
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "فایل نامعتبر",
                Status = StatusCodes.Status400BadRequest,
                Detail = "فایلی برای ورود اطلاعات ارسال نشده است.",
                Extensions = { ["code"] = "import_invalid_file" }
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "فرمت فایل پشتیبانی نمی‌شود",
                Status = StatusCodes.Status400BadRequest,
                Detail = "فقط فایل‌های اکسل با پسوند .xlsx پشتیبانی می‌شوند.",
                Extensions = { ["code"] = "import_invalid_file" }
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await _employeeService.ImportAsync(stream, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ورود اطلاعات ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>ویرایش کارمند.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Organization.EmployeesManage)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> Update(
        Guid id,
        [FromBody] SaveEmployeeRequest request,
        CancellationToken ct)
    {
        var result = await _employeeService.UpdateAsync(id, request, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "کارمند یافت نشد",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message
            });
        }

        return Ok(result.Value);
    }

    /// <summary>حذف نرم کارمند.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Organization.EmployeesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _employeeService.DeleteAsync(id, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "employee_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "کارمند یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "حذف کارمند ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return NoContent();
    }
}
