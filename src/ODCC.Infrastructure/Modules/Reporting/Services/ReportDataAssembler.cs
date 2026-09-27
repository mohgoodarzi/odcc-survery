using System.Globalization;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Reporting.Entities;
using ODCC.Domain.Modules.Reporting.Enums;
using ODCC.Application.Modules.Reporting.Dtos;

namespace ODCC.Infrastructure.Modules.Reporting.Services;

/// <summary>
/// جمع‌آوری داده‌ی گزارش از ماژول تحلیلات.
///
/// **مرز ماژول‌ها:** تمام خواندن‌ها از طریق <see cref="IAnalyticsService"/> انجام
/// می‌شود — هرگز از DbContext ماژول تحلیلات. این تنها مسیر مجاز است.
///
/// **حریم خصوصی:** داده‌های ورودی فقط تجمع هستند (<see cref="SurveyAnalyticsDto"/>،
/// <see cref="BenchmarkComparisonDto"/> و...) و هیچ شناسه‌ی پاسخ‌گویی ندارند؛
/// چیزی هم به بسته اضافه نمی‌شود.
/// </summary>
public sealed class ReportDataAssembler(
    IAnalyticsService analyticsService) : IReportDataAssembler
{
    private readonly IAnalyticsService _analyticsService = analyticsService;

    /// <inheritdoc/>
    public async Task<Result<ReportDataBundle>> AssembleAsync(
        ReportDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var filter = new AnalyticsFilter
        {
            From = definition.From,
            To = definition.To,
            OrgUnitId = definition.OrgUnitId,
            IncludeDescendants = definition.IncludeDescendants
        };

        var sections = new List<ReportSection>();

        if (definition.Type == ReportType.DashboardSummary)
        {
            var dashboard = await _analyticsService.GetDashboardAsync(filter, ct);
            if (dashboard.IsFailure)
            {
                return Result.Failure<ReportDataBundle>(dashboard.Error);
            }

            sections.AddRange(BuildDashboardSections(dashboard.Value!));
        }
        else
        {
            if (definition.SurveyId is not { } surveyId)
            {
                return Result.Failure<ReportDataBundle>(
                    "report_survey_required", "این نوع گزارش نیاز به انتخاب نظرسنجی دارد.");
            }

            var analytics = await _analyticsService.GetAsync(surveyId, filter, ct);
            if (analytics.IsFailure)
            {
                return Result.Failure<ReportDataBundle>(analytics.Error);
            }

            sections.AddRange(BuildSurveySections(analytics.Value!));

            if (definition.Type == ReportType.BenchmarkComparison)
            {
                var comparison = await _analyticsService.CompareWithBenchmarksAsync(surveyId, ct);
                if (comparison.IsFailure)
                {
                    return Result.Failure<ReportDataBundle>(comparison.Error);
                }

                sections.AddRange(BuildBenchmarkSections(comparison.Value!));
            }
        }

        var bundle = new ReportDataBundle
        {
            Title = definition.Name,
            Subtitle = BuildSubtitle(definition),
            Type = definition.Type,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = definition.OwnerUserName ?? ReportLabels.SystemScheduler,
            Sections = sections
        };

        return Result.Success(bundle);
    }

    // --- بخش‌های داشبورد -----------------------------------------------------

    private static List<ReportSection> BuildDashboardSections(AnalyticsDashboardDto dashboard)
    {
        var sections = new List<ReportSection>();

        sections.Add(new ReportSection
        {
            Title = ReportLabels.DashboardOverview,
            Columns =
            [
                new(ReportLabels.Metric, 220, ReportColumnType.Text),
                new(ReportLabels.Value, 120, ReportColumnType.Number)
            ],
            Rows =
            [
                [ReportLabels.TotalSurveys, dashboard.TotalSurveys],
                [ReportLabels.ActiveSurveys, dashboard.ActiveSurveys],
                [ReportLabels.TotalCampaigns, dashboard.TotalCampaigns],
                [ReportLabels.ActiveCampaigns, dashboard.ActiveCampaigns],
                [ReportLabels.TotalSessions, dashboard.TotalSessions],
                [ReportLabels.CompletedSessions, dashboard.CompletedSessions],
                [ReportLabels.CompletionRate, Round1(dashboard.CompletionRate)],
                [ReportLabels.AverageNps, Round1(dashboard.AverageNps)],
                [ReportLabels.AverageCsat, Round1(dashboard.AverageCsat)],
                [ReportLabels.AverageCes, Round1(dashboard.AverageCes)],
                [ReportLabels.AverageRating, Round1(dashboard.AverageRating)]
            ]
        });

        if (dashboard.TopSurveys.Count > 0)
        {
            sections.Add(new ReportSection
            {
                Title = ReportLabels.TopSurveys,
                Columns =
                [
                    new(ReportLabels.SurveyTitle, 260, ReportColumnType.Text),
                    new(ReportLabels.SurveyCode, 110, ReportColumnType.Text),
                    new(ReportLabels.CompletedSessions, 90, ReportColumnType.Number),
                    new(ReportLabels.CompletionRate, 90, ReportColumnType.Percent),
                    new(ReportLabels.AverageNps, 80, ReportColumnType.Number),
                    new(ReportLabels.AverageCsat, 80, ReportColumnType.Number)
                ],
                Rows = dashboard.TopSurveys.Select(s => Row(
                     s.SurveyTitle, s.SurveyCode, s.CompletedSessions,
                     Round1(s.CompletionRate), Round1(s.NpsScore), Round1(s.CsatScore))).ToList()
            });
        }

        if (dashboard.Trend.Count > 0)
        {
            sections.Add(BuildTrendSection(dashboard.Trend));
        }

        return sections;
    }

    // --- بخش‌های یک نظرسنجی ---------------------------------------------------

    private static List<ReportSection> BuildSurveySections(SurveyAnalyticsDto analytics)
    {
        var sections = new List<ReportSection>();

        sections.Add(new ReportSection
        {
            Title = ReportLabels.KeyMetrics,
            Columns =
            [
                new(ReportLabels.Metric, 260, ReportColumnType.Text),
                new(ReportLabels.Value, 120, ReportColumnType.Number)
            ],
            Rows =
            [
                [ReportLabels.TotalSessions, analytics.TotalSessions],
                [ReportLabels.CompletedSessions, analytics.CompletedSessions],
                [ReportLabels.CompletionRate, Round1(analytics.CompletionRate)],
                [ReportLabels.AverageNps, Round1(analytics.NpsScore)],
                [ReportLabels.AverageCsat, Round1(analytics.CsatScore)],
                [ReportLabels.AverageCes, Round1(analytics.CesScore)],
                [ReportLabels.AverageRating, Round1(analytics.AverageRating)]
            ],
            Footnote = analytics.IsAnonymous ? ReportLabels.AnonymousSurveyFootnote : null
        });

        // ترکیب NPS: توزیع ترویج‌کننده/خنثی/منتقد فقط در صورت وجود سؤال NPS.
        if (analytics.NpsScore.HasValue)
        {
            sections.Add(new ReportSection
            {
                Title = ReportLabels.NpsDistribution,
                Columns =
                [
                    new(ReportLabels.NpsPromoters, 140, ReportColumnType.Number),
                    new(ReportLabels.NpsPassives, 140, ReportColumnType.Number),
                    new(ReportLabels.NpsDetractors, 140, ReportColumnType.Number),
                    new(ReportLabels.AverageNps, 120, ReportColumnType.Number)
                ],
                Rows =
                [
                    Row(analytics.NpsPromoters, analytics.NpsPassives, analytics.NpsDetractors, Round1(analytics.NpsScore))
                ]
            });
        }

        foreach (var question in analytics.Questions.OrderBy(q => q.DisplayOrder))
        {
            sections.AddRange(BuildQuestionSections(question));
        }

        if (analytics.Trend.Count > 0)
        {
            sections.Add(BuildTrendSection(analytics.Trend));
        }

        return sections;
    }

    private static List<ReportSection> BuildQuestionSections(QuestionMetricDto question)
    {
        var sections = new List<ReportSection>();

        if (question.Options.Count > 0)
        {
            sections.Add(new ReportSection
            {
                Title = $"{ReportLabels.QuestionDistribution}: {question.QuestionText}",
                Columns =
                [
                    new(ReportLabels.Option, 300, ReportColumnType.Text),
                    new(ReportLabels.Count, 90, ReportColumnType.Number),
                    new(ReportLabels.Percentage, 100, ReportColumnType.Percent)
                ],
                Rows = question.Options
                    .OrderBy(o => o.DisplayOrder)
                    .Select(o => Row(o.OptionText, o.Count, Round1(o.Percentage))).ToList(),
                Footnote = $"{ReportLabels.QuestionCode}: {question.QuestionCode}"
            });
        }
        else if (question.NumericStats is { } stats)
        {
            sections.Add(new ReportSection
            {
                Title = $"{ReportLabels.QuestionStatistics}: {question.QuestionText}",
                Columns =
                [
                    new(ReportLabels.Mean, 100, ReportColumnType.Number),
                    new(ReportLabels.Median, 100, ReportColumnType.Number),
                    new(ReportLabels.Minimum, 100, ReportColumnType.Number),
                    new(ReportLabels.Maximum, 100, ReportColumnType.Number),
                    new(ReportLabels.StandardDeviation, 120, ReportColumnType.Number),
                    new(ReportLabels.ResponseCount, 100, ReportColumnType.Number)
                ],
                Rows =
                [
                    Row(
                        Round2(stats.Mean), Round2(stats.Median), Round2(stats.Min), Round2(stats.Max),
                        Round2(stats.StandardDeviation), stats.ResponseCount)
                ],
                Footnote = $"{ReportLabels.QuestionCode}: {question.QuestionCode}"
            });
        }
        else if (question.YesNoDistribution is { } yesNo)
        {
            sections.Add(new ReportSection
            {
                Title = $"{ReportLabels.QuestionDistribution}: {question.QuestionText}",
                Columns =
                [
                    new(ReportLabels.YesCount, 100, ReportColumnType.Number),
                    new(ReportLabels.NoCount, 100, ReportColumnType.Number),
                    new(ReportLabels.YesPercentage, 110, ReportColumnType.Percent),
                    new(ReportLabels.NoPercentage, 110, ReportColumnType.Percent)
                ],
                Rows = [Row(yesNo.YesCount, yesNo.NoCount, Round1(yesNo.YesPercentage), Round1(yesNo.NoPercentage))],
                Footnote = $"{ReportLabels.QuestionCode}: {question.QuestionCode}"
            });
        }

        return sections;
    }

    // --- بخش‌های بنچمارک -----------------------------------------------------

    private static IReadOnlyList<ReportSection> BuildBenchmarkSections(IReadOnlyList<BenchmarkComparisonDto> comparisons)
    {
        if (comparisons.Count == 0)
        {
            return [];
        }

        return
        [
            new ReportSection
            {
                Title = ReportLabels.BenchmarkComparison,
                Columns =
                [
                    new(ReportLabels.Metric, 200, ReportColumnType.Text),
                    new(ReportLabels.BenchmarkName, 200, ReportColumnType.Text),
                    new(ReportLabels.ActualValue, 110, ReportColumnType.Number),
                    new(ReportLabels.TargetValue, 110, ReportColumnType.Number),
                    new(ReportLabels.Delta, 100, ReportColumnType.Number),
                    new(ReportLabels.Status, 120, ReportColumnType.Text)
                ],
                Rows = comparisons.Select(c => Row(
                    ReportLabels.MetricName(c.Metric),
                    c.BenchmarkName,
                    Round1(c.ActualValue),
                    Round1(c.TargetValue),
                    Round1(c.Delta),
                    c.IsAboveTarget switch
                    {
                        true => ReportLabels.AboveTarget,
                        false => ReportLabels.BelowTarget,
                        _ => ReportLabels.NoTarget
                    })).ToList()
            }
        ];
    }

    // --- بخش‌های مشترک -------------------------------------------------------

    private static ReportSection BuildTrendSection(IReadOnlyList<TrendPointDto> trend) => new()
    {
        Title = ReportLabels.SubmissionTrend,
        Columns =
        [
            new(ReportLabels.Period, 160, ReportColumnType.Text),
            new(ReportLabels.Count, 90, ReportColumnType.Number),
            new(ReportLabels.CumulativeCount, 120, ReportColumnType.Number),
            new(ReportLabels.AverageRating, 110, ReportColumnType.Number),
            new(ReportLabels.CompletionRate, 100, ReportColumnType.Percent)
        ],
        Rows = trend.Select(t => Row(
            t.PeriodLabel, t.Count, t.CumulativeCount, Round1(t.AverageRating), Round1(t.CompletionRate))).ToList()
    };

    /// <summary>ساخت یک ردیف داده از مقادیر سلول‌ها.</summary>
    private static object?[] Row(params object?[] values) => values;

    /// <summary>زیرعنوان گزارش: دامنه، بازه‌ی زمانی و راه‌انداز.</summary>
    private static string BuildSubtitle(ReportDefinition definition)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(definition.SurveyTitle))
        {
            parts.Add($"{ReportLabels.Survey}: {definition.SurveyTitle}");
        }

        if (definition.OrgUnitId.HasValue && !string.IsNullOrWhiteSpace(definition.OrgUnitPath))
        {
            parts.Add($"{ReportLabels.OrgUnit}: {definition.OrgUnitPath}");
        }

        if (definition.From.HasValue || definition.To.HasValue)
        {
            parts.Add($"{ReportLabels.Window}: {FormatDate(definition.From)} {ReportLabels.To} {FormatDate(definition.To)}");
        }

        return parts.Count > 0 ? string.Join(" • ", parts) : ReportLabels.WholeCompany;
    }

    private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";

    private static decimal? Round1(decimal? value) => value.HasValue ? Math.Round(value.Value, 1) : null;

    private static decimal? Round2(decimal? value) => value.HasValue ? Math.Round(value.Value, 2) : null;
}

