using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Response.Enums;

namespace ODCC.Domain.Modules.Analytics.Entities;

/// <summary>
/// عکس‌العمل محاسبه‌شده‌ی تحلیلات یک نظرسنجی در یک بُعد بخش‌بندی مشخص.
///
/// این موجودیت یک <b>مدل خواندنی</b> (read model) است: سرویس تحلیلات آن را
/// محاسبه و ذخیره می‌کند تا داشبوردها بدون محاسبه‌ی مجدد در هر درخواست سریع
/// بمانند. در هر بار محاسبه، مقادیر جایگزین می‌شوند (نه الحاق).
///
/// **حفظ حریم خصوصی:** این موجودیت هرگز شناسه‌ی پاسخ‌گو (کاربر، کارمند یا نام)
/// را ذخیره نمی‌کند — حتی برای نظرسنجی‌های غیرناشناس. فقط <b>تجمع‌ها</b>
/// (تعداد، میانگین، درصد) نگه داشته می‌شوند. برای نظرسنجی‌های ناشناس،
/// بُعد <see cref="AnalyticsSegment.OrgUnit"/> در دسترس نیست چون هیچ پیوندی به
/// ساختار سازمانی وجود ندارد.
/// </summary>
public class SurveyMetric : BaseEntity
{
    /// <summary>شناسه‌ی نظرسنجی.</summary>
    public Guid SurveyId { get; set; }

    /// <summary>کد نظرسنجی در زمان محاسبه (عکس‌العمل).</summary>
    public string SurveyCode { get; set; } = string.Empty;

    /// <summary>عنوان نظرسنجی در زمان محاسجه (عکس‌العمل).</summary>
    public string SurveyTitle { get; set; } = string.Empty;

    /// <summary>آیا نظرسنجی ناشناس است؟ در این صورت بخش‌بندی سازمانی ممکن نیست.</summary>
    public bool IsAnonymous { get; set; }

    /// <summary>بُعد بخش‌بندی این ردیف تحلیلی.</summary>
    public AnalyticsSegment SegmentType { get; set; } = AnalyticsSegment.Survey;

    /// <summary>شناسه‌ی واحد سازمانی (فقط برای <see cref="AnalyticsSegment.OrgUnit"/>).</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>مسیر مادی واحد سازمانی (برای فیلتر کردن کارآمد زیردرخت).</summary>
    public string? OrgUnitPath { get; set; }

    /// <summary>شناسه‌ی کمپین (فقط برای <see cref="AnalyticsSegment.Campaign"/>).</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>کد کمپین در زمان محاسبه (عکس‌العمل).</summary>
    public string? CampaignCode { get; set; }

    /// <summary>کانال ورود پاسخ (فقط برای <see cref="AnalyticsSegment.Source"/>).</summary>
    public ResponseSource? Source { get; set; }

    /// <summary>شروع پنجره‌ی زمانی محاسبه (شامل).</summary>
    public DateTime WindowStart { get; set; }

    /// <summary>پایان پنجره‌ی زمانی محاسبه (شامل).</summary>
    public DateTime WindowEnd { get; set; }

    // --- شمارش‌ها ----------------------------------------------------------

    /// <summary>تعداد کل نشست‌های (شروع‌شده) در این بُعد.</summary>
    public int TotalSessions { get; set; }

    /// <summary>تعداد نشست‌های ارسال‌شده در این بُعد.</summary>
    public int CompletedSessions { get; set; }

    /// <summary>نرخ تکمیل (درصد): CompletedSessions / TotalSessions.</summary>
    public decimal CompletionRate { get; set; }

    // --- NPS ---------------------------------------------------------------

    /// <summary>امتیاز NPS (۱۰۰- تا ۱۰۰) یا <c>null</c> اگر سؤال NPS وجود نداشته باشد.</summary>
    public decimal? NpsScore { get; set; }

    /// <summary>تعداد ترویج‌کنندگان (۹ و ۱۰).</summary>
    public int NpsPromoters { get; set; }

    /// <summary>تعداد خنثی‌ها (۷ و ۸).</summary>
    public int NpsPassives { get; set; }

    /// <summary>تعداد منتقدان (۱ تا ۶).</summary>
    public int NpsDetractors { get; set; }

    /// <summary>شناسه‌ی سؤالی که به‌عنوان NPS محاسبه شده (برای شفافیت).</summary>
    public Guid? NpsQuestionId { get; set; }

    // --- CSAT --------------------------------------------------------------

    /// <summary>امتیاز CSAT (درصد راضیان: ۴ و ۵ از ۵) یا <c>null</c>.</summary>
    public decimal? CsatScore { get; set; }

    /// <summary>تعداد پاسخ‌گویان CSAT.</summary>
    public int CsatRespondents { get; set; }

    /// <summary>شناسه‌ی سؤالی که به‌عنوان CSAT محاسبه شده.</summary>
    public Guid? CsatQuestionId { get; set; }

