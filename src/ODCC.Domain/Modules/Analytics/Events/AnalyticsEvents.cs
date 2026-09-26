using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Enums;

namespace ODCC.Domain.Modules.Analytics.Events;

/// <summary>
/// رویداد دامنه‌ی «محاسبه‌ی تحلیلات یک نظرسنجی».
///
/// **چرا این رویداد منتشر می‌شود:** ماژول تحلیلات پس از محاسبه‌ی شاخص‌ها این
/// رویداد را منتشر می‌کند تا ماژول‌های آینده (گزارش‌گیری، اعلان‌ها، برنامه‌ی
/// اقدام) بتوانند واکنش نشان دهند — مثلاً هشدار وقتی NPS از بنچمارک پایین‌تر
/// می‌رود. ماژول تحلیلات از وجود آن‌ها بی‌خبر است.
///
/// **حریم خصوصی:** این رویاد فقط شامل شاخص‌های تجمعی است و هرگز شناسه‌ی
/// پاسخ‌گو را حمل نمی‌کند.
/// </summary>
public sealed class AnalyticsComputedEvent : DomainEvent
{
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;

    /// <summary>بُعد بخش‌بندی این محاسبه.</summary>
    public AnalyticsSegment SegmentType { get; init; }

    /// <summary>شناسه‌ی ردیف تحلیلات تولیدشده (برای پیگیری در گزارش‌ها).</summary>
    public Guid MetricId { get; init; }

    /// <summary>تعداد کل نشست‌های شامل‌شده در این محاسبه.</summary>
    public int TotalSessions { get; init; }

    /// <summary>تعداد نشست‌های ارسال‌شده.</summary>
    public int CompletedSessions { get; init; }

    /// <summary>امتیاز NPS محاسبه‌شده (ممکن است <c>null</c> باشد).</summary>
    public decimal? NpsScore { get; init; }

    /// <summary>امتیاز CSAT محاسبه‌شده (ممکن است <c>null</c> باشد).</summary>
    public decimal? CsatScore { get; init; }

    /// <summary>امتیاز CES محاسبه‌شده (ممکن است <c>null</c> باشد).</summary>
    public decimal? CesScore { get; init; }

    /// <summary>کاربری که محاسبه را راه‌اندازی کرده (در محاسبه‌ی خودکار <c>null</c>).</summary>
    public Guid? ActorUserId { get; init; }

    public AnalyticsComputedEvent(
        Guid surveyId,
        string surveyCode,
        AnalyticsSegment segmentType,
        Guid metricId,
        int totalSessions,
        int completedSessions,
        decimal? npsScore,
        decimal? csatScore,
        decimal? cesScore,
        Guid? actorUserId)
    {
        SurveyId = surveyId;
        SurveyCode = surveyCode;
        SegmentType = segmentType;
        MetricId = metricId;
        TotalSessions = totalSessions;
        CompletedSessions = completedSessions;
        NpsScore = npsScore;
        CsatScore = csatScore;
        CesScore = cesScore;
        ActorUserId = actorUserId;
    }
}
