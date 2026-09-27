using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Reporting.Dtos;
using ODCC.Domain.Modules.Reporting.Entities;
using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Application.Modules.Reporting.Abstractions;

/// <summary>
/// مخزن اختصاصی تعاریف گزارش.
/// </summary>
public interface IReportDefinitionRepository : IRepository<ReportDefinition>
{
    /// <summary>جستجوی صفحه‌بندی‌شده‌ی تعاریف گزارش.</summary>
    Task<IReadOnlyList<ReportDefinition>> SearchAsync(ReportSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد تعاریف مطابق با فیلتر.</summary>
    Task<int> CountAsync(ReportSearchRequest request, CancellationToken ct = default);

    /// <summary>
    /// تعاریف فعالِ زمان‌بندی‌شده‌ای که زمان اجرای رسیده‌شان فرا رسیده.
    /// فقط تعاریف <see cref="ReportStatus.Active"/> با <see cref="ReportDefinition.NextRunAt"/>
    /// غیرتهی و کوچک‌تر یا مساوی <paramref name="asOf"/> بازگردانده می‌شوند.
    /// </summary>
    Task<IReadOnlyList<ReportDefinition>> ListDueAsync(DateTime asOf, CancellationToken ct = default);
}

/// <summary>
/// مخزن اختصاصی اجراهای گزارش.
/// </summary>
public interface IReportExecutionRepository : IRepository<ReportExecution>
{
    /// <summary>جستجوی صفحه‌بندی‌شده‌ی اجراها.</summary>
    Task<IReadOnlyList<ReportExecution>> SearchAsync(ExecutionSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد اجراها مطابق با فیلتر.</summary>
    Task<int> CountAsync(ExecutionSearchRequest request, CancellationToken ct = default);

    /// <summary>آخرین اجراهای یک تعریف (مرتب از جدید به قدیم).</summary>
    Task<IReadOnlyList<ReportExecution>> ListByDefinitionAsync(
        Guid reportDefinitionId, int take, CancellationToken ct = default);
}
