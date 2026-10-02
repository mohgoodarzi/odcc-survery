using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.ActionManagement.Enums;
using ODCC.Domain.Modules.ActionManagement.Events;

namespace ODCC.Infrastructure.Modules.ActionManagement.EventListeners;

/// <summary>
/// شنونده‌ی رویدادهای دامنه‌ی ماژول مدیریت اقدامات برای ثبت ممیزی.
///
/// این کلاس نمونه‌ی الگوی مجاز ارتباط بین ماژول‌هاست: ماژول اقدامات رویدادی
/// منتشر می‌کند و ماژول ممیزی به آن گوش می‌دهد، بدون اینکه اقدامات از وجود
/// ممیزی بداند یا به جداول آن دسترسی مستقیم داشته باشد.
/// </summary>
public sealed class ActionManagementAuditEventListener(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<ActionManagementAuditEventListener> logger) :
    IDomainEventListener<ActionPlanCreatedEvent>,
    IDomainEventListener<ActionPlanUpdatedEvent>,
    IDomainEventListener<ActionPlanActivatedEvent>,
    IDomainEventListener<ActionPlanCompletedEvent>,
    IDomainEventListener<ActionPlanCancelledEvent>,
    IDomainEventListener<ActionPlanArchivedEvent>,
    IDomainEventListener<ActionItemCreatedEvent>,
    IDomainEventListener<ActionItemUpdatedEvent>,
    IDomainEventListener<ActionItemStatusChangedEvent>,
    IDomainEventListener<ActionItemEscalatedEvent>,
    IDomainEventListener<ActionCommentAddedEvent>,
    IDomainEventListener<ActionEvidenceUploadedEvent>
{
    private readonly IAuditService _auditService = auditService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<ActionManagementAuditEventListener> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> AuditFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(13, "AuditLogFailed"),
        "ثبت رخداد ممیزی برای {EventType} ناموفق بود.");

    public Task HandleAsync(ActionPlanCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.PlanId, "action_plan",
            $"ایجاد برنامه‌ی اقدام «{domainEvent.Title}» از منشأ {SourceLabel(domainEvent.Source)}", "low", ct);

    public Task HandleAsync(ActionPlanUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.PlanId, "action_plan",
            $"ویرایش برنامه‌ی اقدام «{domainEvent.Title}»", "low", ct);

    public Task HandleAsync(ActionPlanActivatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "activate", domainEvent.PlanId, "action_plan",
            $"فعال‌سازی برنامه‌ی اقدام «{domainEvent.Title}»", "medium", ct);

    public Task HandleAsync(ActionPlanCompletedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "complete", domainEvent.PlanId, "action_plan",
            $"تکمیل برنامه‌ی اقدام «{domainEvent.Title}» ({domainEvent.CompletedItemCount} از {domainEvent.TotalItemCount} آیتم)", "low", ct);

    public Task HandleAsync(ActionPlanCancelledEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "cancel", domainEvent.PlanId, "action_plan",
            $"لغو برنامه‌ی اقدام «{domainEvent.Title}»", "medium", ct);

    public Task HandleAsync(ActionPlanArchivedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "archive", domainEvent.PlanId, "action_plan",
            $"بایگانی برنامه‌ی اقدام «{domainEvent.Title}»", "medium", ct);

    public Task HandleAsync(ActionItemCreatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "create", domainEvent.ItemId, "action_item",
            $"ایجاد آیتم اقدام «{domainEvent.Title}»" +
            (domainEvent.AssigneeUserId is null ? string.Empty : $" با مسئول {domainEvent.AssigneeUserName}"), "low", ct);

    public Task HandleAsync(ActionItemUpdatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "update", domainEvent.ItemId, "action_item",
            $"ویرایش آیتم اقدام «{domainEvent.Title}»", "low", ct);

    public Task HandleAsync(ActionItemStatusChangedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "transition", domainEvent.ItemId, "action_item",
            $"تغییر وضعیت آیتم اقدام «{domainEvent.Title}» از {StatusLabel(domainEvent.OldStatus)} به {StatusLabel(domainEvent.NewStatus)}",
            domainEvent.NewStatus == ActionItemStatus.Done ? "low" : "medium", ct);

    public Task HandleAsync(ActionItemEscalatedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "escalate", domainEvent.ItemId, "action_item",
            $"تشدید آیتم اقدام «{domainEvent.Title}» به درجه‌ی {EscalationLabel(domainEvent.EscalationLevel)}", "high", ct);

    public Task HandleAsync(ActionCommentAddedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "comment", domainEvent.CommentId, "action_comment",
            $"افزودن دیدگاه به آیتم اقدام «{domainEvent.ItemTitle}» توسط {domainEvent.AuthorUserName}", "low", ct);

    public Task HandleAsync(ActionEvidenceUploadedEvent domainEvent, CancellationToken ct = default) =>
        LogAsync(domainEvent, "upload", domainEvent.EvidenceId, "action_evidence",
            $"آپلود پیوست «{domainEvent.FileName}» برای آیتم اقدام «{domainEvent.ItemTitle}»", "low", ct);

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

    private static string SourceLabel(ActionSource source) => source switch
    {
        ActionSource.AnalyticsAlert => "هشدار تحلیل ها",
        ActionSource.SurveyFinding => "یافته‌ی نظرسنجی",
        _ => "دستی"
    };

    private static string StatusLabel(ActionItemStatus status) => status switch
    {
        ActionItemStatus.Open => "باز",
        ActionItemStatus.InProgress => "در حال انجام",
        ActionItemStatus.Done => "انجام‌شده",
        _ => "لغوشده"
    };

    private static string EscalationLabel(EscalationLevel level) => level switch
    {
        EscalationLevel.Reminder => "یادآوری",
        EscalationLevel.EscalatedToOwner => "تشدید به مالک",
        EscalationLevel.EscalatedToManagement => "تشدید به مدیریت",
        _ => "نامشخص"
    };
}
