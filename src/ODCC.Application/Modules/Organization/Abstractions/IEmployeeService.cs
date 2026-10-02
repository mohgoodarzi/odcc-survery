using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Organization.Abstractions;

/// <summary>
/// سرویس مدیریت کارمندان.
/// </summary>
public interface IEmployeeService
{
    Task<Result<PagedResult<EmployeeSummaryDto>>> SearchAsync(EmployeeSearchRequest request, CancellationToken ct = default);

    Task<Result<EmployeeDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>دریافت کارمند مرتبط با یک حساب کاربری.</summary>
    Task<Result<EmployeeDto>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<Result<EmployeeDto>> CreateAsync(SaveEmployeeRequest request, CancellationToken ct = default);

    /// <summary>
    /// ورود گروهی کارمندان از یک فایل اکسل (xlsx).
    /// ردیف‌های معتبر ایجاد می‌شوند، ردیف‌های با کد پرسنلی تکراری نادیده گرفته
    /// می‌شوند و ردیف‌های نامعتبر در نتیجه گزارش می‌شوند (بدون توقف کل عملیات).
    /// </summary>
    /// <param name="excelStream">جریان فایل اکسل.</param>
    /// <param name="ct">توکن لغو.</param>
    Task<Result<EmployeeImportResultDto>> ImportAsync(Stream excelStream, CancellationToken ct = default);

    Task<Result<EmployeeDto>> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
