using ODCC.Application.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Response.Enums;

namespace ODCC.Application.Modules.Analytics.Dtos;

// --- درخواست‌ها -----------------------------------------------------------

/// <summary>فیلتر مشترک برای همه‌ی پرس‌وجوهای تحلیلات.</summary>
public sealed record AnalyticsFilter
{
    /// <summary>شروع پنجره‌ی زمانی (شامل). <c>null</c> یعنی بدون محدودیت.</summary>
    public DateTime? From { get; init; }

    /// <summary>پایان پنجره‌ی زمانی (شامل). <c>null</c> یعنی بدون محدودیت.</summary>
    public DateTime? To { get; init; }

    /// <summary>محدود کردن به پاسخ‌های یک کمپین مشخص.</summary>
    public Guid? CampaignId { get; init; }

    /// <summary>
    /// شناسه‌ی واحد سازمانی برای فیلتر کردن. با <see cref="IncludeDescendants"/>
    /// زیردرخت آن هم شامل می‌شود. فقط برای نظرسنجی‌های غیرناشناس معتبر است
    /// (در نظرسنجی‌های ناشناس هیچ پیوند سازمانی وجود ندارد).
    /// </summary>
    public Guid? OrgUnitId { get; init; }

    /// <summary>آیا زیرمجموعه‌های واحد سازمانی هم شامل شوند؟</summary>
    public bool IncludeDescendants { get; init; } = true;
}

/// <summary>درخواست محاسبه‌ی تحلیلات یک نظرسنجی.</summary>
public sealed record ComputeAnalyticsRequest
{
    public required Guid SurveyId { get; init; }

    public AnalyticsFilter Filter { get; init; } = new();

    /// <summary>
    /// بُعد بخش‌بندی برای محاسبه. <see cref="AnalyticsSegment.Survey"/> کل نظرسنجی
    /// را به‌صورت یکپارچه محاسبه می‌کند.
    /// </summary>
    public AnalyticsSegment SegmentType { get; init; } = AnalyticsSegment.Survey;

    /// <summary>
    /// شناسه‌ی واحد سازمانی مقصد (فقط برای <see cref="AnalyticsSegment.OrgUnit"/>).
    /// </summary>
    public Guid? OrgUnitId { get; init; }

    /// <summary>شناسه‌ی کمپین مقصد (فقط برای <see cref="AnalyticsSegment.Campaign"/>).</summary>
    public Guid? CampaignId { get; init; }

    /// <summary>
    /// آیا سؤال‌های متنی به سرویس هوش مصنوعی ارسال شوند تا احساس و مضامین
    /// استخراج شود؟ پیش‌فرض <c>false</c> است تا در صورت نبودن ارائه‌دهنده
    /// هزینه نشود. برای نظرسنجی‌های ناشناس فقط متن (بدون هیچ شناسه‌ای) ارسال می‌شود.
    /// </summary>
    public bool IncludeTextAnalytics { get; init; }
}

