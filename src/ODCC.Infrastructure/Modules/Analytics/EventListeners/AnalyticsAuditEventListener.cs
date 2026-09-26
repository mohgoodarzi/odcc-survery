using System.Globalization;
using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.Analytics.Events;

namespace ODCC.Infrastructure.Modules.Analytics.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول تحلیلات برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول تحلیلات رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه تحلیلات از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
///
/// **حریم خصوصی:** رویدادهای تحلیلات فقط شامل شاخص‌های تجمعی هستند و هیچ
/// شناسه‌ی پاسخ‌گویی همراه آن‌ها نیست؛ ممیزی هم این پیوند را نمی‌سازد.
/// </summary>
public sealed class AnalyticsAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<AnalyticsAuditEventListener> logger) :
    IDomainEventListener<AnalyticsComputedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<AnalyticsAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(11, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(AnalyticsComputedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "compute", domainEvent.MetricId, BuildDescription(domainEvent), "low", ct);

    private static string BuildDescription(AnalyticsComputedEvent domainEvent) =>
        $"محاسبه‌ی تحلیلات نظرسنجی «{domainEvent.SurveyCode}» در بُعد {domainEvent.SegmentType} " +
        $"با {domainEvent.CompletedSessions} پاسخ ارسال‌شده " +
        $"(NPS: {FormatMetric(domainEvent.NpsScore)}، CSAT: {FormatMetric(domainEvent.CsatScore)}، CES: {FormatMetric(domainEvent.CesScore)})";

    private static string FormatMetric(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "—";

    private async Task LogAsync(AnalyticsComputedEvent domainEvent, string action, Guid entityId, string description, string severity, CancellationToken ct)
    {
        try
        {
            var entry = new AuditEntryDto
            {
                OccurredAt = domainEvent.OccurredAt,
                UserId = _currentUserService.UserId,
                UserName = _currentUserService.UserName,
                Action = action,
                EntityType = "survey_metric",
                EntityId = entityId,
                Severity = severity,
                ClientIp = _currentUserService.ClientIpAddress,
                Description = description
            };

            await _auditService.LogAsync(entry, ct);
        }
        catch (Exception ex)
        {
            // شکست ممیزی نباید عملیات اصلی را لغو کند؛ فقط لاگ می‌شود.
            AuditFailed(_logger, domainEvent.GetType().Name, ex);
        }
    }
}
