using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Survey.Events;

namespace ODCC.Infrastructure.Modules.Survey.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول نظرسنجی‌ها برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول نظرسنجی رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه نظرسنجی از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class SurveyAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<SurveyAuditEventListener> logger) :
    IDomainEventListener<SurveyCreatedEvent>,
    IDomainEventListener<SurveyUpdatedEvent>,
    IDomainEventListener<SurveyPublishedEvent>,
    IDomainEventListener<SurveyStartedEvent>,
    IDomainEventListener<SurveyClosedEvent>,
    IDomainEventListener<SurveyArchivedEvent>,
    IDomainEventListener<SurveyTemplateCreatedEvent>,
    IDomainEventListener<SurveyTemplateArchivedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<SurveyAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(6, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(SurveyCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.SurveyId, $"ایجاد نظرسنجی «{domainEvent.Code}»", "medium", ct);

    public Task HandleAsync(SurveyUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.SurveyId, $"ویرایش نظرسنجی «{domainEvent.Code}»", "medium", ct);

    public Task HandleAsync(SurveyPublishedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "publish", domainEvent.SurveyId, $"انتشار نظرسنجی «{domainEvent.Code}» (وضعیت: {domainEvent.Status})", "high", ct);

    public Task HandleAsync(SurveyStartedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "start", domainEvent.SurveyId, $"شروع/از سرگیری پاسخ‌گویی نظرسنجی «{domainEvent.Code}»", "high", ct);

    public Task HandleAsync(SurveyClosedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "close", domainEvent.SurveyId, $"بستن نظرسنجی «{domainEvent.Code}»", "high", ct);

    public Task HandleAsync(SurveyArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.SurveyId, $"بایگانی نظرسنجی «{domainEvent.Code}»", "medium", ct);

    public Task HandleAsync(SurveyTemplateCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.TemplateId, $"ایجاد قالب نظرسنجی «{domainEvent.Code}»", "low", ct);

    public Task HandleAsync(SurveyTemplateArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.TemplateId, $"بایگانی قالب نظرسنجی «{domainEvent.Code}»", "low", ct);

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
                EntityType = "survey",
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
