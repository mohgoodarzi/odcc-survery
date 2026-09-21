namespace ODCC.Application.Abstractions;

/// <summary>
/// قرارداد تحلیلات هوش مصنوعی روی پاسخ‌های متنی (نظرات باز).
///
/// طراحی: این قرارداد در لایه‌ی کاربرد قرار دارد و پیاده‌سازی واقعی آن یک «ارائه‌دهنده‌ی
/// قابل تعویض» در لایه‌ی زیرساخت است. در فاز صفر هیچ ارائه‌دهنده‌ی خارجی انتخاب
/// یا یکپارچه نشده است؛ سامانه با پیاده‌سازی پیش‌فرض (No-op) کار می‌کند تا وابستگی
/// به هیچ سرویس خارجی ایجاد نشود. انتخاب ارائه‌دهنده (مثلاً سرویس ابری یا مدل محلی)
/// در فاز ۷ و تنها پس از تأیید امنیت و حریم خصوصی انجام می‌شود.
///
/// قواعد امنیتی:
/// - هیچ متنِ حاوی اطلاعات هویتی پاسخ‌دهنده‌ی نظرسنجی‌های ناشناس نباید به این سرویس ارسال شود.
/// - فراخوانی‌ها باید ناشناس‌شده (Anonymized) باشند و در صورت لزوم روی کلاستر داخلی اجرا شوند.
/// </summary>
public interface IAnalyticsAiService
{
    /// <summary>
    /// تحلیل احساس یک متن فارسی یا انگلیسی.
    /// </summary>
    Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// استخراج مضامین/کلیدواژه‌های پرتکرار از مجموعه‌ای از متن‌ها.
    /// </summary>
    Task<IReadOnlyList<ThemeExtractionResult>> ExtractThemesAsync(
        IReadOnlyCollection<string> texts,
        CancellationToken ct = default);

    /// <summary>
    /// تولید خلاصه‌ی خودکار از نتایج یک نظرسنجی.
    /// </summary>
    Task<string> GenerateInsightsSummaryAsync(AnalyticsInsightsRequest request, CancellationToken ct = default);
}

/// <summary>احساس شناسایی‌شده.</summary>
public enum Sentiment
{
    /// <summary>نامشخص / خنثی</summary>
    Unknown = 0,

    /// <summary>مثبت</summary>
    Positive = 1,

    /// <summary>خنثی</summary>
    Neutral = 2,

    /// <summary>منفی</summary>
    Negative = 3
}

/// <summary>نتیجه‌ی تحلیل احساس یک متن.</summary>
public sealed record SentimentAnalysisResult(Sentiment Sentiment, double Confidence, string? DetectedLanguage);

/// <summary>یک مضمون استخراج‌شده همراه با میزان تکرار.</summary>
public sealed record ThemeExtractionResult(string Theme, int OccurrenceCount, double RelevanceScore);

/// <summary>درخواست تولید خلاصه‌ی هوشمند از نتایج نظرسنجی.</summary>
public sealed record AnalyticsInsightsRequest(Guid SurveyId, string SurveyTitle, IReadOnlyDictionary<string, double> KeyMetrics);
