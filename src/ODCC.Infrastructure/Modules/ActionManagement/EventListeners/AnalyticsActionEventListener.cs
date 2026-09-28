using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.ActionManagement.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.ActionManagement.Enums;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Analytics.Events;
using ODCC.Infrastructure.Modules.ActionManagement.Services;

namespace ODCC.Infrastructure.Modules.ActionManagement.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای تحلیلات برای تولید خودکار برنامه‌های اقدام.
///
/// وقتی تحلیلات یک نظرسنجی محاسبه می‌شود و یک شاخص (NPS/CSAT/CES) زیر آستانه‌ی
/// هشدار می‌رود، این شنونده یک برنامه‌ی اقدام با منشأ <c>AnalyticsAlert</c>
/// ایجاد می‌کند. ایجاد آن <b>خودتوان</b> است: محاسبه‌ی مجدد تحلیلات برنامه‌ی
/// مضاعف نمی‌سازد، بلکه فقط مقدار شاخصِ خروجیِ برنامه‌ی موجود را به‌روز
/// می‌کند (سنجش اثربخشی زنده).
///
/// <b>چرا شنونده در ماژول مدیریت اقدامات است:</b> موجودیت‌های ساخته‌شده
/// (<c>ActionPlan</c>) متعلق به این ماژول‌اند (الگوی مجاز: ماژول الف رویدادی
/// منتشر می‌کند، ماژول ب گوش می‌دهد).
///
/// <b>حریم خصوصی (حیاتی):</b> رویداد تحلیلات فقط شاخص‌های تجمعی را حمل
/// می‌کند — هرگز شناسه‌ی پاسخ‌گو. این شنونده هم هیچ‌گاه چنین شناسه‌ای تولید
/// یا ذخیره نمی‌کند. برنامه‌ی تولیدشده فقط شاخص‌ها را به‌عنوان زمینه می‌بیند.
/// </summary>
public sealed class AnalyticsActionEventListener(
    IActionManagementService actionManagementService,
    IOptions<ActionAnalyticsOptions> options,
    ILogger<AnalyticsActionEventListener> logger) :
    IDomainEventListener<AnalyticsComputedEvent>
{
    private readonly IActionManagementService _actionManagementService = actionManagementService;
    private readonly ActionAnalyticsOptions _options = options.Value;
    private readonly ILogger<AnalyticsActionEventListener> _logger = logger;

    private static readonly Action<ILogger, Guid, Exception?> AlertFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(3, "AnalyticsActionAlertFailed"),
        "ایجاد برنامه‌ی اقدام خودکار برای نظرسنجی {SurveyId} ناموفق بود.");

    /// <inheritdoc/>
    public async Task HandleAsync(AnalyticsComputedEvent domainEvent, CancellationToken ct = default)
    {
        // فقط بخش‌بندی کل نظرسنجی هشدار می‌دهد؛ بخش‌بندی واحد سازمانی داده‌ی
        // کافی و معنای مدیریتی یکپارچه ندارد.
        if (domainEvent.SegmentType != AnalyticsSegment.Survey)
        {
            return;
        }

        // بدون داده‌ی کافی، هشدار نویز است.
        if (domainEvent.CompletedSessions < _options.MinResponsesForAlert)
        {
            return;
        }

        var alert = DetectAlert(domainEvent);

        if (alert is null)
        {
            return;
        }

        try
        {
            await _actionManagementService.EnsurePlanFromAnalyticsAlertAsync(alert, ct);
        }
        catch (Exception ex)
        {
            // شکست تولید برنامه‌ی خودکار نباید محاسبه‌ی تحلیلات را لغو کند.
            AlertFailed(_logger, domainEvent.SurveyId, ex);
        }
    }

    /// <summary>
    /// تشخیص اینکه آیا یکی از شاخص‌ها زیر آستانه‌ی هشدار است. اولین شاخصِ
    /// پایین‌تر از آستانه برنده است (NPS اولویت دارد).
    /// </summary>
    private AnalyticsAlertRequest? DetectAlert(AnalyticsComputedEvent domainEvent)
    {
        var metric = ActionMetricType.Nps;
        var value = domainEvent.NpsScore;
        var threshold = _options.NpsAlertThreshold;

        if (value is null || value >= threshold)
        {
            metric = ActionMetricType.Csat;
            value = domainEvent.CsatScore;
            threshold = _options.CsatAlertThreshold;
        }

        if (value is null || value >= threshold)
        {
            metric = ActionMetricType.Ces;
            value = domainEvent.CesScore;
            threshold = _options.CesAlertThreshold;
        }

        if (value is null || value >= threshold)
        {
            return null;
        }

        var metricLabel = metric switch
        {
            ActionMetricType.Csat => "CSAT",
            ActionMetricType.Ces => "CES",
            _ => "NPS"
        };

        return new AnalyticsAlertRequest
        {
            SurveyId = domainEvent.SurveyId,
            SurveyCode = domainEvent.SurveyCode,
            SurveyTitle = string.Empty, // عنوان از سمت سرویس پایگاه داده خوانده نمی‌شود؛ شنونده فقط کد دارد
            SegmentKey = domainEvent.SegmentType.ToString().ToLowerInvariant(),
            MetricType = metric,
            MetricValue = value.Value,
            TargetValue = threshold,
            OrgUnitId = null,
            OrgUnitPath = null,
            SuggestedTitle = $"برنامه‌ی اقدام: افت شاخص {metricLabel} در نظرسنجی «{domainEvent.SurveyCode}»",
            ActorUserId = domainEvent.ActorUserId
        };
    }
}

/// <summary>
/// تنظیمات هشدارهای خودکار تحلیلات → اقدام، از بخش <c>Actions:AnalyticsAlerts</c>.
/// </summary>
public sealed class ActionAnalyticsOptions
{
    public const string SectionName = "Actions:AnalyticsAlerts";

    /// <summary>حداقل تعداد پاسخِ ارسال‌شده برای تولید هشدار (جلوگیری از نویز).</summary>
    public int MinResponsesForAlert { get; set; } = 5;

    /// <summary>آستانه‌ی هشدار NPS (زیر این مقدار = هشدار).</summary>
    public decimal NpsAlertThreshold { get; set; } = 0m;

    /// <summary>آستانه‌ی هشدار CSAT (زیر این مقدار = هشدار).</summary>
    public decimal CsatAlertThreshold { get; set; } = 70m;

    /// <summary>آستانه‌ی هشدار CES (زیر این مقدار = هشدار).</summary>
    public decimal CesAlertThreshold { get; set; } = 60m;
}
