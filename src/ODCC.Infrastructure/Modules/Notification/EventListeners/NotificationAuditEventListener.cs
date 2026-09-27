using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.Notification.Events;

namespace ODCC.Infrastructure.Modules.Notification.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول اعلان‌ها برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول اعلان‌ها رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه اعلان‌ها از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class NotificationAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<NotificationAuditEventListener> logger) :
    IDomainEventListener<NotificationCreatedEvent>,
    IDomainEventListener<NotificationDeliveredEvent>,
    IDomainEventListener<NotificationFailedEvent>,
    IDomainEventListener<NotificationReadEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<NotificationAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(13, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(NotificationCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "send", domainEvent.NotificationId, "notification",
            $"ارسال اعلان «{domainEvent.Subject}» از نوع {domainEvent.Channel} در دسته‌ی {domainEvent.Category} " +
            $"با قالب {domainEvent.TemplateCode}", "low", ct);

    public Task HandleAsync(NotificationDeliveredEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "deliver", domainEvent.NotificationId, "notification",
            $"تحویل موفق اعلان در کانال {domainEvent.Channel}", "low", ct);

    public Task HandleAsync(NotificationFailedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "send_failed", domainEvent.NotificationId, "notification",
            $"شکست تحویل اعلان در کانال {domainEvent.Channel}: {domainEvent.LastError}", "high", ct);

    public Task HandleAsync(NotificationReadEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "read", domainEvent.NotificationId, "notification",
            "خوانده‌شدن اعلان درون‌برنامه‌ای توسط گیرنده", "low", ct);

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
}