/// <summary>درخواست جستجوی تحلیلات ذخیره‌شده.</summary>
public sealed record AnalyticsSearchRequest
{
    public string? SearchText { get; init; }
    public Guid? SurveyId { get; init; }
    public AnalyticsSegment? SegmentType { get; init; }
    public bool IncludeArchived { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>درخواست ایجاد یا ویرایش یک بنچمارک.</summary>
public sealed record SaveBenchmarkRequest
{
    public string Name { get; init; } = string.Empty;
    public MetricType Metric { get; init; } = MetricType.Nps;
    public decimal TargetValue { get; init; }

    /// <summary><c>true</c> برای بنچمارک سراسری شرکت.</summary>
    public bool IsCompanyWide { get; init; } = true;

    /// <summary>شناسه‌ی واحد سازمانی (فقط اگر سراسری نباشد).</summary>
    public Guid? OrgUnitId { get; init; }

    public string? Description { get; init; }
}

// --- خروجی‌ها --------------------------------------------------------------

/// <summary>خلاصه‌ی شاخص‌های یک نظرسنجی برای داشبورد.</summary>
public sealed record SurveyMetricSummaryDto
{
    public Guid Id { get; init; }
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string SurveyTitle { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }
    public AnalyticsSegment SegmentType { get; init; }
    public string? SegmentLabel { get; init; }

    public int TotalSessions { get; init; }
    public int CompletedSessions { get; init; }
    public decimal CompletionRate { get; init; }

    public decimal? NpsScore { get; init; }
    public decimal? CsatScore { get; init; }
    public decimal? CesScore { get; init; }
    public decimal? AverageRating { get; init; }

    /// <summary>نرخ پاسخ‌گویی (فقط برای بخش‌بندی کمپین).</summary>
    public decimal? ResponseRate { get; init; }

    public DateTime WindowStart { get; init; }
    public DateTime WindowEnd { get; init; }
    public DateTime ComputedAt { get; init; }
}

/// <summary>یک سؤال با توزیع پاسخ‌های آن.</summary>
public sealed record QuestionMetricDto
{
    public Guid QuestionId { get; init; }
    public string QuestionCode { get; init; } = string.Empty;
    public string QuestionText { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; }
    public int DisplayOrder { get; init; }

    /// <summary>تعداد پاسخ‌های دارای مقدار برای این سؤال.</summary>
    public int ResponseCount { get; init; }

    /// <summary>درصد پاسخ‌گویانی که این سؤال را پاسخ داده‌اند (در برابر کل).</summary>
    public decimal ResponseRate { get; init; }

    /// <summary>توزیع گزینه‌ها (فقط سؤال‌های گزینه‌ای).</summary>
    public IReadOnlyList<OptionDistributionDto> Options { get; init; } = [];

    /// <summary>آمار عددی (فقط سؤال‌های امتیازدهی و عددی).</summary>
    public NumericStatsDto? NumericStats { get; init; }

    /// <summary>توزیع بله/خیر (فقط سؤال‌های بله/خیر).</summary>
    public YesNoDistributionDto? YesNoDistribution { get; init; }

    /// <summary>نتایج هوش مصنوعی روی متن (فقط سؤال‌های متنی و در صورت درخواست).</summary>
    public TextAnalyticsDto? TextAnalytics { get; init; }

    /// <summary>آیا این سؤال به‌عنوان سؤال NPS/CSAT/CES شناسایی شده؟</summary>
    public MetricType? DetectedMetric { get; init; }
}

/// <summary>توزیع یک گزینه در یک سؤال گزینه‌ای.</summary>
public sealed record OptionDistributionDto
{
    public Guid OptionId { get; init; }
    public string OptionCode { get; init; } = string.Empty;
    public string OptionText { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }

    /// <summary>تعداد انتخاب این گزینه. در سؤال‌های چندانتخابی، مجموع می‌تواند
    /// از تعداد پاسخ‌گویان بیشتر باشد.</summary>
    public int Count { get; init; }

    /// <summary>درصد پاسخ‌گویانی که این گزینه را انتخاب کرده‌اند.</summary>
    public decimal Percentage { get; init; }
}

/// <summary>آمار توصیفی پاسخ‌های عددی/امتیازدهی.</summary>
public sealed record NumericStatsDto
{
    public decimal? Mean { get; init; }
    public decimal? Median { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public decimal? StandardDeviation { get; init; }
    public int ResponseCount { get; init; }

    /// <summary>توزیع هر مقدار امتیاز (فقط سؤال‌های امتیازدهی).</summary>
    public IReadOnlyList<RatingBucketDto> RatingBuckets { get; init; } = [];
}

/// <summary>یک.bucket از طیف امتیازدهی.</summary>
public sealed record RatingBucketDto
{
    public decimal Value { get; init; }
    public int Count { get; init; }
    public decimal Percentage { get; init; }
}

/// <summary>توزیع بله/خیر.</summary>
public sealed record YesNoDistributionDto
{
    public int YesCount { get; init; }
    public int NoCount { get; init; }
    public decimal YesPercentage { get; init; }
    public decimal NoPercentage { get; init; }
}

/// <summary>نتایج تحلیل متن با هوش مصنوعی.</summary>
public sealed record TextAnalyticsDto
{
    public int TextResponseCount { get; init; }

    /// <summary>توزیع احساسات (درصد).</summary>
    public SentimentDistributionDto Sentiment { get; init; } = new();

    /// <summary>مضامین استخراج‌شده بر اساس میزان تکرار.</summary>
    public IReadOnlyList<ThemeExtractionResult> Themes { get; init; } = [];
}

/// <summary>توزیع احساسات.</summary>
public sealed record SentimentDistributionDto
{
    public decimal PositivePercentage { get; init; }
    public decimal NeutralPercentage { get; init; }
    public decimal NegativePercentage { get; init; }
    public decimal UnknownPercentage { get; init; }
    public int TotalCount { get; init; }
}

/// <summary>خروجی کامل تحلیلات یک نظرسنجی.</summary>
public sealed record SurveyAnalyticsDto
{
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string SurveyTitle { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }

    /// <summary>
    /// آیا بخش‌بندی سازمانی برای این نظرسنجی ممکن است؟ برای نظرسنجی‌های
    /// ناشناس <c>false</c> است چون هیچ پیوند سازمانی ذخیره نشده است.
    /// </summary>
    public bool CanSegmentByOrgUnit { get; init; }

    public AnalyticsFilter AppliedFilter { get; init; } = new();

    public int TotalSessions { get; init; }
    public int CompletedSessions { get; init; }
    public decimal CompletionRate { get; init; }

    public decimal? NpsScore { get; init; }
    public int NpsPromoters { get; init; }
    public int NpsPassives { get; init; }
    public int NpsDetractors { get; init; }

    public decimal? CsatScore { get; init; }
    public decimal? CesScore { get; init; }
    public decimal? AverageRating { get; init; }

    public IReadOnlyList<QuestionMetricDto> Questions { get; init; } = [];

    /// <summary>روند ارسال پاسخ‌ها در طول زمان (بر اساس پارامتر درخواست).</summary>
    public IReadOnlyList<TrendPointDto> Trend { get; init; } = [];

    public DateTime ComputedAt { get; init; }

    /// <summary>شناسه‌ی ردیف تحلیلات ذخیره‌شده (برای ارجاع در گزارش‌ها).</summary>
    public Guid? MetricId { get; init; }
}

/// <summary>یک نقطه از روند زمانی.</summary>
public sealed record TrendPointDto
{
    /// <summary>برچسب دوره (مثلاً «۱۴۰۴/۰۷/۰۱» یا «هفته ۳۰»).</summary>
    public string PeriodLabel { get; init; } = string.Empty;

    /// <summary>شروع دوره (برای مرتب‌سازی).</summary>
    public DateTime PeriodStart { get; init; }

    /// <summary>تعداد ارسال در این دوره.</summary>
    public int Count { get; init; }

    /// <summary>تعداد تجمعی تا این دوره.</summary>
    public int CumulativeCount { get; init; }

    /// <summary>میانگین امتیاز در این دوره (در صورت وجود).</summary>
    public decimal? AverageRating { get; init; }

    /// <summary>نرخ تکمیل این دوره (درصد).</summary>
    public decimal? CompletionRate { get; init; }
}

/// <summary>درخواست روند زمانی.</summary>
public sealed record TrendRequest
{
    public Guid? SurveyId { get; init; }
    public AnalyticsPeriod Period { get; init; } = AnalyticsPeriod.Day;
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

/// <summary>خروجی یک بنچمارک.</summary>
public sealed record BenchmarkDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public MetricType Metric { get; init; }
    public decimal TargetValue { get; init; }
    public bool IsCompanyWide { get; init; }
    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitPath { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>مقایسه‌ی یک شاخص با بنچمارک‌های قابل‌اعمال.</summary>
public sealed record BenchmarkComparisonDto
{
    public MetricType Metric { get; init; }
    public decimal? ActualValue { get; init; }
    public decimal TargetValue { get; init; }
    public string BenchmarkName { get; init; } = string.Empty;

    /// <summary>اختلاف واقعی منهای هدف.</summary>
    public decimal? Delta => ActualValue.HasValue ? ActualValue.Value - TargetValue : null;

    /// <summary>آیا مقدار واقعی از هدف بالاتر است؟</summary>
    public bool? IsAboveTarget => Delta.HasValue ? Delta > 0 : null;
}

/// <summary>خلاصه‌ی تحلیلات سطح شرکت (داشبورد اصلی).</summary>
public sealed record AnalyticsDashboardDto
{
    public int TotalSurveys { get; init; }
    public int ActiveSurveys { get; init; }
    public int TotalCampaigns { get; init; }
    public int ActiveCampaigns { get; init; }

    /// <summary>کل نشست‌های پاسخ‌گویی در بازه‌ی قابل‌مشاهده.</summary>
    public int TotalSessions { get; init; }
    public int CompletedSessions { get; init; }
    public decimal CompletionRate { get; init; }

    /// <summary>میانگین NPS در همه‌ی نظرسنجی‌ها (در صورت وجود).</summary>
    public decimal? AverageNps { get; init; }
    public decimal? AverageCsat { get; init; }
    public decimal? AverageCes { get; init; }
    public decimal? AverageRating { get; init; }

    /// <summary>روند کلی ارسال پاسخ‌ها.</summary>
    public IReadOnlyList<TrendPointDto> Trend { get; init; } = [];

    /// <summary>پربازدیدترین نظرسنجی‌ها بر اساس تعداد ارسال.</summary>
    public IReadOnlyList<SurveyMetricSummaryDto> TopSurveys { get; init; } = [];

    /// <summary>بازه‌ی زمانی این داشبورد.</summary>
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

/// <summary>یک بخش (segment) از پاسخ‌ها با برچسب نمایشی.</summary>
public sealed record AnalyticsSegmentDto
{
    public string Label { get; init; } = string.Empty;
    public int TotalSessions { get; init; }
    public int CompletedSessions { get; init; }
    public decimal CompletionRate { get; init; }
    public decimal? AverageRating { get; init; }
}
