using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Events;

namespace ODCC.Infrastructure.Modules.QuestionBank.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول کتابخانه‌ی سؤالات برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول کتابخانه‌ی سؤالات
/// رویدادی منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه کتابخانه‌ی
/// سؤالات از وجود ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class QuestionBankAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<QuestionBankAuditEventListener> logger) :
    IDomainEventListener<QuestionCreatedEvent>,
    IDomainEventListener<QuestionUpdatedEvent>,
    IDomainEventListener<QuestionArchivedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<QuestionBankAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(4, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(QuestionCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.QuestionId, $"ایجاد سؤال «{domainEvent.Code}» در کتابخانه", ct);

    public Task HandleAsync(QuestionUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.QuestionId, $"ویرایش سؤال «{domainEvent.Code}» (نسخه‌ی {domainEvent.VersionNumber})", ct);

    public Task HandleAsync(QuestionArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.QuestionId, $"بایگانی سؤال «{domainEvent.Code}»", ct);

    private async Task LogAsync(IDomainEvent domainEvent, string action, Guid entityId, string description, CancellationToken ct)
    {
        try
        {
            var entry = new AuditEntryDto
            {
                OccurredAt = domainEvent.OccurredAt,
                UserId = _currentUserService.UserId,
                UserName = _currentUserService.UserName,
                Action = action,
                EntityType = "question",
                EntityId = entityId,
                Severity = "medium",
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