/// <summary>
/// برچسب‌های ثابت فارسی گزارش‌ها. سامانه فارسی-اول است و خروجی گزارش‌ها با
/// برچسب‌های فارسی تولید می‌شود.
/// </summary>
internal static class ReportLabels
{
    public const string SystemScheduler = "زمان‌بند خودکار";
    public const string WholeCompany = "تمام شرکت";

    public const string DashboardOverview = "نگاه کلی";
    public const string KeyMetrics = "شاخص‌های کلیدی";
    public const string NpsDistribution = "توزیع NPS";
    public const string TopSurveys = "پربازدیدترین نظرسنجی‌ها";
    public const string QuestionDistribution = "توزیع پاسخ";
    public const string QuestionStatistics = "آمار عددی";
    public const string BenchmarkComparison = "مقایسه با بنچمارک";
    public const string SubmissionTrend = "روند ارسال پاسخ‌ها";

    public const string Metric = "شاخص";
    public const string Value = "مقدار";
    public const string Survey = "نظرسنجی";
    public const string SurveyTitle = "عنوان نظرسنجی";
    public const string SurveyCode = "کد نظرسنجی";
    public const string OrgUnit = "واحد سازمانی";
    public const string Window = "بازه";
    public const string To = "تا";
    public const string Period = "دوره";
    public const string Count = "تعداد";
    public const string Percentage = "درصد";
    public const string CumulativeCount = "تجمعی";
    public const string ResponseCount = "تعداد پاسخ";
    public const string Option = "گزینه";
    public const string Mean = "میانگین";
    public const string Median = "میانه";
    public const string Minimum = "کمینه";
    public const string Maximum = "بیشینه";
    public const string StandardDeviation = "انحراف معیار";
    public const string YesCount = "تعداد «بله»";
    public const string NoCount = "تعداد «خیر»";
    public const string YesPercentage = "درصد «بله»";
    public const string NoPercentage = "درصد «خیر»";
    public const string BenchmarkName = "نام بنچمارک";
    public const string ActualValue = "مقدار واقعی";
    public const string TargetValue = "مقدار هدف";
    public const string Delta = "اختلاف";
    public const string Status = "وضعیت";
    public const string AboveTarget = "بالاتر از هدف";
    public const string BelowTarget = "پایین‌تر از هدف";
    public const string NoTarget = "بدون هدف";
    public const string QuestionCode = "کد سؤال";

