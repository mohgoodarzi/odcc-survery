using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;
using ODCC.Domain.Modules.Analytics.Events;
using ODCC.Domain.Modules.Response.Events;
using ODCC.Domain.Modules.Workflow.Events;

namespace ODCC.Infrastructure.Modules.Integration.EventListeners;

/// <summary>
/// پل بین ماژول‌های دامنه و ماژول یکپارچه‌سازی: تبدیل رویدادهای قابل‌اشتراک
/// به payloadهای وب‌هوک و ارسال آن‌ها به اندپوینت‌های خروجی فعال.
///
/// <b>چرا شنونده در ماژول یکپارچه‌سازی است:</b> موجودیت‌های ساخته‌شده
/// (<c>WebhookDelivery</c>) متعلق به این ماژول‌اند (الگوی مجاز: ماژول الف
/// رویدادی منتشر می‌کند و ماژول ب به آن گوش می‌دهد).
///
/// <b>حریم خصوصی (حیاتی):</b> این شنونده فقط متادیتای عمومی (شناسه‌ی
/// نظرسنجی، شاخص‌های تجمعی، کدهای وضعیت) را در payload می‌گذارد. <b>هرگز</b>
/// شناسه‌ی پاسخ‌گوی نظرسنجی، محتوای پاسخ شخصی یا رازها را ارسال نمی‌کند.
/// شاخص‌ها فقط تجمعی هستند (همان چیزی که ماژول تحلیلات محاسبه می‌کند).
/// </summary>
public sealed class IntegrationWebhookEventListener(
    IWebhookDispatcher webhookDispatcher,
    ILogger<IntegrationWebhookEventListener> logger) :
    IDomainEventListener<AnalyticsComputedEvent>,
    IDomainEventListener<ResponseSubmittedEvent>,
    IDomainEventListener<WorkflowInstanceTransitionedEvent>
{
    private readonly IWebhookDispatcher _webhookDispatcher = webhookDispatcher;
    private readonly ILogger<IntegrationWebhookEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception> DispatchFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1, "IntegrationEventDispatchFailed"),
        "ارسال رویداد یکپارچه‌سازی {EventType} ناموفق بود.");

    /// <summary>
    /// محاسبه‌ی تحلیلات کامل شد: شاخص‌های تجمعی نظرسنجی به وب‌هوک‌ها می‌روند.
    /// فقط داده‌ی تجمعی — هیچ شناسه‌ی پاسخ‌گویی وجود ندارد.
    /// </summary>
    public Task HandleAsync(AnalyticsComputedEvent domainEvent, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["surveyId"] = domainEvent.SurveyId,
            ["surveyCode"] = domainEvent.SurveyCode,
            ["segmentType"] = domainEvent.SegmentType.ToString(),
            ["metricId"] = domainEvent.MetricId,
            ["totalSessions"] = domainEvent.TotalSessions,
            ["completedSessions"] = domainEvent.CompletedSessions,
            ["nps"] = domainEvent.NpsScore,
            ["csat"] = domainEvent.CsatScore,
            ["ces"] = domainEvent.CesScore,
            ["computedAt"] = domainEvent.OccurredAt.ToString("O")
        };

        // شناسه‌ی یکتای پایدار:survey + بُعد + شاخص + زمان رویداد. بازپخشِ
        // همان رویداد همان شناسه را می‌دهد (idempotency)، ولی محاسبه‌های
        // مجزا شناسه‌های متفاوتی می‌دهند (تحویل مضاعف نمی‌شود).
        return DispatchAsync(
            "analytics.computed",
            $"{domainEvent.SurveyId}:{domainEvent.SegmentType}:{domainEvent.MetricId}",
            payload,
            domainEvent.OccurredAt,
            ct);
    }

    /// <summary>
    /// پاسخی ثبت شد: فقط شناسه‌ی نظرسنجی و متادیتای عمومی. <b>هیچ</b> شناسه‌ی
    /// پاسخ‌گو یا محتوای پاسخ ارسال نمی‌شود.
    /// </summary>
    public Task HandleAsync(ResponseSubmittedEvent domainEvent, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["surveyId"] = domainEvent.SurveyId,
            ["surveyCode"] = domainEvent.SurveyCode,
            ["campaignId"] = domainEvent.CampaignId,
            ["isAnonymous"] = domainEvent.IsAnonymous,
            ["answerCount"] = domainEvent.AnswerCount,
            ["submittedAt"] = domainEvent.OccurredAt.ToString("O")
        };

        // شناسه‌ی یکتای پایدار: نشست پاسخ‌گویی + زمان رویداد.
        return DispatchAsync(
            "response.submitted",
            domainEvent.SessionId.ToString(),
            payload,
            domainEvent.OccurredAt,
            ct);
    }

    /// <summary>
    /// گذار گردش کار انجام شد: متادیتای عمومی گذار.
    /// </summary>
    public Task HandleAsync(WorkflowInstanceTransitionedEvent domainEvent, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["instanceId"] = domainEvent.InstanceId,
            ["workflowCode"] = domainEvent.WorkflowCode,
            ["entityId"] = domainEvent.EntityId,
            ["fromState"] = domainEvent.FromStateCode,
            ["toState"] = domainEvent.ToStateCode,
            ["approved"] = domainEvent.Approved,
            ["transitionedAt"] = domainEvent.OccurredAt.ToString("O")
        };

        return DispatchAsync(
            "workflow.transitioned",
            $"{domainEvent.InstanceId}:{domainEvent.FromStateCode}->{domainEvent.ToStateCode}",
            payload,
            domainEvent.OccurredAt,
            ct);
    }

    /// <summary>
    /// ارسال به اندپوینت‌های مشترک. <paramref name="eventKey"/> کلید طبیعی رویداد
    /// (بدون زمان) است و <paramref name="occurredAt"/> برای افتراق رویدادهای مجزا
    /// استفاده می‌شود. ترکیب این دو تضمین می‌کند که بازپخشِ همان رویداد همیشه
    /// همان <c>eventId</c> را تولید می‌کند (idempotency سمت گیرنده) ولی رویدادهای
    /// مختلف تحویل مضاعف نمی‌شوند.
    /// </summary>
    private async Task DispatchAsync(
        string eventType, string eventKey, Dictionary<string, object?> payload, DateTime occurredAt, CancellationToken ct)
    {
        try
        {
            // شناسه‌ی یکتای رویداد برای idempotency سمت گیرنده — پایدار و
            // قطعی (تعینی): تابعی از نوع رویداد، کلید طبیعی و زمان وقوع.
            var eventId = $"{eventType}:{eventKey}:{occurredAt:O}";

            payload["eventId"] = eventId;

            await _webhookDispatcher.DispatchAsync(new DispatchWebhookRequest
            {
                EventType = eventType,
                EventId = eventId,
                Payload = payload
            }, ct);
        }
        catch (Exception ex)
        {
            // شکست ارسال وب‌هوک نباید رویداد اصلی را لغو کند.
            DispatchFailed(_logger, eventType, ex);
        }
    }
}