    // --- CES ---------------------------------------------------------------

    /// <summary>امتیاز CES (درصد کم‌تلاش‌ها: ۵ به بالا از ۷) یا <c>null</c>.</summary>
    public decimal? CesScore { get; set; }

    /// <summary>تعداد پاسخ‌گویان CES.</summary>
    public int CesRespondents { get; set; }

    /// <summary>شناسه‌ی سؤالی که به‌عنوان CES محاسبه شده.</summary>
    public Guid? CesQuestionId { get; set; }

    // --- امتیاز کلی --------------------------------------------------------

    /// <summary>میانگین همه‌ی پاسخ‌های امتیازدهی یا <c>null</c>.</summary>
    public decimal? AverageRating { get; set; }

    /// <summary>تعداد پاسخ‌های امتیازدهی.</summary>
    public int RatingRespondents { get; set; }

    // --- نرخ پاسخ (کمپین) --------------------------------------------------

    /// <summary>نرخ پاسخ‌گویی (درصد) یا <c>null</c> اگر کمپینی وجود نداشته باشد.</summary>
    public decimal? ResponseRate { get; set; }

    /// <summary>کل دعوت‌نامه‌های توزیع‌شده (مخرج نرخ پاسخ).</summary>
    public int TotalDistributions { get; set; }

    /// <summary>دعوت‌نامه‌های پاسخ‌داده‌شده.</summary>
    public int RespondedDistributions { get; set; }

    // --- تحلیل سطح سؤال ----------------------------------------------------

    /// <summary>
    /// تجزیه‌وتحلیل هر سؤال به‌صورت JSON ساختاریافته. سری‌سازی در لایه‌ی
    /// کاربرد انجام می‌شود تا دامنه بدون وابستگی به کتابخانه‌ی JSON بماند
    /// (همان الگوی <c>Changes</c> در ماژول ممیزی).
    /// </summary>
    public string QuestionMetrics { get; set; } = "{}";

    /// <summary>زمان آخرین محاسبه.</summary>
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// جایگزینی کامل مقادیر محاسبه‌شده. چون این یک مدل خواندنی است، به‌جای
    /// ویرایش مرحله‌ای، کل عکس‌العمل دوباره نوشته می‌شود.
    /// </summary>
    public void Update(SurveyMetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        TotalSessions = snapshot.TotalSessions;
        CompletedSessions = snapshot.CompletedSessions;
        CompletionRate = snapshot.CompletionRate;

        NpsScore = snapshot.NpsScore;
        NpsPromoters = snapshot.NpsPromoters;
        NpsPassives = snapshot.NpsPassives;
        NpsDetractors = snapshot.NpsDetractors;
        NpsQuestionId = snapshot.NpsQuestionId;

        CsatScore = snapshot.CsatScore;
        CsatRespondents = snapshot.CsatRespondents;
        CsatQuestionId = snapshot.CsatQuestionId;

        CesScore = snapshot.CesScore;
        CesRespondents = snapshot.CesRespondents;
        CesQuestionId = snapshot.CesQuestionId;

        AverageRating = snapshot.AverageRating;
        RatingRespondents = snapshot.RatingRespondents;

        ResponseRate = snapshot.ResponseRate;
        TotalDistributions = snapshot.TotalDistributions;
        RespondedDistributions = snapshot.RespondedDistributions;

        QuestionMetrics = snapshot.QuestionMetrics;
        ComputedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// مقادیر محاسبه‌شده‌ی یک بُعد تحلیلی که روی <see cref="SurveyMetric"/> اعمال می‌شود.
/// این نوع در دامنه است تا قواعد کسب‌وکار محاسبه در یک مکان متمرکز باشد.
/// </summary>
public sealed class SurveyMetricsSnapshot
{
    public int TotalSessions { get; init; }
    public int CompletedSessions { get; init; }
    public decimal CompletionRate { get; init; }

    public decimal? NpsScore { get; init; }
    public int NpsPromoters { get; init; }
    public int NpsPassives { get; init; }
    public int NpsDetractors { get; init; }
    public Guid? NpsQuestionId { get; init; }

    public decimal? CsatScore { get; init; }
    public int CsatRespondents { get; init; }
    public Guid? CsatQuestionId { get; init; }

    public decimal? CesScore { get; init; }
    public int CesRespondents { get; init; }
    public Guid? CesQuestionId { get; init; }

    public decimal? AverageRating { get; init; }
    public int RatingRespondents { get; init; }

    public decimal? ResponseRate { get; init; }
    public int TotalDistributions { get; init; }
    public int RespondedDistributions { get; init; }

    /// <summary>تجزیه‌وتحلیل هر سؤال به‌صورت JSON سری‌سازی‌شده.</summary>
    public string QuestionMetrics { get; init; } = "{}";
}
