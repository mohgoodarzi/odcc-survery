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
    /// <summary>
    /// جستجوی صفحه‌بندی‌شده‌ی تعاریف گزارش.
    /// </summary>
    /// <param name="request">درخواست جستجوی تعاریف گزارش.</param>
    /// <param name="scope">
    /// دامنه‌ی سازمانی قابل‌مشاهده توسط کاربر جاری (fail-closed). دامنه‌ی
    /// Company محدودیتی اعمال نمی‌کند؛ دامنه‌های دیگر فقط تعاریف داخل
    /// زیردرخت لنگر را برمی‌گردانند و در صورت نبودن لنگر معتبر، هیچ چیزی
    /// برنمی‌گردانند.
    /// </param>
    /// <param name="ct">توکن لغو.</param>
    Task<IReadOnlyList<ReportDefinition>> SearchAsync(
        ReportSearchRequest request,
        Authorization.OrgScope? scope = null,
        CancellationToken ct = default);

    /// <summary>
    /// تعداد تعاریف مطابق با فیلتر.
    /// </summary>
    /// <param name="request">درخواست جستجوی تعاریف گزارش.</param>
    /// <param name="scope">
    /// دامنه‌ی سازمانی قابل‌مشاهده (همان semantics <see cref="SearchAsync(ReportSearchRequest, Authorization.OrgScope?, CancellationToken)"/>).
    /// </param>
    /// <param name="ct">توکن لغو.</param>
    Task<int> CountAsync(
        ReportSearchRequest request,
        Authorization.OrgScope? scope = null,
        CancellationToken ct = default);

    /// <summary>
    /// شناسه‌ی تعاریف قابل‌مشاهده در دامنه‌ی سازمانی فعلی (fail-closed).
    /// برای دامنه‌ی Company همه‌ی تعاریف را برمی‌گرداند؛ برای دامنه‌ی Own یا
    /// بدون لنگر معتبر، لیست خالی.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListVisibleIdsAsync(Authorization.OrgScope scope, CancellationToken ct = default);

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
    /// <summary>
    /// جستجوی صفحه‌بندی‌شده‌ی اجراها.
    /// </summary>
    /// <param name="request">درخواست جستجوی اجراها.</param>
    /// <param name="visibleDefinitionIds">
    /// شناسه‌ی تعاریف قابل‌مشاهده در دامنه‌ی سازمانی فعلی. <c>null</c> یعنی بدون
    /// محدودیت؛ لیست خالی یعنی هیچ اجرایی نباید برگردد (fail-closed).
    /// </param>
    /// <param name="ct">توکن لغو.</param>
    Task<IReadOnlyList<ReportExecution>> SearchAsync(
        ExecutionSearchRequest request,
        IReadOnlyCollection<Guid>? visibleDefinitionIds = null,
        CancellationToken ct = default);

    /// <summary>
    /// تعداد اجراها مطابق با فیلتر.
    /// </summary>
    /// <param name="request">درخواست جستجوی اجراها.</param>
    /// <param name="visibleDefinitionIds">
    /// شناسه‌ی تعاریف قابل‌مشاهده (همان semantics <see cref="SearchAsync(ExecutionSearchRequest, IReadOnlyCollection{Guid}?, CancellationToken)"/>).
    /// </param>
    /// <param name="ct">توکن لغو.</param>
    Task<int> CountAsync(
        ExecutionSearchRequest request,
        IReadOnlyCollection<Guid>? visibleDefinitionIds = null,
        CancellationToken ct = default);

    /// <summary>آخرین اجراهای یک تعریف (مرتب از جدید به قدیم).</summary>
    Task<IReadOnlyList<ReportExecution>> ListByDefinitionAsync(
        Guid reportDefinitionId, int take, CancellationToken ct = default);
}
