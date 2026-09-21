using ODCC.Application.Abstractions;

namespace ODCC.Infrastructure.Services;

/// <summary>
/// پیاده‌سازی پیش‌فرض و بدون اثر <see cref="IAnalyticsAiService"/>.
///
/// این کلاس تا زمان انتخاب و یکپارچه‌سازی یک ارائه‌دهنده‌ی خارجی (فاز ۷) استفاده می‌شود
/// تا هیچ وابستگی به سرویس خارجی در فاز صفر ایجاد نشود. نتایج خنثی برمی‌گرداند
/// و هیچ داده‌ای از سامانه خارج نمی‌شود.
/// </summary>
public sealed class NoOpAnalyticsAiService : IAnalyticsAiService
{
    public Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(new SentimentAnalysisResult(Sentiment.Unknown, Confidence: 0d, DetectedLanguage: null));

    public Task<IReadOnlyList<ThemeExtractionResult>> ExtractThemesAsync(
        IReadOnlyCollection<string> texts, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ThemeExtractionResult>>([]);

    public Task<string> GenerateInsightsSummaryAsync(AnalyticsInsightsRequest request, CancellationToken ct = default) =>
        Task.FromResult(string.Empty);
}
