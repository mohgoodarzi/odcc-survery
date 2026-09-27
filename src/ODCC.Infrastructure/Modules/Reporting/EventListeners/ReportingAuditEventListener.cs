using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.Reporting.Events;

namespace ODCC.Infrastructure.Modules.Reporting.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول گزارش‌گیری برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول گزارش‌گیری رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه گزارش‌گیری از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class ReportingAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<ReportingAuditEventListener> logger) :
    IDomainEventListener<ReportCreatedEvent>,
    IDomainEventListener<ReportUpdatedEvent>,
    IDomainEventListener<ReportActivatedEvent>,
    IDomainEventListener<ReportArchivedEvent>,
    IDomainEventListener<ReportExecutionStartedEvent>,
    IDomainEventListener<ReportExecutionSucceededEvent>,
    IDomainEventListener<ReportExecutionFailedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<ReportingAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(12, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(ReportCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.ReportId, "report_definition",
            $"ایجاد گزارش «{domainEvent.Name}» از نوع {domainEvent.Type} با قالب {domainEvent.Format}", "low", ct);

    public Task HandleAsync(ReportUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.ReportId, "report_definition",
            $"ویرایش گزارش «{domainEvent.Name}»", "low", ct);

    public Task HandleAsync(ReportActivatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "activate", domainEvent.ReportId, "report_definition",
            domainEvent.Schedule == ODCC.Domain.Modules.Reporting.Enums.ReportSchedule.OneTime
                ? $"فعال‌سازی گزارش «{domainEvent.Name}»"
                : $"فعال‌سازی گزارش زمان‌بندی‌شده‌ی «{domainEvent.Name}» (هر {domainEvent.Schedule})",
            "medium", ct);

    public Task HandleAsync(ReportArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.ReportId, "report_definition",
            $"بایگانی گزارش «{domainEvent.Name}»", "medium", ct);

    public Task HandleAsync(ReportExecutionStartedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "execute", domainEvent.ExecutionId, "report_execution",
            $"شروع اجرای گزارش «{domainEvent.ReportName}»", "low", ct);

    public Task HandleAsync(ReportExecutionSucceededEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "export", domainEvent.ExecutionId, "report_execution",
            $"تولید موفق خروجی گزارش «{domainEvent.ReportName}»: {domainEvent.FileName} " +
            $"با {domainEvent.RowCount} ردیف داده", "low", ct);

    public Task HandleAsync(ReportExecutionFailedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "execute_failed", domainEvent.ExecutionId, "report_execution",
            $"شکست اجرای گزارش «{domainEvent.ReportName}»: {domainEvent.ErrorMessage}", "high", ct);

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
