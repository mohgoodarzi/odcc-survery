using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Reporting.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Reporting.Abstractions;

/// <summary>
/// سرویس مدیریت گزارش‌ها: تعاریف، زمان‌بندی و اجرا.
///
/// **مسئولیت:** ایجاد/ویرایش/بایگانی تعاریف گزارش، اجرای آن‌ها (دستی یا
/// زمان‌بندی‌شده)، نگه‌داری تاریخچه‌ی اجرا و ارائه‌ی فایل خروجی برای دانلود.
///
/// **مرزهای ماژول‌ها:** این سرویس برای خواندن داده‌های گزارش فقط از قرارداد
/// <c>IAnalyticsService</c> و <c>IBenchmarkService</c> استفاده می‌کند — هرگز از
/// DbContext ماژول تحلیلات.
///
/// **حریم خصوصی:** خروجی گزارش‌ها فقط شامل تجمع‌هاست. هیچ شناسه‌ی پاسخ‌گو
/// در فایل‌های تولیدشده قرار نمی‌گیرد.
///
/// **اجرای خودکار:** <see cref="ProcessDueReportsAsync"/> توسط یک سرویس پس‌زمینه
/// زمان‌بندی‌شده (در صورت فعال‌سازی <c>Reports:EnableScheduler</c>) فراخوانی
/// می‌شود تا تعاریف فعال در زمان مقرر اجرا شوند.
/// </summary>
public interface IReportingService
{
    /// <summary>جستجوی صفحه‌بندی‌شده‌ی تعاریف گزارش.</summary>
    Task<PagedResult<ReportDefinitionDto>> SearchAsync(ReportSearchRequest request, CancellationToken ct = default);

    /// <summary>دریافت یک تعریف گزارش با شناسه.</summary>
    Task<Result<ReportDefinitionDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>ایجاد تعریف گزارش جدید.</summary>
    Task<Result<ReportDefinitionDto>> CreateAsync(SaveReportRequest request, CancellationToken ct = default);

    /// <summary>ویرایش یک تعریف گزارش.</summary>
    Task<Result<ReportDefinitionDto>> UpdateAsync(Guid id, SaveReportRequest request, CancellationToken ct = default);

    /// <summary>فعال‌سازی تعریف (اجرا در زمان‌بندی‌های رسیده).</summary>
    Task<Result<ReportDefinitionDto>> ActivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>بایگانی تعریف (حذف نرم: دیگر اجرا نمی‌شود).</summary>
    Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// اجرای فوری یک تعریف گزارش و تولید خروجی آن. ردیف اجرا پیش از شروع
    /// ذخیره می‌شود تا حتی در صورت شکست، تاریخچه باقی بماند.
    /// </summary>
    Task<Result<ReportExecutionDto>> ExecuteAsync(Guid reportDefinitionId, CancellationToken ct = default);

    /// <summary>دریافت یک اجرا با شناسه (برای نمایش وضعیت و دانلود).</summary>
    Task<Result<ReportExecutionDto>> GetExecutionAsync(Guid executionId, CancellationToken ct = default);

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی اجراهای گزارش.</summary>
    Task<PagedResult<ReportExecutionDto>> SearchExecutionsAsync(
        ExecutionSearchRequest request, CancellationToken ct = default);

    /// <summary>
    /// دریافت جریان فایل خروجی یک اجرای موفق برای دانلود.
    /// </summary>
    Task<Result<ReportArtifact>> GetArtifactAsync(Guid executionId, CancellationToken ct = default);

    /// <summary>
    /// پردازش تعاریف زمان‌بندی‌شده‌ای که زمان اجرایشان رسیده. این متد توسط
    /// زمان‌بند پس‌زمینه فراخوانی می‌شود و در خارج آن نباید استفاده شود.
    /// </summary>
    Task<int> ProcessDueReportsAsync(DateTime asOf, CancellationToken ct = default);
}
