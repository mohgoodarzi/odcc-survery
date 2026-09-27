using ODCC.Domain.Common;
using ODCC.Domain.Modules.Reporting.Entities;

namespace ODCC.Application.Modules.Reporting.Abstractions;

/// <summary>
/// قرارداد جمع‌آوری داده‌ی یک گزارش.
///
/// این لایه‌ی قراردادی، نقطه‌ی تماس ماژول گزارش‌گیری با ماژول تحلیلات است.
/// جمع‌آوری داده **فقط** از طریق <c>IAnalyticsService</c> و <c>IBenchmarkService</c>
/// انجام می‌شود (نه DbContext ماژول تحلیلات) تا مرز ماژول‌ها حفظ شود.
///
/// <b>حریم خصوصی:</b> داده‌ی جمع‌آوری‌شده فقط تجمع است؛ هیچ شناسه‌ی پاسخ‌گو
/// (کاربر، کارمند یا نام) وارد بسته نمی‌شود — حتی برای نظرسنجی‌های غیرناشناس.
/// </summary>
public interface IReportDataAssembler
{
    /// <summary>
    /// جمع‌آوری داده بر اساس دامنه‌ی تثبیت‌شده در تعریف گزارش.
    /// دامنه در زمان تعریف اعتبارسنجی و ثابت شده، بنابراین اجرای زمان‌بندی‌شده
    /// (بدون کاربر) دقیقاً همان دامنه را می‌بیند.
    /// </summary>
    Task<Result<ReportDataBundle>> AssembleAsync(ReportDefinition definition, CancellationToken ct = default);
}
