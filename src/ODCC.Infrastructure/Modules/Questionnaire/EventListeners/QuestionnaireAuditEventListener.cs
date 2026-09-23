using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Questionnaire.Events;

namespace ODCC.Infrastructure.Modules.Questionnaire.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول پرسشنامه‌ها برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول پرسشنامه رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه پرسشنامه از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class QuestionnaireAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<QuestionnaireAuditEventListener> logger) :
    IDomainEventListener<QuestionnaireCreatedEvent>,
    IDomainEventListener<QuestionnaireUpdatedEvent>,
    IDomainEventListener<QuestionnairePublishedEvent>,
    IDomainEventListener<QuestionnaireArchivedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<QuestionnaireAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(5, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(QuestionnaireCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.QuestionnaireId, $"ایجاد پرسشنامه «{domainEvent.Code}»", "medium", ct);

    public Task HandleAsync(QuestionnaireUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.QuestionnaireId, $"ویرایش پرسشنامه «{domainEvent.Code}»", "medium", ct);

    public Task HandleAsync(QuestionnairePublishedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "publish", domainEvent.QuestionnaireId, $"انتشار پرسشنامه «{domainEvent.Code}» (نسخه‌ی {domainEvent.Version})", "high", ct);

    public Task HandleAsync(QuestionnaireArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.QuestionnaireId, $"بایگانی پرسشنامه «{domainEvent.Code}»", "medium", ct);

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
                EntityType = "questionnaire",
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
