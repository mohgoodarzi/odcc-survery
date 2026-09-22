using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Events;

namespace ODCC.Infrastructure.Modules.Audit.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول سازمان برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول سازمان رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه سازمان از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class OrganizationAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<OrganizationAuditEventListener> logger) :
    IDomainEventListener<OrgUnitCreatedEvent>,
    IDomainEventListener<OrgUnitUpdatedEvent>,
    IDomainEventListener<OrgUnitDeletedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<OrganizationAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(3, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(OrgUnitCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.OrgUnitId, $"ایجاد واحد سازمانی «{domainEvent.Code}»", ct);

    public Task HandleAsync(OrgUnitUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.OrgUnitId, $"ویرایش واحد سازمانی «{domainEvent.Code}»", ct);

    public Task HandleAsync(OrgUnitDeletedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "delete", domainEvent.OrgUnitId, $"حذف واحد سازمانی «{domainEvent.Code}»", ct);

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
                EntityType = "org_unit",
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
