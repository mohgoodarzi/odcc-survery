using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Enums;

namespace ODCC.Application.Modules.Analytics.Abstractions;

/// <summary>
/// سرویس تحلیلات و داشبورد.
///
/// **مسئولیت:** محاسبه‌ی شاخص‌های تجمعی (NPS، CSAT، CES، نرخ تکمیل، توزیع
/// گزینه‌ها، روند زمانی) از پاسخ‌های ثبت‌شده، ذخیره‌ی آن‌ها به‌عنوان عکس‌العمل
/// خواندنی، و ارائه‌ی داشبورد.
///
/// **حریم خصوصی (حیاتی):** این سرویس <b>هرگز</b> شناسه‌ی پاسخ‌گو (کاربر، کارمند
/// یا نام) را در خروجی‌ها فاش نمی‌کند. فقط تجمع‌ها بازگردانده می‌شوند. برای
/// نظرسنجی‌های ناشناس، بخش‌بندی سازمانی در دسترس نیست چون هیچ پیوندی به
/// ساختار سازمانی ذخیره نشده است؛ این یک ویژگی طراحی است، نه یک محدودیت.
///
/// **مرزهای ماژول‌ها:** این سرویس برای خواندن پاسخ‌ها فقط از قرارداد
/// <c>IResponseRepository</c> و برای ساختار پرسشنامه فقط از <c>IQuestionnaireService</c>
/// و <c>IQuestionRepository</c> استفاده می‌کند — هرگز از DbContext آن ماژول‌ها.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// محاسبه (یا محاسبه‌ی مجدد) تحلیلات یک نظرسنجی و ذخیره‌ی عکس‌العمل آن.
    /// این عملیات پس از هر ارسال پاسخ به‌صورت خودکار توسط شنونده‌ی رویداد
    /// فراخوانی می‌شود، ولی مدیران هم می‌توانند آن را دستی اجرا کنند.
    /// </summary>
    Task<Result<SurveyAnalyticsDto>> ComputeAsync(ComputeAnalyticsRequest request, CancellationToken ct = default);

    /// <summary>
    /// دریافت تحلیلات ذخیره‌شده‌ی یک نظرسنجی بدون محاسبه‌ی مجدد. اگر عکس‌العملی
    /// وجود نداشته باشد، <c>null</c> برمی‌گرداند (مرز نمایش: کلاینت باید محاسبه کند).
    /// </summary>
    Task<Result<SurveyAnalyticsDto>> GetAsync(Guid surveyId, AnalyticsFilter filter, CancellationToken ct = default);

    /// <summary>داشبورد تحلیلات سطح شرکت (با در نظر گرفتن دامنه‌ی سازمانی کاربر).</summary>
    Task<Result<AnalyticsDashboardDto>> GetDashboardAsync(AnalyticsFilter filter, CancellationToken ct = default);

    /// <summary>روند زمانی ارسال پاسخ‌ها.</summary>
    Task<Result<IReadOnlyList<TrendPointDto>>> GetTrendAsync(TrendRequest request, CancellationToken ct = default);

    /// <summary>جستجوی عکس‌العمل‌های ذخیره‌شده.</summary>
    Task<PagedResult<SurveyMetricSummaryDto>> SearchAsync(AnalyticsSearchRequest request, CancellationToken ct = default);

    /// <summary>
    /// بخش‌بندی پاسخ‌های یک نظرسنجی بر اساس واحد سازمانی. برای نظرسنجی‌های
    /// ناشناس لیست خالی برمی‌گردد (هیچ پیوند سازمانی وجود ندارد).
    /// </summary>
    Task<Result<IReadOnlyList<AnalyticsSegmentDto>>> GetSegmentsByOrgUnitAsync(
        Guid surveyId,
        AnalyticsFilter filter,
        CancellationToken ct = default);

    /// <summary>مقایسه‌ی شاخص‌های یک نظرسنجی با بنچمارک‌های قابل‌اعمال.</summary>
    Task<Result<IReadOnlyList<BenchmarkComparisonDto>>> CompareWithBenchmarksAsync(
        Guid surveyId,
        CancellationToken ct = default);
}

/// <summary>
/// سرویس مدیریت بنچمارک‌ها (اهداف مرجع).
/// </summary>
public interface IBenchmarkService
{
    Task<PagedResult<BenchmarkDto>> SearchAsync(string? searchText, bool includeInactive, CancellationToken ct = default);
    Task<Result<BenchmarkDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<BenchmarkDto>> CreateAsync(SaveBenchmarkRequest request, CancellationToken ct = default);
    Task<Result<BenchmarkDto>> UpdateAsync(Guid id, SaveBenchmarkRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
