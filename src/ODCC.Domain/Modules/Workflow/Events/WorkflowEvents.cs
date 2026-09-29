using ODCC.Domain.Common;
using ODCC.Domain.Modules.Workflow.Enums;

namespace ODCC.Domain.Modules.Workflow.Events;

/// <summary>
/// رویدادهای دامنه‌ی ماژول گردش کار.
///
/// <b>طراحی:</b> این رویدادها فقط شناسه‌ها و متادیتای عمومی (کدها، نام‌های
/// نمایشی، وضعیت‌ها) را حمل می‌کنند و هرگز محتوای حساس یا شناسه‌ی پاسخ‌گوی
/// نظرسنجی را ندارند.
///
/// شنونده‌ها: ممیزی (ثبت رخداد) و اعلان‌ها (اطلاع به تأییدکننده).
/// </summary>
public sealed class WorkflowCreatedEvent : DomainEvent
{
    public Guid WorkflowId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public WorkflowEntityType EntityType { get; init; }
    public int Version { get; init; }

    public WorkflowCreatedEvent(Guid workflowId, string name, string code, WorkflowEntityType entityType, int version)
    {
        WorkflowId = workflowId;
        Name = name;
        Code = code;
        EntityType = entityType;
        Version = version;
    }
}

public sealed class WorkflowUpdatedEvent : DomainEvent
{
    public Guid WorkflowId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;

    public WorkflowUpdatedEvent(Guid workflowId, string name, string code)
    {
        WorkflowId = workflowId;
        Name = name;
        Code = code;
    }
}

public sealed class WorkflowActivatedEvent : DomainEvent
{
    public Guid WorkflowId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int Version { get; init; }

    public WorkflowActivatedEvent(Guid workflowId, string name, string code, int version)
    {
        WorkflowId = workflowId;
        Name = name;
        Code = code;
        Version = version;
    }
}

public sealed class WorkflowArchivedEvent : DomainEvent
{
    public Guid WorkflowId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;

    public WorkflowArchivedEvent(Guid workflowId, string name, string code)
    {
        WorkflowId = workflowId;
        Name = name;
        Code = code;
    }
}

/// <summary>
/// یک نمونه‌ی گردش کار شروع شده. این رویداد به سایر ماژول‌ها اجازه می‌دهد
/// (در صورت تمایل) چرخه‌ی عمر خود را با گردش کار هماهنگ کنند — ولی هیچ
/// ماژولی مجبور نیست.
/// </summary>
public sealed class WorkflowInstanceStartedEvent : DomainEvent
{
    public Guid InstanceId { get; init; }
    public Guid WorkflowId { get; init; }
    public string WorkflowCode { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public WorkflowEntityType EntityType { get; init; }
    public string InitialStateCode { get; init; } = string.Empty;
    public Guid? StartedByUserId { get; init; }

    public WorkflowInstanceStartedEvent(
        Guid instanceId, Guid workflowId, string workflowCode,
        Guid entityId, WorkflowEntityType entityType, string initialStateCode, Guid? startedByUserId)
    {
        InstanceId = instanceId;
        WorkflowId = workflowId;
        WorkflowCode = workflowCode;
        EntityId = entityId;
        EntityType = entityType;
        InitialStateCode = initialStateCode;
        StartedByUserId = startedByUserId;
    }
}

/// <summary>
/// نمونه از یک وضعیت به وضعیت دیگر گذار کرده. <c>Approved</c> نشان می‌دهد
/// این گذار از مسیر تأیید گذشته است.
/// </summary>
public sealed class WorkflowInstanceTransitionedEvent : DomainEvent
{
    public Guid InstanceId { get; init; }
    public Guid WorkflowId { get; init; }
    public string WorkflowCode { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public WorkflowEntityType EntityType { get; init; }
    public string FromStateCode { get; init; } = string.Empty;
    public string ToStateCode { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }
    public bool Approved { get; init; }

    public WorkflowInstanceTransitionedEvent(
        Guid instanceId, Guid workflowId, string workflowCode,
        Guid entityId, WorkflowEntityType entityType,
        string fromStateCode, string toStateCode, Guid? actorUserId, bool approved)
    {
        InstanceId = instanceId;
        WorkflowId = workflowId;
        WorkflowCode = workflowCode;
        EntityId = entityId;
        EntityType = entityType;
        FromStateCode = fromStateCode;
        ToStateCode = toStateCode;
        ActorUserId = actorUserId;
        Approved = approved;
    }
}

public sealed class WorkflowInstanceCompletedEvent : DomainEvent
{
    public Guid InstanceId { get; init; }
    public Guid WorkflowId { get; init; }
    public string WorkflowCode { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public WorkflowEntityType EntityType { get; init; }
    public string FinalStateCode { get; init; } = string.Empty;

    public WorkflowInstanceCompletedEvent(
        Guid instanceId, Guid workflowId, string workflowCode,
        Guid entityId, WorkflowEntityType entityType, string finalStateCode)
    {
        InstanceId = instanceId;
        WorkflowId = workflowId;
        WorkflowCode = workflowCode;
        EntityId = entityId;
        EntityType = entityType;
        FinalStateCode = finalStateCode;
    }
}

public sealed class WorkflowInstanceCancelledEvent : DomainEvent
{
    public Guid InstanceId { get; init; }
    public Guid WorkflowId { get; init; }
    public string WorkflowCode { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public WorkflowEntityType EntityType { get; init; }

    public WorkflowInstanceCancelledEvent(
        Guid instanceId, Guid workflowId, string workflowCode,
        Guid entityId, WorkflowEntityType entityType)
    {
        InstanceId = instanceId;
        WorkflowId = workflowId;
        WorkflowCode = workflowCode;
        EntityId = entityId;
        EntityType = entityType;
    }
}

/// <summary>
/// یک گذار نیازمند تأیید، درخواست تأیید شده است. شنونده‌ی اعلان‌ها
/// تأییدکنندگان مجاز را مطلع می‌سازد.
/// </summary>
public sealed class WorkflowApprovalRequestedEvent : DomainEvent
{
    public Guid ApprovalRequestId { get; init; }
    public Guid InstanceId { get; init; }
    public Guid TransitionId { get; init; }
    public string TransitionCode { get; init; } = string.Empty;
    public string FromStateCode { get; init; } = string.Empty;
    public string ToStateCode { get; init; } = string.Empty;
    public string? ApproverPermission { get; init; }
    public Guid? RequestedById { get; init; }
    public DateTime? ExpiresAt { get; init; }

