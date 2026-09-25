using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Response.Events;

namespace ODCC.Infrastructure.Modules.Response.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول پاسخ‌ها برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول پاسخ‌ها رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه پاسخ‌ها از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
///
/// **ناشناس بودن:** در رویدادهای پاسخ ناشناس، شناسه‌ی کاربر پاسخ‌گو منتقل
/// نمی‌شود تا ممیزی هم این پیوند را نشکند. توضیح رخداد به جای آن به کد
/// نظرسنجی اشاره می‌کند.
/// </summary>
public sealed class ResponseAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<ResponseAuditEventListener> logger) :
    IDomainEventListener<ResponseStartedEvent>,
    IDomainEventListener<ResponseSubmittedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<ResponseAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(8, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(ResponseStartedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "start", domainEvent.SessionId, BuildStartedDescription(domainEvent), "low", domainEvent.IsAnonymous, ct);

    public Task HandleAsync(ResponseSubmittedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "submit", domainEvent.SessionId, BuildSubmittedDescription(domainEvent), "medium", domainEvent.IsAnonymous, ct);

    private static string BuildStartedDescription(ResponseStartedEvent domainEvent) =>
        domainEvent.IsAnonymous
            ? $"شروع پاسخ‌گویی ناشناس به نظرسنجی «{domainEvent.SurveyCode}»"
            : $"شروع پاسخ‌گویی به نظرسنجی «{domainEvent.SurveyCode}»";

    private static string BuildSubmittedDescription(ResponseSubmittedEvent domainEvent) =>
        domainEvent.IsAnonymous
            ? $"ارسال پاسخ ناشناس به نظرسنجی «{domainEvent.SurveyCode}» با {domainEvent.AnswerCount} پاسخ"
            : $"ارسال پاسخ به نظرسنجی «{domainEvent.SurveyCode}» با {domainEvent.AnswerCount} پاسخ";

    private async Task LogAsync(IDomainEvent domainEvent, string action, Guid entityId, string description, string severity, bool isAnonymous, CancellationToken ct)
    {
        try
        {
            var entry = new AuditEntryDto
            {
                OccurredAt = domainEvent.OccurredAt,
                // در رویدادهای ناشناس، شناسه‌ی کاربر پاسخ‌گو منتقل نمی‌شود تا
                // ممیزی هم این پیوند را نشکند. توضیح رخداد به جای آن به کد
                // نظرسنجی اشاره می‌کند.
                UserId = isAnonymous ? null : _currentUserService.UserId,
                UserName = isAnonymous ? null : _currentUserService.UserName,
                Action = action,
                EntityType = "response_session",
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
