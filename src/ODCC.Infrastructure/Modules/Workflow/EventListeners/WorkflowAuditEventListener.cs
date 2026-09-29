using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.Workflow.Events;

namespace ODCC.Infrastructure.Modules.Workflow.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول گردش کار برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول گردش کار رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه گردش کار از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class WorkflowAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<WorkflowAuditEventListener> logger) :
    IDomainEventListener<WorkflowCreatedEvent>,
    IDomainEventListener<WorkflowUpdatedEvent>,
    IDomainEventListener<WorkflowActivatedEvent>,
    IDomainEventListener<WorkflowArchivedEvent>,
    IDomainEventListener<WorkflowInstanceStartedEvent>,
    IDomainEventListener<WorkflowInstanceTransitionedEvent>,
    IDomainEventListener<WorkflowInstanceCompletedEvent>,
    IDomainEventListener<WorkflowInstanceCancelledEvent>,
    IDomainEventListener<WorkflowApprovalRequestedEvent>,
    IDomainEventListener<WorkflowApprovalDecidedEvent>,
    IDomainEventListener<WorkflowApprovalExpiredEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<WorkflowAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(14, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(WorkflowCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.WorkflowId, "workflow",
            $"ایجاد گردش کار «{domainEvent.Name}» با کد {domainEvent.Code} برای {EntityLabel(domainEvent.EntityType)}", "low", ct);

    public Task HandleAsync(WorkflowUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.WorkflowId, "workflow",
            $"ویرایش گردش کار «{domainEvent.Name}»", "low", ct);

    public Task HandleAsync(WorkflowActivatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "activate", domainEvent.WorkflowId, "workflow",
            $"فعال‌سازی گردش کار «{domainEvent.Name}» (نسخه‌ی {domainEvent.Version})", "medium", ct);

    public Task HandleAsync(WorkflowArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.WorkflowId, "workflow",
            $"بایگانی گردش کار «{domainEvent.Name}»", "medium", ct);

    public Task HandleAsync(WorkflowInstanceStartedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "start", domainEvent.InstanceId, "workflow_instance",
            $"شروع نمونه‌ی گردش کار {domainEvent.WorkflowCode} برای {EntityLabel(domainEvent.EntityType)} در وضعیت «{domainEvent.InitialStateCode}»", "medium", ct);

    public Task HandleAsync(WorkflowInstanceTransitionedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "transition", domainEvent.InstanceId, "workflow_instance",
            $"گذار نمونه‌ی {domainEvent.WorkflowCode} از «{domainEvent.FromStateCode}» به «{domainEvent.ToStateCode}»"
            + (domainEvent.Approved ? " (پس از تأیید)" : string.Empty), "medium", ct);

    public Task HandleAsync(WorkflowInstanceCompletedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "complete", domainEvent.InstanceId, "workflow_instance",
            $"تکمیل نمونه‌ی گردش کار {domainEvent.WorkflowCode} در وضعیت نهایی «{domainEvent.FinalStateCode}»", "low", ct);

    public Task HandleAsync(WorkflowInstanceCancelledEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "cancel", domainEvent.InstanceId, "workflow_instance",
            $"لغو نمونه‌ی گردش کار {domainEvent.WorkflowCode}", "medium", ct);

    public Task HandleAsync(WorkflowApprovalRequestedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "approval_request", domainEvent.ApprovalRequestId, "workflow_approval",
            $"درخواست تأیید گذار «{domainEvent.TransitionCode}» از «{domainEvent.FromStateCode}» به «{domainEvent.ToStateCode}»", "high", ct);

    public Task HandleAsync(WorkflowApprovalDecidedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, domainEvent.Approved ? "approve" : "reject", domainEvent.ApprovalRequestId, "workflow_approval",
            $"{(domainEvent.Approved ? "تأیید" : "رد")} گذار «{domainEvent.TransitionCode}» توسط {domainEvent.DecidedByUserName}", "high", ct);

    public Task HandleAsync(WorkflowApprovalExpiredEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "expire", domainEvent.ApprovalRequestId, "workflow_approval",
            $"انقضای خودکار درخواست تأیید گذار «{domainEvent.TransitionCode}»", "medium", ct);

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

    private static string EntityLabel(ODCC.Domain.Modules.Workflow.Enums.WorkflowEntityType entityType) => entityType switch
    {
        ODCC.Domain.Modules.Workflow.Enums.WorkflowEntityType.Survey => "نظرسنجی",
        ODCC.Domain.Modules.Workflow.Enums.WorkflowEntityType.Campaign => "کمپین",
        ODCC.Domain.Modules.Workflow.Enums.WorkflowEntityType.ActionPlan => "برنامه‌ی اقدام",
        ODCC.Domain.Modules.Workflow.Enums.WorkflowEntityType.ReportDefinition => "تعریف گزارش",
        _ => "موجودیت سفارشی"
    };
}
