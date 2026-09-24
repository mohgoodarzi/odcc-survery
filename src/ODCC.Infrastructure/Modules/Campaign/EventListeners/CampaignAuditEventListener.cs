using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Events;

namespace ODCC.Infrastructure.Modules.Campaign.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول کمپین‌ها برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول کمپین رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه کمپین از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class CampaignAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<CampaignAuditEventListener> logger) :
    IDomainEventListener<CampaignCreatedEvent>,
    IDomainEventListener<CampaignUpdatedEvent>,
    IDomainEventListener<CampaignScheduledEvent>,
    IDomainEventListener<CampaignLaunchedEvent>,
    IDomainEventListener<CampaignCompletedEvent>,
    IDomainEventListener<CampaignArchivedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<CampaignAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(7, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(CampaignCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.CampaignId, $"ایجاد کمپین «{domainEvent.Code}»", "medium", ct);

    public Task HandleAsync(CampaignUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.CampaignId, $"ویرایش کمپین «{domainEvent.Code}»", "medium", ct);

    public Task HandleAsync(CampaignScheduledEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "schedule", domainEvent.CampaignId, $"زمان‌بندی کمپین «{domainEvent.Code}» برای {domainEvent.ScheduledAt:u}", "medium", ct);

    public Task HandleAsync(CampaignLaunchedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "launch", domainEvent.CampaignId, $"اجرای کمپین «{domainEvent.Code}» برای {domainEvent.RecipientCount} گیرنده", "high", ct);

    public Task HandleAsync(CampaignCompletedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "complete", domainEvent.CampaignId, $"تکمیل کمپین «{domainEvent.Code}»", "medium", ct);

    public Task HandleAsync(CampaignArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.CampaignId, $"بایگانی کمپین «{domainEvent.Code}»", "medium", ct);

    private async Task LogAsync(IDomainEvent domainEvent, string action, Guid entityId, string description, string severity, CancellationToken ct)
    {
        try
        {
            var entry = new AuditEntryDto
            {
                OccurredAt = domainEvent.OccurredAt,
                UserId = _currentUserService.UserId,
                UserName = _currentUserService.UserName,
                Action = action,
                EntityType = "campaign",
                EntityId = entityId,
                Severity = severity,
                ClientIp = _currentUserService.ClientIpAddress,
                Description = description
            };

            await _auditService.LogAsync(entry, ct);
        }
        catch (Exception ex)
        {
            // شکست ممیزی نباید عملیات اصلی کاربر را لغو کند؛ فقط لاگ می‌شود.
            AuditFailed(_logger, domainEvent.GetType().Name, ex);
        }
    }
}