    public const string TotalSurveys = "تعداد نظرسنجی‌ها";
    public const string ActiveSurveys = "نظرسنجی‌های فعال";
    public const string TotalCampaigns = "تعداد کمپین‌ها";
    public const string ActiveCampaigns = "کمپین‌های در حال اجرا";
    public const string TotalSessions = "نشست‌های پاسخ‌گویی";
    public const string CompletedSessions = "پاسخ‌های ارسال‌شده";
    public const string CompletionRate = "نرخ تکمیل (٪)";
    public const string AverageNps = "NPS میانگین";
    public const string AverageCsat = "CSAT میانگین";
    public const string AverageCes = "CES میانگین";
    public const string AverageRating = "میانگین امتیاز";
    public const string NpsPromoters = "ترویج‌کنندگان";
    public const string NpsPassives = "خنثی‌ها";
    public const string NpsDetractors = "منتقدان";

    public const string AnonymousSurveyFootnote =
        "این نظرسنجی ناشناس است؛ بخش‌بندی سازمانی در آن ممکن نیست چون هیچ پیوند سازمانی ذخیره نشده است.";

    /// <summary>نام نمایشی یک شاخص.</summary>
    public static string MetricName(MetricType metric) => metric switch
    {
        MetricType.Nps => "NPS (شاخص خالص ترویج)",
        MetricType.Csat => "CSAT (رضایت)",
        MetricType.Ces => "CES (تلاش)",
        MetricType.CompletionRate => "نرخ تکمیل",
        MetricType.ResponseRate => "نرخ پاسخ‌گویی",
        _ => "میانگین امتیاز"
    };
}
