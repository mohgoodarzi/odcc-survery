using Microsoft.AspNetCore.Mvc;
using ODCC.Api.Authorization;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Domain.Modules.Analytics.Enums;

namespace ODCC.Api.Controllers.Analytics;

/// <summary>
/// تحلیلات و داشبورد: شاخص‌های NPS/CSAT/CES، توزیع پاسخ سؤال‌ها، روند زمانی،
/// بخش‌بندی سازمانی و مقایسه با بنچمارک‌ها.
///
/// **حریم خصوصی:** همه‌ی خروجی‌ها فقط تجمع هستند. هیچ شناسه‌ی پاسخ‌گویی
/// (کاربر، کارمند یا نام) بازگردانده نمی‌شود. برای نظرسنجی‌های ناشناس،
/// بخش‌بندی سازمانی در دسترس نیست چون هیچ پیوند سازمانی ذخیره نشده است.
/// </summary>
[ApiController]
[Route("api/{culture:language}/analytics")]
public sealed class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
{
    private readonly IAnalyticsService _analyticsService = analyticsService;

    /// <summary>داشبورد تحلیلات سطح شرکت (با در نظر گرفتن دامنه‌ی سازمانی کاربر).</summary>
    [HttpGet("dashboard")]
    [HasPermission(Permissions.Analytics.View)]
    [ProducesResponseType(typeof(AnalyticsDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalyticsDashboardDto>> Dashboard(
        [FromQuery] AnalyticsFilter filter,
        CancellationToken ct)
    {
        var result = await _analyticsService.GetDashboardAsync(filter, ct);
        return Ok(result.Value);
    }

    /// <summary>
    /// دریافت تحلیلات ذخیره‌شده‌ی یک نظرسنجی. اگر عکس‌العملی وجود نداشته باشد،
    /// یک محاسبه‌ی اولیه انجام می‌شود.
    /// </summary>
    [HttpGet("surveys/{surveyId:guid}")]
    [HasPermission(Permissions.Analytics.View)]
    [ProducesResponseType(typeof(SurveyAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SurveyAnalyticsDto>> GetSurveyAnalytics(
        Guid surveyId,
        [FromQuery] AnalyticsFilter filter,
        CancellationToken ct)
    {
        var result = await _analyticsService.GetAsync(surveyId, filter, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "تحلیلات این نظرسنجی در دسترس نیست",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// محاسبه‌ (یا محاسبه‌ی مجدد) تحلیلات یک نظرسنجی و ذخیره‌ی عکس‌العمل آن.
    /// این عملیات پس از هر ارسال پاسخ به‌صورت خودکار هم انجام می‌شود.
    /// </summary>
    [HttpPost("surveys/{surveyId:guid}/compute")]
    [HasPermission(Permissions.Analytics.View)]
    [ProducesResponseType(typeof(SurveyAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SurveyAnalyticsDto>> Compute(
        Guid surveyId,
        [FromBody] ComputeAnalyticsRequest? request,
        CancellationToken ct)
    {
        // بدنه‌ی درخواست اختیاری است؛ مسیر، شناسه‌ی نظرسنجی را تأمین می‌کند.
        var compute = request ?? new ComputeAnalyticsRequest { SurveyId = surveyId };
        compute = compute with { SurveyId = surveyId };

        var result = await _analyticsService.ComputeAsync(compute, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code is "survey_not_found" or "survey_archived")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "نظرسنجی یافت نشد",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.Message,
                    Extensions = { ["code"] = result.Error.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "محاسبه‌ی تحلیلات ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>روند زمانی ارسال پاسخ‌ها (روزانه، هفتگی یا ماهانه).</summary>
    [HttpGet("trend")]
    [HasPermission(Permissions.Analytics.View)]
    [ProducesResponseType(typeof(IReadOnlyList<TrendPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrendPointDto>>> Trend(
        [FromQuery] TrendRequest request,
        CancellationToken ct)
    {
        var result = await _analyticsService.GetTrendAsync(request, ct);
        return Ok(result.Value);
    }

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی عکس‌العمل‌های تحلیلات ذخیره‌شده.</summary>
    [HttpGet("metrics")]
    [HasPermission(Permissions.Analytics.View)]
    [ProducesResponseType(typeof(PagedResult<SurveyMetricSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SurveyMetricSummaryDto>>> SearchMetrics(
        [FromQuery] AnalyticsSearchRequest request,
        CancellationToken ct)
    {
        var result = await _analyticsService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// بخش‌بندی پاسخ‌های یک نظرسنجی بر اساس واحد سازمانی پاسخ‌گو.
    /// برای نظرسنجی‌های ناشناس لیست خالی برمی‌گردد (هیچ پیوند سازمانی وجود ندارد).
    /// </summary>
    [HttpGet("surveys/{surveyId:guid}/segments/org-units")]
    [HasPermission(Permissions.Analytics.DepartmentView)]
    [ProducesResponseType(typeof(IReadOnlyList<AnalyticsSegmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AnalyticsSegmentDto>>> SegmentsByOrgUnit(
        Guid surveyId,
        [FromQuery] AnalyticsFilter filter,
        CancellationToken ct)
    {
        var result = await _analyticsService.GetSegmentsByOrgUnitAsync(surveyId, filter, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "بخش‌بندی این نظرسنجی در دسترس نیست",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>مقایسه‌ی شاخص‌های یک نظرسنجی با بنچمارک‌های قابل‌اعمال.</summary>
    [HttpGet("surveys/{surveyId:guid}/benchmarks")]
    [HasPermission(Permissions.Analytics.View)]
    [ProducesResponseType(typeof(IReadOnlyList<BenchmarkComparisonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BenchmarkComparisonDto>>> CompareWithBenchmarks(
        Guid surveyId,
        CancellationToken ct)
    {
        var result = await _analyticsService.CompareWithBenchmarksAsync(surveyId, ct);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "مقایسه با بنچمارک ممکن نیست",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}
