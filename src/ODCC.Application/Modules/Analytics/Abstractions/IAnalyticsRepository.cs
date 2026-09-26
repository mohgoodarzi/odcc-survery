using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Domain.Modules.Analytics.Entities;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Response.Enums;
using SurveyMetricEntity = ODCC.Domain.Modules.Analytics.Entities.SurveyMetric;

namespace ODCC.Application.Modules.Analytics.Abstractions;

/// <summary>
/// مخزن اختصاصی عکس‌العمل‌های تحلیلی (مدل خواندنی).
/// </summary>
public interface IAnalyticsRepository : IRepository<SurveyMetricEntity>
{
    /// <summary>
    /// یافتن عکس‌العمل یک نظرسنجی در یک بُعد بخش‌بندی مشخص. هر نظرسنجی در هر
    /// بُعد فقط یک عکس‌العمل دارد (جایگزینی در هر محاسبه).
    /// </summary>
    Task<SurveyMetricEntity?> FindAsync(
        Guid surveyId,
        AnalyticsSegment segmentType,
        Guid? orgUnitId,
        Guid? campaignId,
        ResponseSource? source,
        CancellationToken ct = default);

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی عکس‌العمل‌های ذخیره‌شده.</summary>
    Task<IReadOnlyList<SurveyMetricEntity>> SearchAsync(AnalyticsSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد عکس‌العمل‌های مطابق با فیلتر.</summary>
    Task<int> CountAsync(AnalyticsSearchRequest request, CancellationToken ct = default);

    /// <summary>
    /// همه‌ی عکس‌العمل‌های یک نظرسنجی (برای محاسبه‌ی میانگین داشبورد).
    /// </summary>
    Task<IReadOnlyList<SurveyMetricEntity>> ListBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    /// <summary>حذف تمام عکس‌العمل‌های یک نظرسنجی (در زمان بایگانی نظرسنجی).</summary>
    Task RemoveBySurveyAsync(Guid surveyId, CancellationToken ct = default);
}

/// <summary>
/// مخزن اختصاصی بنچمارک‌ها.
/// </summary>
public interface IBenchmarkRepository : IRepository<Benchmark>
{
    /// <summary>جستجوی صفحه‌بندی‌شده‌ی بنچمارک‌ها.</summary>
    Task<IReadOnlyList<Benchmark>> SearchAsync(string? searchText, bool includeInactive, CancellationToken ct = default);

    /// <summary>
    /// بنچمارک‌های فعال قابل‌اعمال برای یک شاخص و (اختیاری) یک واحد سازمانی.
    /// بنچمارک سراسری شرکت + بنچمارک مخصوص همان واحد (یا اجداد آن) بازگردانده می‌شود.
    /// </summary>
    Task<IReadOnlyList<Benchmark>> GetApplicableAsync(
        MetricType metric,
        string? orgUnitPath,
        CancellationToken ct = default);
}