    public WorkflowApprovalRequestedEvent(
        Guid approvalRequestId, Guid instanceId, Guid transitionId, string transitionCode,
        string fromStateCode, string toStateCode, string? approverPermission,
        Guid? requestedById, DateTime? expiresAt)
    {
        ApprovalRequestId = approvalRequestId;
        InstanceId = instanceId;
        TransitionId = transitionId;
        TransitionCode = transitionCode;
        FromStateCode = fromStateCode;
        ToStateCode = toStateCode;
        ApproverPermission = approverPermission;
        RequestedById = requestedById;
        ExpiresAt = expiresAt;
    }
}

/// <summary>
/// یک درخواست تأیید به‌خاطر گذشتن زمان انقضا به‌صورت خودکار منقضی شده است.
/// این رویداد توسط زمان‌بند پس‌زمینه منتشر می‌شود و شنونده‌ی ممیزی آن را
/// ثبت می‌کند تا انقضاها قابل بازبینی باشند.
/// </summary>
public sealed class WorkflowApprovalExpiredEvent : DomainEvent
{
    public Guid ApprovalRequestId { get; init; }
    public Guid InstanceId { get; init; }
    public string TransitionCode { get; init; } = string.Empty;
    public DateTime? ExpiredAt { get; init; }

    public WorkflowApprovalExpiredEvent(Guid approvalRequestId, Guid instanceId, string transitionCode, DateTime? expiredAt)
    {
        ApprovalRequestId = approvalRequestId;
        InstanceId = instanceId;
        TransitionCode = transitionCode;
        ExpiredAt = expiredAt;
    }
}

/// <summary>
/// تصمیم درباره‌ی یک درخواست تأیید گرفته شده (تأیید یا رد).
/// </summary>
public sealed class WorkflowApprovalDecidedEvent : DomainEvent
{
    public Guid ApprovalRequestId { get; init; }
    public Guid InstanceId { get; init; }
    public string TransitionCode { get; init; } = string.Empty;
    public bool Approved { get; init; }
    public Guid? DecidedById { get; init; }
    public string? DecidedByUserName { get; init; }
    public string? DecisionNote { get; init; }

    public WorkflowApprovalDecidedEvent(
        Guid approvalRequestId, Guid instanceId, string transitionCode, bool approved,
        Guid? decidedById, string? decidedByUserName, string? decisionNote)
    {
        ApprovalRequestId = approvalRequestId;
        InstanceId = instanceId;
        TransitionCode = transitionCode;
        Approved = approved;
        DecidedById = decidedById;
        DecidedByUserName = decidedByUserName;
        DecisionNote = decisionNote;
    }
}
