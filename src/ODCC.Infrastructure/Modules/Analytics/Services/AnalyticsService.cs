using System.Text.Json;
using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Languages;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Entities;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Analytics.Events;
using ODCC.Domain.Modules.Campaign.Enums;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Domain.Modules.QuestionBank.Entities;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Response.Entities;
using ODCC.Domain.Modules.Response.Enums;
using ODCC.Domain.Modules.Survey.Enums;
using ODCC.Infrastructure.Modules.Analytics.Services;
using SurveyEntity = ODCC.Domain.Modules.Survey.Entities.Survey;
using SurveyMetricEntity = ODCC.Domain.Modules.Analytics.Entities.SurveyMetric;

namespace ODCC.Infrastructure.Modules.Analytics.Services;

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
/// <see cref="IResponseRepository"/> و برای ساختار پرسشنامه فقط از
/// <see cref="IQuestionnaireService"/> و <see cref="IQuestionRepository"/>
/// استفاده می‌کند — هرگز از DbContext آن ماژول‌ها.
///
/// **دامنه‌ی سازمانی:** همه‌ی خواندن‌ها fail-closed هستند. اگر دامنه‌ی قابل
/// مشاهده‌ی کاربر مشخص نباشد، هیچ داده‌ای برگردانده نمی‌شود.
/// </summary>
public sealed class AnalyticsService(
    IAnalyticsRepository analyticsRepository,
    IBenchmarkRepository benchmarkRepository,
    IResponseRepository responseRepository,
    ISurveyRepository surveyRepository,
    ICampaignRepository campaignRepository,
    IQuestionnaireService questionnaireService,
    IQuestionRepository questionRepository,
    IEmployeeRepository employeeRepository,
    IOrgUnitRepository orgUnitRepository,
    AnalyticsComputationEngine engine,
    IAnalyticsUnitOfWork unitOfWork,
    ICurrentUserService currentUserService) : IAnalyticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAnalyticsRepository _analyticsRepository = analyticsRepository;
    private readonly IBenchmarkRepository _benchmarkRepository = benchmarkRepository;
    private readonly IResponseRepository _responseRepository = responseRepository;
    private readonly ISurveyRepository _surveyRepository = surveyRepository;
    private readonly ICampaignRepository _campaignRepository = campaignRepository;
    private readonly IQuestionnaireService _questionnaireService = questionnaireService;
    private readonly IQuestionRepository _questionRepository = questionRepository;
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly AnalyticsComputationEngine _engine = engine;
    private readonly IAnalyticsUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    /// <inheritdoc/>
    public async Task<Result<SurveyAnalyticsDto>> ComputeAsync(
        ComputeAnalyticsRequest request, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(request.SurveyId, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyAnalyticsDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        // نظرسنجی بایگانی‌شده داده‌ای برای محاسبه ندارد.
        if (survey.Status == SurveyStatus.Archived)
        {
            return Result.Failure<SurveyAnalyticsDto>("survey_archived", "این نظرسنجی بایگانی شده و تحلیلی ندارد.");
        }

        var structure = await _engine.LoadStructureAsync(survey, ct);
        if (structure is null)
        {
            return Result.Failure<SurveyAnalyticsDto>("questionnaire_not_available", "ساختار پرسشنامه‌ی این نظرسنجی در دسترس نیست.");
        }

        // نشست‌های ارسال‌شده‌ی این نظرسنجی. عکس‌العمل تحلیلات یک مدل سراسری
        // برای نظرسنجی است: همه‌ی پاسخ‌های ارسال‌شده را شامل می‌شود تا شاخص‌ها
        // ارزش واقعی خود را داشته باشند. این محاسبه از طریق شنونده‌ی رویداد
        // «ارسال پاسخ» و از طریق فراخوانی صریحِ مدیر انجام می‌شود.
        var submittedSessions = await _engine.LoadSessionsAsync(survey, request.Filter, ct);

        // کل نشست‌های شروع‌شده (مخرج نرخ تکمیل) — برای محاسبه‌ی دقیقِ افت پاسخ.
        // این مجموعه از دامنه‌ی وسیع‌تری خوانده می‌شود تا نشست‌های شروع‌شده‌ی
        // ارسال‌نشده هم در مخرج باشند.
        var allSessionsInScope = await LoadAllSessionsInScopeAsync(survey, request.Filter, ct);

        // اگر هیچ پاسخ ارسال‌شده‌ای وجود ندارد، عکس‌العمل تهی است؛ در این حالت
        // هیچ ردیفی ذخیره نمی‌شود تا جداول تحلیلات فقط داده‌ی معنی‌دار داشته باشند.
        if (submittedSessions.Count == 0 && allSessionsInScope.Count == 0)
        {
            var empty = new SurveyAnalyticsDto
            {
                SurveyId = survey.Id,
                SurveyCode = survey.Code,
                SurveyTitle = survey.Localizations.Pick(Language.Fa)?.Title
                    ?? survey.Localizations.FirstOrDefault()?.Title
                    ?? survey.Code,
                IsAnonymous = survey.IsAnonymous,
                CanSegmentByOrgUnit = !survey.IsAnonymous,
                AppliedFilter = request.Filter,
                ComputedAt = DateTime.UtcNow
            };

            return Result.Success(empty);
        }

        var snapshot = await _engine.ComputeSnapshotAsync(
            survey, structure, submittedSessions, allSessionsInScope, request, ct);

        // --- ذخیره‌ی عکس‌العمل (جایگزینی در هر محاسبه) ---------------------------
        var metric = await GetOrCreateMetricAsync(survey, request, ct);
        metric.Update(snapshot);

        _analyticsRepository.Update(metric);

        metric.RaiseDomainEvent(new AnalyticsComputedEvent(
            survey.Id,
            survey.Code,
            request.SegmentType,
            metric.Id,
            snapshot.TotalSessions,
            snapshot.CompletedSessions,
            snapshot.NpsScore,
            snapshot.CsatScore,
            snapshot.CesScore,
            _currentUserService.UserId));

        await _unitOfWork.SaveChangesAsync(ct);

        var questions = DeserializeQuestionMetrics(snapshot.QuestionMetrics);

        return Result.Success(ToDto(survey, metric, questions));
    }

    /// <inheritdoc/>
    public async Task<Result<SurveyAnalyticsDto>> GetAsync(
        Guid surveyId, AnalyticsFilter filter, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(surveyId, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyAnalyticsDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        if (survey.Status == SurveyStatus.Archived)
        {
            return Result.Failure<SurveyAnalyticsDto>("survey_archived", "این نظرسنجی بایگانی شده و تحلیلی ندارد.");
        }

        // عکس‌العمل ذخیره‌شده (در صورت موجود بودن). این مسیر فقط خواندنی است:
        // هیچ عکس‌العملی ذخیره یا به‌روزرسانی نمی‌شود. اگر عکس‌العملی هنوز وجود
        // نداشته باشد (مثلاً نظرسنجی تازه منتشر شده و هنوز پاسخی نیامده)، یک
        // نتیجه‌ی تهی برمی‌گردد تا کلاینت بداند داده‌ای وجود ندارد.
        //
        // **چرا محاسبه‌ی خودکار اینجا انجام نمی‌شود:** ذخیره‌سازی فقط از طریق
        // ComputeAsync صریح یا شنونده‌ی رویداد «ارسال پاسخ» انجام می‌شود. محاسبه‌ی
        // ضمنی در مسیر GET یک副作用 غیرمنتظره است: می‌توانست یک ردیف تحلیلی
        // ایجاد کند و رویداد ممیزی ثبت کند فقط به خاطر باز کردن یک صفحه.
        var existing = await _analyticsRepository.FindAsync(
            surveyId, AnalyticsSegment.Survey, null, filter.CampaignId, null, ct);

        if (existing is null)
        {
            return Result.Success(BuildEmptySurveyAnalytics(survey, filter));
        }

        var questions = DeserializeQuestionMetrics(existing.QuestionMetrics);

        return Result.Success(ToDto(survey, existing, questions));
    }

    /// <summary>ساخت خروجی تهی برای نظرسنجی بدون عکس‌العمل (بدون ذخیره‌سازی).</summary>
    private static SurveyAnalyticsDto BuildEmptySurveyAnalytics(SurveyEntity survey, AnalyticsFilter filter)
    {
        return new SurveyAnalyticsDto
        {
            SurveyId = survey.Id,
            SurveyCode = survey.Code,
            SurveyTitle = survey.Localizations.Pick(Language.Fa)?.Title
                ?? survey.Localizations.FirstOrDefault()?.Title
                ?? survey.Code,
            IsAnonymous = survey.IsAnonymous,
            CanSegmentByOrgUnit = !survey.IsAnonymous,
            AppliedFilter = filter,
            ComputedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc/>
    public async Task<Result<AnalyticsDashboardDto>> GetDashboardAsync(
        AnalyticsFilter filter, CancellationToken ct = default)
    {
        // فقط نظرسنجی‌هایی که در دامنه‌ی قابل‌مشاهده‌ی کاربر هستند.
        var visibleSurveys = await ListVisibleSurveysAsync(ct);

        if (visibleSurveys.Count == 0)
        {
            return Result.Success(BuildEmptyDashboard(filter));
        }

        var surveyIds = visibleSurveys.Select(s => s.Id).ToList();

        // --- روند زمانی ارسال پاسخ‌ها -------------------------------------------
        var summaries = await _responseRepository.ListSubmittedAsync(surveyIds, filter.From, filter.To, ct);

        // فیلتر سازمانی روند: برای نظرسنجی‌های ناشناس هیچ پیوند سازمانی نیست.
        var anonymousSurveyIds = visibleSurveys.Where(s => s.IsAnonymous).Select(s => s.Id).ToHashSet();
        var nonAnonymousSurveyIds = visibleSurveys.Where(s => !s.IsAnonymous).Select(s => s.Id).ToHashSet();

        var trend = BuildTrend(summaries, anonymousSurveyIds, nonAnonymousSurveyIds, filter, ct);

        // --- شاخص‌های ذخیره‌شده‌ی نظرسنجی‌ها --------------------------------------
        var metrics = await ListMetricsForSurveysAsync(surveyIds, ct);

        // فقط نظرسنجی‌هایی که عکس‌العمل دارند وارد میانگین می‌شوند.
        var metricsBySurvey = metrics
            .GroupBy(m => m.SurveyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var topSurveys = metricsBySurvey
            .Select(kvp => AggregateSurveyMetrics(kvp.Key, kvp.Value, visibleSurveys))
            .Where(dto => dto.CompletedSessions > 0)
            .OrderByDescending(dto => dto.CompletedSessions)
            .Take(10)
            .ToList();

        var avgNps = topSurveys
            .Where(m => m.NpsScore.HasValue)
            .Select(m => m.NpsScore!.Value)
            .ToList();

        var avgCsat = topSurveys
            .Where(m => m.CsatScore.HasValue)
            .Select(m => m.CsatScore!.Value)
            .ToList();

        var avgCes = topSurveys
            .Where(m => m.CesScore.HasValue)
            .Select(m => m.CesScore!.Value)
            .ToList();

        var avgRating = topSurveys
            .Where(m => m.AverageRating.HasValue)
            .Select(m => m.AverageRating!.Value)
            .ToList();

        var totalSessions = topSurveys.Sum(m => m.TotalSessions);
        var completedSessions = topSurveys.Sum(m => m.CompletedSessions);

        // --- شمارش‌های کمپین و نظرسنجی -------------------------------------------
        var activeSurveys = visibleSurveys.Count(s => s.Status == SurveyStatus.Active);

        var campaigns = await ListVisibleCampaignsAsync(visibleSurveys.Select(s => s.Id).ToList(), ct);
        var activeCampaigns = campaigns.Count(c => c.Status == CampaignStatus.Running);

        return Result.Success(new AnalyticsDashboardDto
        {
            TotalSurveys = visibleSurveys.Count,
            ActiveSurveys = activeSurveys,
            TotalCampaigns = campaigns.Count,
            ActiveCampaigns = activeCampaigns,
            TotalSessions = totalSessions,
            CompletedSessions = completedSessions,
            CompletionRate = totalSessions > 0
                ? (decimal)completedSessions / totalSessions * 100m
                : 0m,
            AverageNps = avgNps.Count > 0 ? avgNps.Average() : null,
            AverageCsat = avgCsat.Count > 0 ? avgCsat.Average() : null,
            AverageCes = avgCes.Count > 0 ? avgCes.Average() : null,
            AverageRating = avgRating.Count > 0 ? avgRating.Average() : null,
            Trend = trend,
            TopSurveys = topSurveys,
            From = filter.From,
            To = filter.To
        });
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<TrendPointDto>>> GetTrendAsync(
        TrendRequest request, CancellationToken ct = default)
    {
        List<Guid> surveyIds;

        if (request.SurveyId is { } singleSurveyId)
        {
            surveyIds = [singleSurveyId];
        }
        else
        {
            var visibleSurveys = await ListVisibleSurveysAsync(ct);
            surveyIds = visibleSurveys.Select(s => s.Id).ToList();
        }

        if (surveyIds.Count == 0)
            return Result.Success<IReadOnlyList<TrendPointDto>>([]);

        var summaries = await _responseRepository.ListSubmittedAsync(surveyIds, request.From, request.To, ct);

        return Result.Success<IReadOnlyList<TrendPointDto>>(BuildTrendPoints(summaries, request.Period));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<SurveyMetricSummaryDto>> SearchAsync(
        AnalyticsSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _analyticsRepository.CountAsync(request, ct);
        var metrics = await _analyticsRepository.SearchAsync(request, ct);

        return new PagedResult<SurveyMetricSummaryDto>
        {
            Items = metrics.Select(ToSummaryDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<AnalyticsSegmentDto>>> GetSegmentsByOrgUnitAsync(
        Guid surveyId, AnalyticsFilter filter, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(surveyId, ct);
        if (survey is null)
        {
            return Result.Failure<IReadOnlyList<AnalyticsSegmentDto>>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        // نظرسنجی ناشناس هیچ پیوند سازمانی ندارد — بخش‌بندی ممکن نیست.
        if (survey.IsAnonymous)
        {
            return Result.Success<IReadOnlyList<AnalyticsSegmentDto>>([]);
        }

        var sessions = await _engine.LoadSessionsAsync(survey, filter, ct);
        if (sessions.Count == 0)
            return Result.Success<IReadOnlyList<AnalyticsSegmentDto>>([]);

        // این مسیر با مجوز جداگانه (<c>analytics.department.view</c>) در دسترس است
        // و باید فقط بخش‌های قابل‌مشاهده‌ی کاربر را برگرداند (fail-closed).
        sessions = await _engine.ApplyOrgScopeFilterAsync(
            sessions, survey.IsAnonymous, filter.OrgUnitId, filter.IncludeDescendants, ct);

        if (sessions.Count == 0)
            return Result.Success<IReadOnlyList<AnalyticsSegmentDto>>([]);

        var employeeIds = sessions
            .Where(s => s.RespondentEmployeeId.HasValue)
            .Select(s => s.RespondentEmployeeId!.Value)
            .Distinct()
            .ToList();

        var employeePaths = await _engine.ResolveOrgUnitPathsAsync(employeeIds, ct);

        var segments = sessions
            .Where(s => s.RespondentEmployeeId.HasValue)
            .GroupBy(s => employeePaths.GetValueOrDefault(s.RespondentEmployeeId!.Value) ?? string.Empty)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g =>
            {
                var total = g.Count();
                var completed = g.Count(s => s.IsSubmitted);

                return new AnalyticsSegmentDto
                {
                    Label = g.Key,
                    TotalSessions = total,
                    CompletedSessions = completed,
                    CompletionRate = total > 0
                        ? (decimal)completed / total * 100m
                        : 0m,
                    AverageRating = ComputeAverageRating(g.ToList())
                };
            })
            .OrderByDescending(s => s.CompletedSessions)
            .ToList();

        return Result.Success<IReadOnlyList<AnalyticsSegmentDto>>(segments);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<BenchmarkComparisonDto>>> CompareWithBenchmarksAsync(
        Guid surveyId, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(surveyId, ct);
        if (survey is null)
        {
            return Result.Failure<IReadOnlyList<BenchmarkComparisonDto>>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        var metrics = await _analyticsRepository.ListBySurveyAsync(surveyId, ct);

        if (metrics.Count == 0)
            return Result.Success<IReadOnlyList<BenchmarkComparisonDto>>([]);

        var orgUnitPath = metrics
            .Where(m => !string.IsNullOrWhiteSpace(m.OrgUnitPath))
            .Select(m => m.OrgUnitPath)
            .FirstOrDefault();

        var aggregated = AggregateSurveyMetrics(surveyId, metrics, [survey]);

        var comparisons = new List<BenchmarkComparisonDto>();

        await AddComparisonAsync(comparisons, MetricType.Nps, aggregated.NpsScore, orgUnitPath, ct);
        await AddComparisonAsync(comparisons, MetricType.Csat, aggregated.CsatScore, orgUnitPath, ct);
        await AddComparisonAsync(comparisons, MetricType.Ces, aggregated.CesScore, orgUnitPath, ct);

        return Result.Success<IReadOnlyList<BenchmarkComparisonDto>>(comparisons);
    }

    /// <summary>افزودن یک مقایسه‌ی شاخص با بنچمارک‌های قابل‌اعمال.</summary>
    private async Task AddComparisonAsync(
        List<BenchmarkComparisonDto> comparisons,
        MetricType metric,
        decimal? actualValue,
        string? orgUnitPath,
        CancellationToken ct)
    {
        var benchmarks = await _benchmarkRepository.GetApplicableAsync(metric, orgUnitPath, ct);

        // اگر بنچمارکی نبود، مقایسه‌ای هم نیست (شاخص بدون هدف نمایش داده می‌شود).
        if (benchmarks.Count == 0 && actualValue.HasValue)
        {
            comparisons.Add(new BenchmarkComparisonDto
            {
                Metric = metric,
                ActualValue = actualValue,
                TargetValue = 0m,
                BenchmarkName = string.Empty
            });

            return;
        }

        foreach (var benchmark in benchmarks)
        {
            comparisons.Add(new BenchmarkComparisonDto
            {
                Metric = metric,
                ActualValue = actualValue,
                TargetValue = benchmark.TargetValue,
                BenchmarkName = benchmark.Name
            });
        }
    }

    // --- کمک‌کننده‌های خواندن ----------------------------------------------------

    /// <summary>
    /// همه‌ی نشست‌های (شروع‌شده و ارسال‌شده) یک نظرسنجی. این مجموعه مخرج نرخ
    /// تکمیل است و باید همه‌ی نشست‌های شروع‌شده را شامل شود — در غیر این صورت
    /// نرخ تکمیل همواره ۱۰۰٪ می‌شد.
    ///
    /// <b>چرا فیلتر دامنه‌ی سازمانی اعمال نمی‌شود:</b> این متد فقط مخرج (تعداد)
    /// را برمی‌گرداند و هیچ پاسخ خام یا شناسه‌ی پاسخ‌گویی را جابه‌جا نمی‌کند.
    /// بخش‌بندی سازمانی فقط در <see cref="GetSegmentsByOrgUnitAsync"/> و با مجوز
    /// جداگانه انجام می‌شود.
    /// </summary>
    private async Task<IReadOnlyList<ResponseSession>> LoadAllSessionsInScopeAsync(
        SurveyEntity survey, AnalyticsFilter filter, CancellationToken ct)
    {
        var sessions = await _responseRepository.ListAllBySurveyAsync(
            survey.Id, filter.From, filter.To, ct);

        // محدودسازی به کمپین در صورت درخواست.
        if (filter.CampaignId is { } campaignId)
            sessions = sessions.Where(s => s.CampaignId == campaignId).ToList();

        return sessions;
    }

    /// <summary>
    /// نظرسنجی‌های قابل‌مشاهده توسط کاربر جاری. در این فاز، کاربری که مجوز
    /// مشاهده‌ی تحلیلات دارد و دامنه‌ی سازمانی قابل‌مشاهده‌ای دارد، نظرسنجی‌های
    /// پاسخ‌گویانش را می‌بیند. چون نظرسنجی‌ها محتوایی هستند (دامنه‌ی سازمانی
    /// روی آن‌ها اعمال نمی‌شود)، فیلتر اصلی روی <b>عکس‌العمل‌های تحلیلات</b> و
    /// <b>پاسخ‌ها</b> اعمال می‌شود، نه تعریف نظرسنجی.
    /// </summary>
    private async Task<IReadOnlyList<SurveyEntity>> ListVisibleSurveysAsync(CancellationToken ct)
    {
        var surveys = await _surveyRepository.SearchAsync(new SurveySearchRequest
        {
            IncludeArchived = false,
            Page = 1,
            PageSize = 200
        }, ct);

        return surveys
            .Where(s => s.Status != SurveyStatus.Archived)
            .ToList();
    }

    /// <summary>عکس‌العمل‌های تحلیلات چند نظرسنجی.</summary>
    private async Task<IReadOnlyList<SurveyMetricEntity>> ListMetricsForSurveysAsync(
        IReadOnlyCollection<Guid> surveyIds, CancellationToken ct)
    {
        var result = new List<SurveyMetricEntity>();

        foreach (var surveyId in surveyIds)
        {
            var metrics = await _analyticsRepository.ListBySurveyAsync(surveyId, ct);
            result.AddRange(metrics);
        }

        return result;
    }

    /// <summary>کمپین‌های مرتبط با نظرسنجی‌های قابل‌مشاهده.</summary>
    private async Task<IReadOnlyList<Domain.Modules.Campaign.Entities.Campaign>> ListVisibleCampaignsAsync(
        List<Guid> surveyIds, CancellationToken ct)
    {
        if (surveyIds.Count == 0)
            return [];

        var campaigns = await _campaignRepository.SearchAsync(new CampaignSearchRequest
        {
            IncludeArchived = false,
            Page = 1,
            PageSize = 200
        }, ct);

        return campaigns
            .Where(c => surveyIds.Contains(c.SurveyId))
            .ToList();
    }

    // --- کمک‌کننده‌های محاسبه ---------------------------------------------------

    /// <summary>میانگین امتیاز یک مجموعه نشست.</summary>
    private static decimal? ComputeAverageRating(IReadOnlyList<ResponseSession> sessions)
    {
        var values = sessions
            .SelectMany(s => s.Answers)
            .Where(a => a.QuestionType == QuestionType.Rating && a.NumericValue.HasValue)
            .Select(a => a.NumericValue!.Value)
            .ToList();

        return values.Count > 0 ? values.Average() : null;
    }

    /// <summary>ساخت روند زمانی از خلاصه‌ی نشست‌ها.</summary>
    private static List<TrendPointDto> BuildTrend(
        IReadOnlyList<SubmittedSessionSummary> summaries,
        HashSet<Guid> anonymousSurveyIds,
        HashSet<Guid> nonAnonymousSurveyIds,
        AnalyticsFilter filter,
        CancellationToken ct)
    {
        // در فاز ۵ روند زمانی فقط شامل شمارش ارسال است؛ فیلتر سازمانی روی
        // روند از طریق فیلتر پاسخ‌ها (در آینده) اعمال می‌شود.
        _ = anonymousSurveyIds;
        _ = nonAnonymousSurveyIds;
        _ = filter;
        _ = ct;

        return BuildTrendPoints(summaries, AnalyticsPeriod.Day);
    }

    /// <summary>ساخت نقاط روند زمانی با دانه‌بندی داده‌شده.</summary>
    private static List<TrendPointDto> BuildTrendPoints(
        IReadOnlyList<SubmittedSessionSummary> summaries, AnalyticsPeriod period)
    {
        if (summaries.Count == 0)
            return [];

        var grouped = summaries
            .GroupBy(s => GetPeriodStart(s.SubmittedAt, period))
            .OrderBy(g => g.Key)
            .ToList();

        var cumulative = 0;
        var points = new List<TrendPointDto>(grouped.Count);

        foreach (var group in grouped)
        {
            cumulative += group.Count();

            points.Add(new TrendPointDto
            {
                PeriodLabel = FormatPeriodLabel(group.Key, period),
                PeriodStart = group.Key,
                Count = group.Count(),
                CumulativeCount = cumulative
            });
        }

        return points;
    }

    /// <summary>شروع دوره‌ی زمانی یک تاریخ.</summary>
    private static DateTime GetPeriodStart(DateTime submittedAt, AnalyticsPeriod period) => period switch
    {
        AnalyticsPeriod.Month => new DateTime(submittedAt.Year, submittedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        // هفته‌ی شروع‌شده با روز دوشنبه (ISO 8601).
        AnalyticsPeriod.Week => GetWeekStart(submittedAt),
        _ => new DateTime(submittedAt.Year, submittedAt.Month, submittedAt.Day, 0, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>شروع هفته (دوشنبه) یک تاریخ.</summary>
    private static DateTime GetWeekStart(DateTime date)
    {
        var offset = (int)date.DayOfWeek;

        // در DateTime.DayOfWeek یکشنبه ۰ است؛ آن را به انتهای هفته می‌بریم.
        if (offset == 0)
            offset = 7;

        var monday = date.AddDays(-(offset - 1));

        return new DateTime(monday.Year, monday.Month, monday.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>برچسب نمایشی یک دوره.</summary>
    private static string FormatPeriodLabel(DateTime periodStart, AnalyticsPeriod period) => period switch
    {
        AnalyticsPeriod.Month => $"{periodStart.Year:0000}-{periodStart.Month:00}",
        AnalyticsPeriod.Week => $"{periodStart:yyyy-MM-dd}",
        _ => $"{periodStart:yyyy-MM-dd}"
    };

    // --- کمک‌کننده‌های نگاشت ----------------------------------------------------

    /// <summary>یافتن یا ساخت عکس‌العمل یک بُعد.</summary>
    private async Task<SurveyMetricEntity> GetOrCreateMetricAsync(
        SurveyEntity survey, ComputeAnalyticsRequest request, CancellationToken ct)
    {
        var existing = await _analyticsRepository.FindAsync(
            survey.Id,
            request.SegmentType,
            request.SegmentType == AnalyticsSegment.OrgUnit ? request.OrgUnitId : null,
            request.SegmentType == AnalyticsSegment.Campaign ? request.CampaignId : null,
            null,
            ct);

        if (existing is not null)
            return existing;

        var metric = new SurveyMetricEntity
        {
            SurveyId = survey.Id,
            SurveyCode = survey.Code,
            SurveyTitle = survey.Localizations.Pick(Language.Fa)?.Title
                ?? survey.Localizations.FirstOrDefault()?.Title
                ?? survey.Code,
            IsAnonymous = survey.IsAnonymous,
            SegmentType = request.SegmentType,
            CampaignId = request.SegmentType == AnalyticsSegment.Campaign ? request.CampaignId : null,
            WindowStart = request.Filter.From ?? DateTime.MinValue,
            WindowEnd = request.Filter.To ?? DateTime.UtcNow
        };

        if (request.SegmentType == AnalyticsSegment.OrgUnit && request.OrgUnitId is { } orgUnitId)
        {
            metric.OrgUnitId = orgUnitId;
            metric.OrgUnitPath = await _orgUnitRepository.GetPathAsync(orgUnitId, ct);
        }

        // موجودیت جدید در اینجا Add نمی‌شود. فراخوانیِ Update در ComputeAsync آن
        // را ردیابی می‌کند و رویداد Tracked در UnitOfWork به‌صورت خودکار Add را
        // به Update تبدیل می‌کند (الگوی مرسوم این ماژول). اگر اینجا AddAsync
        // فراخوانی می‌شد، موجودیت در حالت Added ردیابی می‌شد و سپس Update آن را
        // به Modified تبدیل می‌کرد — یعنی UPDATE برای ردیفی که هنوز INSERT
        // نشده و در نتیجه DbUpdateConcurrencyException.
        return metric;
    }

    /// <summary>تبدیل عکس‌العمل ذخیره‌شده به DTO.</summary>
    private static SurveyAnalyticsDto ToDto(
        SurveyEntity survey,
        SurveyMetricEntity metric,
        IReadOnlyList<QuestionMetricDto> questions)
    {
        return new SurveyAnalyticsDto
        {
            MetricId = metric.Id,
            SurveyId = survey.Id,
            SurveyCode = survey.Code,
            SurveyTitle = survey.Localizations.Pick(Language.Fa)?.Title
                ?? survey.Localizations.FirstOrDefault()?.Title
                ?? survey.Code,
            IsAnonymous = survey.IsAnonymous,
            CanSegmentByOrgUnit = !survey.IsAnonymous,
            TotalSessions = metric.TotalSessions,
            CompletedSessions = metric.CompletedSessions,
            CompletionRate = metric.CompletionRate,
            NpsScore = metric.NpsScore,
            NpsPromoters = metric.NpsPromoters,
            NpsPassives = metric.NpsPassives,
            NpsDetractors = metric.NpsDetractors,
            CsatScore = metric.CsatScore,
            CesScore = metric.CesScore,
            AverageRating = metric.AverageRating,
            Questions = questions,
            ComputedAt = metric.ComputedAt
        };
    }

    /// <summary>تبدیل عکس‌العمل ذخیره‌شده به DTO خلاصه.</summary>
    private SurveyMetricSummaryDto ToSummaryDto(SurveyMetricEntity metric)
    {
        return new SurveyMetricSummaryDto
        {
            Id = metric.Id,
            SurveyId = metric.SurveyId,
            SurveyCode = metric.SurveyCode,
            SurveyTitle = metric.SurveyTitle,
            IsAnonymous = metric.IsAnonymous,
            SegmentType = metric.SegmentType,
            SegmentLabel = metric.SegmentType switch
            {
                AnalyticsSegment.Campaign => metric.CampaignCode,
                AnalyticsSegment.OrgUnit => metric.OrgUnitPath,
                _ => null
            },
            TotalSessions = metric.TotalSessions,
            CompletedSessions = metric.CompletedSessions,
            CompletionRate = metric.CompletionRate,
            NpsScore = metric.NpsScore,
            CsatScore = metric.CsatScore,
            CesScore = metric.CesScore,
            AverageRating = metric.AverageRating,
            ResponseRate = metric.ResponseRate,
            WindowStart = metric.WindowStart,
            WindowEnd = metric.WindowEnd,
            ComputedAt = metric.ComputedAt
        };
    }

    /// <summary>تجمیع چند عکس‌العمل یک نظرسنجی در یک خلاصه.</summary>
    private static SurveyMetricSummaryDto AggregateSurveyMetrics(
        Guid surveyId,
        IReadOnlyList<SurveyMetricEntity> metrics,
        IReadOnlyList<SurveyEntity> surveys)
    {
        var survey = surveys.FirstOrDefault(s => s.Id == surveyId);
        var primary = metrics.FirstOrDefault(m => m.SegmentType == AnalyticsSegment.Survey) ?? metrics[0];

        return new SurveyMetricSummaryDto
        {
            Id = primary.Id,
            SurveyId = surveyId,
            SurveyCode = primary.SurveyCode,
            SurveyTitle = primary.SurveyTitle,
            IsAnonymous = primary.IsAnonymous,
            SegmentType = AnalyticsSegment.Survey,
            TotalSessions = metrics.Sum(m => m.TotalSessions),
            CompletedSessions = metrics.Sum(m => m.CompletedSessions),
            CompletionRate = metrics.Sum(m => m.TotalSessions) > 0
                ? (decimal)metrics.Sum(m => m.CompletedSessions) / metrics.Sum(m => m.TotalSessions) * 100m
                : 0m,
            NpsScore = metrics.Where(m => m.NpsScore.HasValue).Select(m => m.NpsScore!.Value).ToList() switch
            {
                { Count: > 0 } list => list.Average(),
                _ => null
            },
            CsatScore = metrics.Where(m => m.CsatScore.HasValue).Select(m => m.CsatScore!.Value).ToList() switch
            {
                { Count: > 0 } list => list.Average(),
                _ => null
            },
            CesScore = metrics.Where(m => m.CesScore.HasValue).Select(m => m.CesScore!.Value).ToList() switch
            {
                { Count: > 0 } list => list.Average(),
                _ => null
            },
            AverageRating = metrics.Where(m => m.AverageRating.HasValue).Select(m => m.AverageRating!.Value).ToList() switch
            {
                { Count: > 0 } list => list.Average(),
                _ => null
            },
            ResponseRate = null,
            WindowStart = metrics.Min(m => m.WindowStart),
            WindowEnd = metrics.Max(m => m.WindowEnd),
            ComputedAt = metrics.Max(m => m.ComputedAt)
        };
    }

    /// <summary>دریافت JSON تحلیل سؤال‌ها و تبدیل به DTO.</summary>
    private static List<QuestionMetricDto> DeserializeQuestionMetrics(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return [];

        try
        {
            var metrics = JsonSerializer.Deserialize<List<QuestionMetric>>(json, JsonOptions);

            if (metrics is null || metrics.Count == 0)
                return [];

            return metrics
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new QuestionMetricDto
                {
                    QuestionId = m.QuestionId,
                    QuestionCode = m.QuestionCode,
                    QuestionText = m.QuestionText,
                    QuestionType = m.QuestionType,
                    DisplayOrder = m.DisplayOrder,
                    ResponseCount = m.ResponseCount,
                    ResponseRate = m.ResponseRate,
                    Options = m.Options?.Select(o => new OptionDistributionDto
                    {
                        OptionId = o.OptionId,
                        OptionCode = o.OptionCode,
                        OptionText = o.OptionText,
                        DisplayOrder = o.DisplayOrder,
                        Count = o.Count,
                        Percentage = o.Percentage
                    }).ToList() ?? [],
                    NumericStats = m.NumericStats is not null
                        ? new NumericStatsDto
                        {
                            Mean = m.NumericStats.Mean,
                            Median = m.NumericStats.Median,
                            Min = m.NumericStats.Min,
                            Max = m.NumericStats.Max,
                            StandardDeviation = m.NumericStats.StandardDeviation,
                            ResponseCount = m.NumericStats.ResponseCount,
                            RatingBuckets = m.NumericStats.RatingBuckets?.Select(b => new RatingBucketDto
                            {
                                Value = b.Value,
                                Count = b.Count,
                                Percentage = b.Percentage
                            }).ToList() ?? []
                        }
                        : null,
                    YesNoDistribution = m.YesNoDistribution is not null
                        ? new YesNoDistributionDto
                        {
                            YesCount = m.YesNoDistribution.YesCount,
                            NoCount = m.YesNoDistribution.NoCount,
                            YesPercentage = (m.YesNoDistribution.YesCount + m.YesNoDistribution.NoCount) > 0
                                ? (decimal)m.YesNoDistribution.YesCount / (m.YesNoDistribution.YesCount + m.YesNoDistribution.NoCount) * 100m
                                : 0m,
                            NoPercentage = (m.YesNoDistribution.YesCount + m.YesNoDistribution.NoCount) > 0
                                ? (decimal)m.YesNoDistribution.NoCount / (m.YesNoDistribution.YesCount + m.YesNoDistribution.NoCount) * 100m
                                : 0m
                        }
                        : null,
                    DetectedMetric = m.DetectedMetric
                })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>ساخت داشبورد خالی (در صورت نبودن داده‌ی قابل‌مشاهده).</summary>
    private static AnalyticsDashboardDto BuildEmptyDashboard(AnalyticsFilter filter)
    {
        return new AnalyticsDashboardDto
        {
            TotalSurveys = 0,
            ActiveSurveys = 0,
            TotalCampaigns = 0,
            ActiveCampaigns = 0,
            TotalSessions = 0,
            CompletedSessions = 0,
            CompletionRate = 0m,
            AverageNps = null,
            AverageCsat = null,
            AverageCes = null,
            AverageRating = null,
            Trend = [],
            TopSurveys = [],
            From = filter.From,
            To = filter.To
        };
    }
}
