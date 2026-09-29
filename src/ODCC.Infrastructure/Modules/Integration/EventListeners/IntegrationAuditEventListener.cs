using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.Integration.Enums;
using ODCC.Domain.Modules.Integration.Events;

namespace ODCC.Infrastructure.Modules.Integration.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول یکپارچه‌سازی برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول یکپارچه‌سازی
/// رویدادی منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد.
/// </summary>
public sealed class IntegrationAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<IntegrationAuditEventListener> logger) :
    IDomainEventListener<IntegrationEndpointCreatedEvent>,
    IDomainEventListener<IntegrationEndpointUpdatedEvent>,
    IDomainEventListener<IntegrationEndpointActivatedEvent>,
    IDomainEventListener<IntegrationEndpointArchivedEvent>,
    IDomainEventListener<WebhookDeliveredEvent>,
    IDomainEventListener<WebhookDeliveryFailedEvent>,
    IDomainEventListener<InboundWebhookReceivedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<IntegrationAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(15, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(IntegrationEndpointCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.EndpointId, "integration_endpoint",
            $"ایجاد اندپوینت یکپارچه‌سازی «{domainEvent.Name}» با کد {domainEvent.Code} از نوع {TypeLabel(domainEvent.Type)}", "medium", ct);

    public Task HandleAsync(IntegrationEndpointUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.EndpointId, "integration_endpoint",
            $"ویرایش اندپوینت یکپارچه‌سازی «{domainEvent.Name}»", "medium", ct);

    public Task HandleAsync(IntegrationEndpointActivatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "activate", domainEvent.EndpointId, "integration_endpoint",
            $"فعال‌سازی اندپوینت یکپارچه‌سازی «{domainEvent.Name}»", "medium", ct);

    public Task HandleAsync(IntegrationEndpointArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.EndpointId, "integration_endpoint",
            $"بایگانی اندپوینت یکپارچه‌سازی «{domainEvent.Name}»", "medium", ct);

    public Task HandleAsync(WebhookDeliveredEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "webhook_delivered", domainEvent.DeliveryId, "webhook_delivery",
            $"تحویل موفق وب‌هوک «{domainEvent.EventType}» به اندپوینت {domainEvent.EndpointCode} (کد {domainEvent.StatusCode})", "low", ct);

    public Task HandleAsync(WebhookDeliveryFailedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "webhook_failed", domainEvent.DeliveryId, "webhook_delivery",
            $"شکست تحویل وب‌هوک «{domainEvent.EventType}» به اندپوینت {domainEvent.EndpointCode}: {domainEvent.Error}", "high", ct);

    public Task HandleAsync(InboundWebhookReceivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "webhook_received", domainEvent.EndpointId, "webhook_inbound",
            $"دریافت وب‌هوک ورودی «{domainEvent.EventType}» روی اندپوینت {domainEvent.EndpointCode} از {domainEvent.SourceIp}", "medium", ct);

    private async Task LogAsync<TEvent>(
        TEvent domainEvent, string action, Guid entityId, string entityType,
        string description, string severity, CancellationToken ct)
        where TEvent : ODCC.Domain.Common.IDomainEvent
    {
        try
        {
            var entry = new AuditEntryDto
            {
                OccurredAt = domainEvent.OccurredAt,
                UserId = _currentUserService.UserId,
                UserName = _currentUserService.UserName,
                Action = action,
                EntityType = entityType,
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

    private static string TypeLabel(IntegrationType type) => type switch
    {
        IntegrationType.OutboundWebhook => "وب‌هوک خروجی",
        IntegrationType.InboundWebhook => "وب‌هوک ورودی",
        IntegrationType.HrSync => "همگام‌سازی منابع انسانی",
        IntegrationType.Sso => "ورود یکپارچه",
        IntegrationType.AiProvider => "ارائه‌دهنده‌ی هوش مصنوعی",
        _ => "نامشخص"
    };
}
