using System.Text.Json;
using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Languages;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Entities;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Campaign.Enums;
using ODCC.Domain.Modules.QuestionBank.Entities;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Response.Entities;
using ODCC.Domain.Modules.Response.Enums;
using SurveyEntity = ODCC.Domain.Modules.Survey.Entities.Survey;

namespace ODCC.Infrastructure.Modules.Analytics.Services;

/// <summary>
/// موتور محاسبه‌ی شاخص‌های تحلیلی.
///
/// این کلاس فقط <b>تجمع‌ها</b> را از پاسخ‌های خام محاسبه می‌کند و هرگز
/// شناسه‌ی پاسخ‌گو را در خروجی قرار نمی‌دهد. برای بخش‌بندی سازمانی، مسیر واحد
/// پاسخ‌گو را از طریق <b>قراردادهای</b> ماژول سازمان (نه DbContext آن) حل می‌کند.
///
/// **عملکرد:** ساختار ثابتِ پرسشنامه یک بار بارگذاری و قفل می‌شود
/// (<see cref="AnalyticsQuestionnaireStructure"/>) تا محاسبه‌ی چند بُعدِ یک
/// نظرسنجی از پرس‌وجوهای تکراری برای گزینه‌ها و طیف‌ها بی‌نیاز باشد.
/// </summary>
public sealed partial class AnalyticsComputationEngine(
    IResponseRepository responseRepository,
    ISurveyRepository surveyRepository,
    IQuestionnaireService questionnaireService,
    IQuestionRepository questionRepository,
    ICampaignRepository campaignRepository,
    IDistributionRepository distributionRepository,
    IEmployeeRepository employeeRepository,
    IOrgUnitRepository orgUnitRepository,
    IOrgScopeProvider orgScopeProvider,
    ICurrentUserService currentUserService,
    IAnalyticsAiService analyticsAiService,
    ILogger<AnalyticsComputationEngine> logger)
{
    private readonly IResponseRepository _responseRepository = responseRepository;
    private readonly ISurveyRepository _surveyRepository = surveyRepository;
    private readonly IQuestionnaireService _questionnaireService = questionnaireService;
    private readonly IQuestionRepository _questionRepository = questionRepository;
    private readonly ICampaignRepository _campaignRepository = campaignRepository;
    private readonly IDistributionRepository _distributionRepository = distributionRepository;
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly IOrgScopeProvider _orgScopeProvider = orgScopeProvider;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IAnalyticsAiService _analyticsAiService = analyticsAiService;
    private readonly ILogger<AnalyticsComputationEngine> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// بارگذاری ساختار پاسخ‌گویی یک نظرسنجی: پرسشنامه (از طریق قرارداد) به‌همراه
    /// گزینه‌ها و طیف سؤال‌های کتابخانه. این ساختار برای تمام ابعاد یک محاسبه
    /// یکسان است و فقط یک بار ساخته می‌شود.
    /// </summary>
    public async Task<AnalyticsQuestionnaireStructure?> LoadStructureAsync(SurveyEntity survey, CancellationToken ct)
    {
        var questionnaireResult = await _questionnaireService.GetByIdAsync(survey.QuestionnaireId, ct);
        if (questionnaireResult.IsFailure || questionnaireResult.Value is null)
        {
            return null;
        }

        var questionIds = questionnaireResult.Value.Sections
            .SelectMany(s => s.Items)
            .Select(i => i.QuestionId)
            .Distinct()
            .ToList();

        var questions = questionIds.Count > 0
            ? (await _questionRepository.GetByIdsAsync(questionIds, ct)).ToDictionary(q => q.Id)
            : new Dictionary<Guid, Question>();

        return new AnalyticsQuestionnaireStructure(questionnaireResult.Value, questions);
    }

    /// <summary>
    /// بارگذاری نشست‌های ارسال‌شده‌ی یک نظرسنجی به‌همراه پاسخ‌ها و گزینه‌ها، و
    /// فیلتر آن‌ها بر اساس پنجره‌ی زمانی و کمپین دعوت‌کننده.
    ///
    /// <b>چرا فیلتر دامنه‌ی سازمانی اعمال نمی‌شود:</b> عکس‌العمل تحلیلات یک
    /// مدل سراسریِ نظرسنجی است که شاخص‌های واقعی را در سطح نظرسنجی ارائه می‌دهد.
    /// این متد از دو مسیر فراخوانی می‌شود: ۱) شنونده‌ی رویداد «ارسال پاسخ» که در
    /// <b>هویت پاسخ‌گو</b> اجرا می‌شود و دامنه‌ی او <c>Own</c> است؛ اگر فیلتر اعمال
    /// می‌شد، همه‌ی شاخص‌ها صفر می‌شدند. ۲) درخواست صریحِ مدیر. بخش‌بندی سازمانی
    /// فقط در <see cref="AnalyticsService.GetSegmentsByOrgUnitAsync"/> و با مجوز
    /// <c>analytics.department.view</c> انجام می‌شود.
    ///
    /// <b>حریم خصوصی:</b> خروجی فقط شامل پاسخ‌های خام و شناسه‌ی کارمند (برای حل
    /// بخش‌بندی) است. هیچ نام یا شناسه‌ی کاربری در عکس‌العمل نهایی ذخیره نمی‌شود.
    /// </summary>
    public async Task<IReadOnlyList<ResponseSession>> LoadSessionsAsync(
        SurveyEntity survey,
        AnalyticsFilter filter,
        CancellationToken ct)
    {
        var sessions = await _responseRepository.ListSubmittedBySurveyAsync(
            survey.Id, filter.From, filter.To, ct);

        if (sessions.Count == 0)
            return sessions;

        // محدود کردن به کمپین دعوت‌کننده (در صورت درخواست).
        if (filter.CampaignId is { } campaignId)
            sessions = sessions.Where(s => s.CampaignId == campaignId).ToList();

        return sessions;
    }

    /// <summary>
    /// فیلتر کردن نشست‌ها بر اساس دامنه‌ی سازمانی قابل‌مشاهده‌ی کاربر جاری.
    /// <b>fail-closed:</b> اگر دامنه قابل‌مشاهده نباشد، هیچ داده‌ای برگردانده نمی‌شود.
    /// </summary>
    public async Task<IReadOnlyList<ResponseSession>> ApplyOrgScopeFilterAsync(
        IReadOnlyList<ResponseSession> sessions,
        bool isAnonymous,
        Guid? explicitOrgUnitId,
        bool includeDescendants,
        CancellationToken ct)
    {
        if (sessions.Count == 0)
            return sessions;

        // نظرسنجی‌های ناشناس هیچ پیوند سازمانی ندارند؛ فیلتر سازمانی بی‌معنی است.
        // درخواست صریحِ واحد سازمانی روی نظرسنجی ناشناس داده‌ای برنمی‌گرداند.
        if (isAnonymous)
            return explicitOrgUnitId.HasValue ? [] : sessions;

        var employeeIds = sessions
            .Where(s => s.RespondentEmployeeId.HasValue)
            .Select(s => s.RespondentEmployeeId!.Value)
            .Distinct()
            .ToList();

        if (employeeIds.Count == 0)
            return explicitOrgUnitId.HasValue ? [] : sessions;

        // حل مسیر سازمانی پاسخ‌گوها از طریق قراردادها (نه DbContext ماژول سازمان).
        var employeePaths = await ResolveOrgUnitPathsAsync(employeeIds, ct);

        // دامنه‌ی قابل‌مشاهده: درخواست صریح یا دامنه‌ی کاربر جاری.
        OrgScope scope;

        if (explicitOrgUnitId.HasValue)
        {
            var explicitPath = await _orgUnitRepository.GetPathAsync(explicitOrgUnitId.Value, ct);
            if (string.IsNullOrWhiteSpace(explicitPath))
                return [];

            // درخواست صریح باید درون دامنه‌ی کاربر جاری باشد (fail-closed).
            var userScope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
            if (!userScope.IsUnrestricted && !userScope.CanAccess(explicitPath))
            {
                ExplicitUnitDenied(_logger, explicitOrgUnitId.Value, null);
                return [];
            }

            scope = new OrgScope
            {
                AnchorOrgUnitId = explicitOrgUnitId,
                AnchorPath = explicitPath,
                Scope = DataScope.Department
            };

            if (!includeDescendants)
            {
                // فقط همین واحد: مسیر باید دقیقاً برابر باشد.
                return sessions
                    .Where(s => s.RespondentEmployeeId.HasValue
                        && employeePaths.TryGetValue(s.RespondentEmployeeId.Value, out var p)
                        && string.Equals(p, explicitPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }
        else
        {
            scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        }

        // fail-closed: دامنه‌ی بدون دید سازمانی → لیست خالی.
        if (!scope.IsUnrestricted && !scope.HasVisibleOrgScope)
            return [];

        if (scope.IsUnrestricted)
            return sessions;

        return sessions
            .Where(s => !s.RespondentEmployeeId.HasValue
                || (employeePaths.TryGetValue(s.RespondentEmployeeId.Value, out var p)
                    && scope.CanAccess(p)))
            .ToList();
    }

    /// <summary>
    /// حل مسیر سازمانی چند کارمند (برای بخش‌بندی) — از طریق قراردادها.
    /// </summary>
    public async Task<Dictionary<Guid, string?>> ResolveOrgUnitPathsAsync(
        IReadOnlyCollection<Guid> employeeIds, CancellationToken ct)
    {
        if (employeeIds.Count == 0)
            return new Dictionary<Guid, string?>();

        var employees = await _employeeRepository.GetByIdsAsync(employeeIds, ct);

        var employeesWithUnit = employees
            .Where(e => e.OrgUnitId != Guid.Empty)
            .ToList();

        Dictionary<Guid, string?> orgUnitPaths;

        if (employeesWithUnit.Count == 0)
        {
            orgUnitPaths = new Dictionary<Guid, string?>(0);
        }
        else
        {
            // مسیر هر واحد سازمانی یک بار (نه یک بار به ازای هر کارمند) حل می‌شود
            // تا تعداد پرس‌وجوها از تعداد واحدها بیشتر نشود.
            var distinctUnitIds = employeesWithUnit
                .Select(e => e.OrgUnitId)
                .Distinct()
                .ToList();

            orgUnitPaths = new Dictionary<Guid, string?>(distinctUnitIds.Count);

            foreach (var orgUnitId in distinctUnitIds)
                orgUnitPaths[orgUnitId] = await _orgUnitRepository.GetPathAsync(orgUnitId, ct);
        }

        return employees.ToDictionary(
            e => e.Id,
            e => e.OrgUnitId != Guid.Empty
                ? orgUnitPaths.GetValueOrDefault(e.OrgUnitId)
                : null);
    }

    /// <summary>
    /// محاسبه‌ی عکس‌العمل کامل یک بُعد تحلیلی از روی نشست‌های (فیلترشده‌ی) ارسال‌شده.
    /// </summary>
    public async Task<SurveyMetricsSnapshot> ComputeSnapshotAsync(
        SurveyEntity survey,
        AnalyticsQuestionnaireStructure structure,
        IReadOnlyList<ResponseSession> sessions,
        IReadOnlyList<ResponseSession> allSessionsInScope,
        ComputeAnalyticsRequest request,
        CancellationToken ct)
    {
        var allAnswers = sessions.SelectMany(s => s.Answers).ToList();
        var questionMetrics = new List<QuestionMetric>();

        foreach (var item in structure.Items.Values.OrderBy(i => i.DisplayOrder))
        {
            var metric = await ComputeQuestionMetric(item, allAnswers, sessions.Count, structure, request, ct);

            questionMetrics.Add(metric);
        }

        // --- شاخص‌های NPS/CSAT/CES --------------------------------------------
        // سؤال شاخص بر اساس طیف امتیازدهی شناسایی می‌شود: طیف ۱۰ → NPS،
        // طیف ۵ → CSAT و طیف ۷ → CES. این شناسایی مالکیتی روی تعریف سؤال نیست؛
        // فقط روش محاسبه‌ی هر سؤال امتیازدهی را تعیین می‌کند.
        var nps = ComputeNps(structure, allAnswers);
        var csat = ComputeCsat(structure, allAnswers);
        var ces = ComputeCes(structure, allAnswers);

        // --- نرخ تکمیل و نرخ پاسخ ----------------------------------------------
        // مخرج نرخ تکمیل «کل نشست‌های شروع‌شده» است (شروع‌شده + ارسال‌شده).
        // این مقدار از دامنه‌ی وسیع‌تری خوانده می‌شود تا افت پاسخ کامل به‌نمایندگی
        // از کسانی که شروع کرده‌اند ولی ارسال نکرده‌اند نشان داده شود.
        var totalSessions = allSessionsInScope.Count;
        var completedSessions = sessions.Count;

        decimal? responseRate = null;
        var totalDistributions = 0;
        var respondedDistributions = 0;

        if (request.CampaignId is { } campaignId)
        {
            totalDistributions = await _distributionRepository.CountByCampaignAsync(campaignId, ct);
            respondedDistributions = await _distributionRepository.CountByStatusAsync(campaignId, DistributionStatus.Responded, ct);
            responseRate = totalDistributions > 0
                ? (decimal)respondedDistributions / totalDistributions * 100m
                : 0m;
        }

        // --- میانگین امتیاز کلی ------------------------------------------------
        var ratingValues = allAnswers
            .Where(a => a.QuestionType == QuestionType.Rating && a.NumericValue.HasValue)
            .Select(a => a.NumericValue!.Value)
            .ToList();

        var averageRating = ratingValues.Count > 0
            ? ratingValues.Average()
            : (decimal?)null;

        return new SurveyMetricsSnapshot
        {
            TotalSessions = totalSessions,
            CompletedSessions = completedSessions,
            CompletionRate = totalSessions > 0
                ? (decimal)completedSessions / totalSessions * 100m
                : 0m,
            NpsScore = nps.Score,
            NpsPromoters = nps.Promoters,
            NpsPassives = nps.Passives,
            NpsDetractors = nps.Detractors,
            NpsQuestionId = nps.QuestionId,
            CsatScore = csat.Score,
            CsatRespondents = csat.Respondents,
            CsatQuestionId = csat.QuestionId,
            CesScore = ces.Score,
            CesRespondents = ces.Respondents,
            CesQuestionId = ces.QuestionId,
            AverageRating = averageRating,
            RatingRespondents = ratingValues.Count,
            ResponseRate = responseRate,
            TotalDistributions = totalDistributions,
            RespondedDistributions = respondedDistributions,
            QuestionMetrics = JsonSerializer.Serialize(questionMetrics, JsonOptions)
        };
    }

    /// <summary>محاسبه‌ی تحلیل یک سؤال (توزیع گزینه‌ها، آمار عددی یا تحلیل متن).</summary>
    private async Task<QuestionMetric> ComputeQuestionMetric(
        QuestionnaireItemDto item,
        List<ResponseAnswer> allAnswers,
        int sessionCount,
        AnalyticsQuestionnaireStructure structure,
        ComputeAnalyticsRequest request,
        CancellationToken ct)
    {
        var answers = allAnswers
            .Where(a => a.QuestionnaireItemId == item.Id)
            .ToList();

        var answered = answers.Where(a => a.HasValue).ToList();

        var metric = new QuestionMetric
        {
            QuestionId = item.QuestionId,
            QuestionnaireItemId = item.Id,
            QuestionCode = item.QuestionCode,
            QuestionText = string.IsNullOrWhiteSpace(item.TitleOverride)
                ? item.QuestionText
                : item.TitleOverride,
            QuestionType = item.QuestionType,
            DisplayOrder = item.DisplayOrder,
            ResponseCount = answered.Count,
            ResponseRate = sessionCount > 0
                ? (decimal)answered.Count / sessionCount * 100m
                : 0m
        };

        if (item.QuestionType.HasOptions() && structure.TryGetOptions(item.QuestionId, out var options))
        {
            var optionCounts = answered
                .SelectMany(a => a.Selections)
                .GroupBy(s => s.OptionId)
                .ToDictionary(g => g.Key, g => g.Count());

            metric.Options = options
                .Select(o => new OptionMetric
                {
                    OptionId = o.Id,
                    OptionCode = o.Code,
                    OptionText = o.Text,
                    DisplayOrder = o.DisplayOrder,
                    Count = optionCounts.GetValueOrDefault(o.Id)
                })
                .ToList();

            // درصد نسبت به تعداد پاسخ‌گویان این سؤال (نه کل نشست‌ها).
            foreach (var o in metric.Options)
                o.Percentage = answered.Count > 0 ? (decimal)o.Count / answered.Count * 100m : 0m;
        }
        else if (item.QuestionType is QuestionType.Rating or QuestionType.Number)
        {
            var numericValues = answered
                .Where(a => a.NumericValue.HasValue)
                .Select(a => a.NumericValue!.Value)
                .ToList();

            metric.NumericStats = ComputeNumericStats(numericValues, item.QuestionType == QuestionType.Rating);

            // شناسایی سؤال شاخص بر اساس طیف.
            if (item.QuestionType == QuestionType.Rating
                && structure.TryGetScaleMax(item.QuestionId, out var scaleMax))
            {
                metric.DetectedMetric = scaleMax switch
                {
                    10 => MetricType.Nps,
                    5 => MetricType.Csat,
                    7 => MetricType.Ces,
                    _ => null
                };
            }
        }
        else if (item.QuestionType == QuestionType.YesNo)
        {
            var yesCount = answered.Count(a => string.Equals(a.TextValue, "yes", StringComparison.OrdinalIgnoreCase));
            var noCount = answered.Count(a => string.Equals(a.TextValue, "no", StringComparison.OrdinalIgnoreCase));

            metric.YesNoDistribution = new YesNoDistribution
            {
                YesCount = yesCount,
                NoCount = noCount
            };
        }
        else if (item.QuestionType.IsTextual() && request.IncludeTextAnalytics)
        {
            // فقط متن خام ارسال می‌شود — هیچ شناسه‌ی پاسخ‌گویی همراه آن نیست.
            var texts = answered
                .Select(a => a.TextValue)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!.Trim())
                .ToList();

            if (texts.Count > 0)
            {
                metric.TextAnalytics = await ComputeTextAnalyticsAsync(texts, ct);
            }
        }

        return metric;
    }

    /// <summary>
    /// NPS: اولین سؤال امتیازدهی با طیف ۱۰. ترویج‌کننده ۹-۱۰، خنثی ۷-۸، منتقد ۱-۶.
    /// </summary>
    private static (decimal? Score, int Promoters, int Passives, int Detractors, Guid? QuestionId) ComputeNps(
        AnalyticsQuestionnaireStructure structure, List<ResponseAnswer> allAnswers)
    {
        var item = structure.FindRatingItem(10);
        if (item is null)
            return (null, 0, 0, 0, null);

        var values = NumericValuesFor(structure, item, allAnswers);
        if (values.Count == 0)
            return (null, 0, 0, 0, item.QuestionId);

        var promoters = values.Count(v => v >= 9);
        var passives = values.Count(v => v >= 7 && v < 9);
        var detractors = values.Count(v => v < 7);

        return (
            (decimal)(promoters - detractors) / values.Count * 100m,
            promoters,
            passives,
            detractors,
            item.QuestionId);
    }

    /// <summary>CSAT: اولین سؤال امتیازدهی با طیف ۵. درصد راضیان (۴ و ۵).</summary>
    private static (decimal? Score, int Respondents, Guid? QuestionId) ComputeCsat(
        AnalyticsQuestionnaireStructure structure, List<ResponseAnswer> allAnswers)
    {
        var item = structure.FindRatingItem(5);
        if (item is null)
            return (null, 0, null);

        var values = NumericValuesFor(structure, item, allAnswers);
        if (values.Count == 0)
            return (null, 0, item.QuestionId);

        return ((decimal)values.Count(v => v >= 4) / values.Count * 100m, values.Count, item.QuestionId);
    }

    /// <summary>CES: اولین سؤال امتیازدهی با طیف ۷. درصد کم‌تلاش‌ها (۵ به بالا).</summary>
    private static (decimal? Score, int Respondents, Guid? QuestionId) ComputeCes(
        AnalyticsQuestionnaireStructure structure, List<ResponseAnswer> allAnswers)
    {
        var item = structure.FindRatingItem(7);
        if (item is null)
            return (null, 0, null);

        var values = NumericValuesFor(structure, item, allAnswers);
        if (values.Count == 0)
            return (null, 0, item.QuestionId);

        return ((decimal)values.Count(v => v >= 5) / values.Count * 100m, values.Count, item.QuestionId);
    }

    /// <summary>مقادیر عددی پاسخ‌های یک آیتم امتیازدهی.</summary>
    private static List<decimal> NumericValuesFor(
        AnalyticsQuestionnaireStructure structure,
        QuestionnaireItemDto item,
        List<ResponseAnswer> allAnswers)
    {
        if (!structure.TryGetScaleMax(item.QuestionId, out var scaleMax) || scaleMax <= 0)
            return [];

        return allAnswers
            .Where(a => a.QuestionnaireItemId == item.Id && a.NumericValue.HasValue)
            .Select(a => a.NumericValue!.Value)
            .Where(v => v >= 1 && v <= scaleMax)
            .ToList();
    }

    private static NumericStats ComputeNumericStats(List<decimal> values, bool isRating)
    {
        if (values.Count == 0)
            return new NumericStats();

        var sorted = values.Order().ToList();
        var mean = values.Average();
        var median = sorted.Count % 2 == 0
            ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2m
            : sorted[sorted.Count / 2];

        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        var std = (decimal)Math.Sqrt((double)variance);

        var stats = new NumericStats
        {
            Mean = Math.Round(mean, 3),
            Median = Math.Round(median, 3),
            Min = sorted[0],
            Max = sorted[^1],
            StandardDeviation = Math.Round(std, 3),
            ResponseCount = values.Count
        };

        if (isRating)
        {
            stats.RatingBuckets = sorted
                .GroupBy(v => v)
                .OrderBy(g => g.Key)
                .Select(g => new RatingBucket
                {
                    Value = g.Key,
                    Count = g.Count(),
                    Percentage = (decimal)g.Count() / values.Count * 100m
                })
                .ToList();
        }

        return stats;
    }

    /// <summary>
    /// تحلیل متن با هوش مصنوعی — فقط متن خام (بدون شناسه) ارسال می‌شود.
    /// در صورت نبودن ارائه‌دهنده (No-op)، توزیع خنثی برمی‌گرداند.
    /// </summary>
    private async Task<TextAnalyticsResult> ComputeTextAnalyticsAsync(List<string> texts, CancellationToken ct)
    {
        var result = new TextAnalyticsResult
        {
            TextResponseCount = texts.Count
        };

        try
        {
            var sentimentCounts = new Dictionary<Sentiment, int>();

            foreach (var text in texts)
            {
                var sentiment = await _analyticsAiService.AnalyzeSentimentAsync(text, ct);
                sentimentCounts[sentiment.Sentiment] = sentimentCounts.GetValueOrDefault(sentiment.Sentiment) + 1;
            }

            result.Sentiment = new SentimentDistribution
            {
                TotalCount = texts.Count,
                PositivePercentage = PercentageOf(sentimentCounts, Sentiment.Positive, texts.Count),
                NeutralPercentage = PercentageOf(sentimentCounts, Sentiment.Neutral, texts.Count),
                NegativePercentage = PercentageOf(sentimentCounts, Sentiment.Negative, texts.Count),
                UnknownPercentage = PercentageOf(sentimentCounts, Sentiment.Unknown, texts.Count)
            };

            result.Themes = (await _analyticsAiService.ExtractThemesAsync(texts, ct)).ToList();
        }
        catch (Exception ex)
        {
            // شکست هوش مصنوعی نباید محاسبه‌ی کل تحلیلات را لغو کند؛ فقط لاگ می‌شود.
            TextAnalyticsFailed(_logger, ex);
        }

        return result;
    }

    private static decimal PercentageOf(IReadOnlyDictionary<Sentiment, int> counts, Sentiment sentiment, int total) =>
        total > 0 ? (decimal)counts.GetValueOrDefault(sentiment) / total * 100m : 0m;
}

/// <summary>
/// ساختار پرسشنامه برای تحلیلات: آیتم‌ها به‌همراه گزینه‌ها و طیف سؤال‌های کتابخانه.
/// این ساختار برای تمام ابعاد یک محاسبه‌ی تحلیلی مشترک است تا پرس‌وجوهای
/// تکراری برای خواندن گزینه‌ها و طیف‌ها انجام نشود.
/// </summary>
public sealed class AnalyticsQuestionnaireStructure
{
    private readonly QuestionnaireDto _questionnaire;
    private readonly Dictionary<Guid, Question> _questions;

    public AnalyticsQuestionnaireStructure(QuestionnaireDto questionnaire, Dictionary<Guid, Question> questions)
    {
        _questionnaire = questionnaire;
        _questions = questions;
        Items = questionnaire.Sections
            .SelectMany(s => s.Items)
            .OrderBy(i => i.DisplayOrder)
            .ToDictionary(i => i.Id);
    }

    /// <summary>آیتم‌های پرسشنامه به ترتیب نمایش، کلیدگذاری با شناسه‌ی آیتم.</summary>
    public Dictionary<Guid, QuestionnaireItemDto> Items { get; }

    /// <summary>گزینه‌های یک سؤال (مرتب بر اساس ترتیب نمایش) یا مجموعه‌ی خالی.</summary>
    public bool TryGetOptions(Guid questionId, out IReadOnlyList<QuestionOptionDto> options)
    {
        if (_questions.TryGetValue(questionId, out var question))
        {
            options = question.Options
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new QuestionOptionDto
                {
                    Id = o.Id,
                    Code = o.Code,
                    DisplayOrder = o.DisplayOrder,
                    Text = o.Localizations.Pick(Language.Fa)?.Text
                        ?? o.Localizations.FirstOrDefault()?.Text
                        ?? o.Code
                })
                .ToList();

            return true;
        }

        options = [];
        return false;
    }

    /// <summary>حداکثر طیف یک سؤال امتیازدهی (۰ در صورت نبودن).</summary>
    public bool TryGetScaleMax(Guid questionId, out int scaleMax)
    {
        if (_questions.TryGetValue(questionId, out var question))
        {
            scaleMax = question.ScaleMax;
            return true;
        }

        scaleMax = 0;
        return false;
    }

    /// <summary>
    /// اولین سؤال امتیازدهی پرسشنامه با طیفِ داده‌شده (به ترتیب نمایش).
    /// </summary>
    public QuestionnaireItemDto? FindRatingItem(int scaleMax)
    {
        foreach (var item in Items.Values)
        {
            if (item.QuestionType != QuestionType.Rating)
                continue;

            if (TryGetScaleMax(item.QuestionId, out var max) && max == scaleMax)
                return item;
        }

        return null;
    }
}

// --- مدل‌های داخلی سری‌سازی (JSON) -------------------------------------------

public sealed class QuestionMetric
{
    public Guid QuestionId { get; set; }
    public Guid QuestionnaireItemId { get; set; }
    public string QuestionCode { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public int DisplayOrder { get; set; }
    public int ResponseCount { get; set; }
    public decimal ResponseRate { get; set; }
    public List<OptionMetric> Options { get; set; } = [];
    public NumericStats? NumericStats { get; set; }
    public YesNoDistribution? YesNoDistribution { get; set; }
    public TextAnalyticsResult? TextAnalytics { get; set; }
    public MetricType? DetectedMetric { get; set; }
}

public sealed class OptionMetric
{
    public Guid OptionId { get; set; }
    public string OptionCode { get; set; } = string.Empty;
    public string OptionText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class NumericStats
{
    public decimal? Mean { get; set; }
    public decimal? Median { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public decimal? StandardDeviation { get; set; }
    public int ResponseCount { get; set; }
    public List<RatingBucket> RatingBuckets { get; set; } = [];
}

public sealed class RatingBucket
{
    public decimal Value { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class YesNoDistribution
{
    public int YesCount { get; set; }
    public int NoCount { get; set; }
}

public sealed class TextAnalyticsResult
{
    public int TextResponseCount { get; set; }
    public SentimentDistribution Sentiment { get; set; } = new();
    public List<ThemeExtractionResult> Themes { get; set; } = [];
}

public sealed class SentimentDistribution
{
    public decimal PositivePercentage { get; set; }
    public decimal NeutralPercentage { get; set; }
    public decimal NegativePercentage { get; set; }
    public decimal UnknownPercentage { get; set; }
    public int TotalCount { get; set; }
}
