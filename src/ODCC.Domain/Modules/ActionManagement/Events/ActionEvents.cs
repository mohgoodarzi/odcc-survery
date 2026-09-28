using ODCC.Domain.Common;
using ODCC.Domain.Modules.ActionManagement.Enums;

namespace ODCC.Domain.Modules.ActionManagement.Events;

/// <summary>
/// رویدادهای دامنه‌ی ماژول مدیریت اقدامات.
///
/// <b>طراحی:</b> این رویدادها فقط شناسه‌ها و متادیتای عمومی (عناوین،
/// نام‌های نمایشی کاربران سامانه، شاخص‌های تجمعی) را حمل می‌کنند. آنها
/// <b>هرگز</b> شناسه‌ی پاسخ‌گوی یک نظرسنجی یا محتوای پاسخ شخصی را حمل
/// نمی‌کنند — حتی برای نظرسنجی‌های غیرناشناس. ناشناس‌بودن در ماژول پاسخ‌ها
/// اجرا می‌شود و ماژول اقدامات فقط تجمع‌ها را می‌بیند.
///
/// شنونده‌ها: ممیزی (ثبت رخداد) و اعلان‌ها (اطلاع به مسئول/مالک).
/// </summary>
public sealed class ActionPlanCreatedEvent : DomainEvent
{
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public ActionSource Source { get; init; }
    public Guid? SurveyId { get; init; }
    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitPath { get; init; }
    public Guid? OwnerUserId { get; init; }
    public string? OwnerUserName { get; init; }
    public ActionPriority Priority { get; init; }

    public ActionPlanCreatedEvent(
        Guid planId, string title, ActionSource source, Guid? surveyId,
        Guid? orgUnitId, string? orgUnitPath,
        Guid? ownerUserId, string? ownerUserName, ActionPriority priority)
    {
        PlanId = planId;
        Title = title;
        Source = source;
        SurveyId = surveyId;
        OrgUnitId = orgUnitId;
        OrgUnitPath = orgUnitPath;
        OwnerUserId = ownerUserId;
        OwnerUserName = ownerUserName;
        Priority = priority;
    }
}

public sealed class ActionPlanUpdatedEvent : DomainEvent
{
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? OrgUnitId { get; init; }
    public string? OrgUnitPath { get; init; }
    public ActionPriority Priority { get; init; }

    public ActionPlanUpdatedEvent(
        Guid planId, string title, Guid? orgUnitId, string? orgUnitPath, ActionPriority priority)
    {
        PlanId = planId;
        Title = title;
        OrgUnitId = orgUnitId;
        OrgUnitPath = orgUnitPath;
        Priority = priority;
    }
}

public sealed class ActionPlanActivatedEvent : DomainEvent
{
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? OwnerUserId { get; init; }

    public ActionPlanActivatedEvent(Guid planId, string title, Guid? ownerUserId)
    {
        PlanId = planId;
        Title = title;
        OwnerUserId = ownerUserId;
    }
}

public sealed class ActionPlanCompletedEvent : DomainEvent
{
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int CompletedItemCount { get; init; }
    public int TotalItemCount { get; init; }
    public Guid? OwnerUserId { get; init; }

    public ActionPlanCompletedEvent(
        Guid planId, string title, int completedItemCount, int totalItemCount, Guid? ownerUserId)
    {
        PlanId = planId;
        Title = title;
        CompletedItemCount = completedItemCount;
        TotalItemCount = totalItemCount;
        OwnerUserId = ownerUserId;
    }
}

public sealed class ActionPlanCancelledEvent : DomainEvent
{
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? OwnerUserId { get; init; }

    public ActionPlanCancelledEvent(Guid planId, string title, Guid? ownerUserId)
    {
        PlanId = planId;
        Title = title;
        OwnerUserId = ownerUserId;
    }
}

public sealed class ActionPlanArchivedEvent : DomainEvent
{
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;

    public ActionPlanArchivedEvent(Guid planId, string title)
    {
        PlanId = planId;
        Title = title;
    }
}

public sealed class ActionItemCreatedEvent : DomainEvent
{
    public Guid ItemId { get; init; }
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeUserName { get; init; }
    public ActionPriority Priority { get; init; }
    public DateTime? DueDate { get; init; }

    public ActionItemCreatedEvent(
        Guid itemId, Guid planId, string title,
        Guid? assigneeUserId, string? assigneeUserName,
        ActionPriority priority, DateTime? dueDate)
    {
        ItemId = itemId;
        PlanId = planId;
        Title = title;
        AssigneeUserId = assigneeUserId;
        AssigneeUserName = assigneeUserName;
        Priority = priority;
        DueDate = dueDate;
    }
}

public sealed class ActionItemUpdatedEvent : DomainEvent
{
    public Guid ItemId { get; init; }
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AssigneeUserId { get; init; }
    public ActionPriority Priority { get; init; }
    public DateTime? DueDate { get; init; }

    public ActionItemUpdatedEvent(
        Guid itemId, Guid planId, string title,
        Guid? assigneeUserId, ActionPriority priority, DateTime? dueDate)
    {
        ItemId = itemId;
        PlanId = planId;
        Title = title;
        AssigneeUserId = assigneeUserId;
        Priority = priority;
        DueDate = dueDate;
    }
}

/// <summary>
/// تغییر وضعیت یک آیتم. نام مسئول از این رویداد خوانده می‌شود تا اعلان‌ها
/// نیازی به پرس‌وجوی مجدد نداشته باشند.
/// </summary>
public sealed class ActionItemStatusChangedEvent : DomainEvent
{
    public Guid ItemId { get; init; }
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AssigneeUserId { get; init; }
    public ActionItemStatus OldStatus { get; init; }
    public ActionItemStatus NewStatus { get; init; }

    public ActionItemStatusChangedEvent(
        Guid itemId, Guid planId, string title, Guid? assigneeUserId,
        ActionItemStatus oldStatus, ActionItemStatus newStatus)
    {
        ItemId = itemId;
        PlanId = planId;
        Title = title;
        AssigneeUserId = assigneeUserId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}

public sealed class ActionItemCompletedEvent : DomainEvent
{
    public Guid ItemId { get; init; }
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeUserName { get; init; }

    public ActionItemCompletedEvent(Guid itemId, Guid planId, string title, Guid? assigneeUserId, string? assigneeUserName = null)
    {
        ItemId = itemId;
        PlanId = planId;
        Title = title;
        AssigneeUserId = assigneeUserId;
        AssigneeUserName = assigneeUserName;
    }
}

/// <summary>
/// زمان یادآوری یک آیتم رسیده است. زمان‌بند پیگیری این رویداد را تولید
/// می‌کند و شنونده‌ی اعلان‌ها مسئول را مطلع می‌سازد.
/// </summary>
public sealed class ActionItemReminderDueEvent : DomainEvent
{
    public Guid ItemId { get; init; }
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeUserName { get; init; }
    public DateTime? DueDate { get; init; }

    public ActionItemReminderDueEvent(
        Guid itemId, Guid planId, string title, Guid? assigneeUserId, DateTime? dueDate, string? assigneeUserName = null)
    {
        ItemId = itemId;
        PlanId = planId;
        Title = title;
        AssigneeUserId = assigneeUserId;
        DueDate = dueDate;
        AssigneeUserName = assigneeUserName;
    }
}

/// <summary>
/// یک آیتم سررسیده‌شده یک درجه تشدید شد. شنونده‌ی اعلان‌ها مسئول و (در
/// درجات بالاتر) مالک برنامه یا مدیران را مطلع می‌سازد.
/// </summary>
public sealed class ActionItemEscalatedEvent : DomainEvent
{
    public Guid ItemId { get; init; }
    public Guid PlanId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeUserName { get; init; }
    public EscalationLevel EscalationLevel { get; init; }
    public DateTime? DueDate { get; init; }

    public ActionItemEscalatedEvent(
        Guid itemId, Guid planId, string title, Guid? assigneeUserId,
        EscalationLevel escalationLevel, DateTime? dueDate, string? assigneeUserName = null)
    {
        ItemId = itemId;
        PlanId = planId;
        Title = title;
        AssigneeUserId = assigneeUserId;
        EscalationLevel = escalationLevel;
        DueDate = dueDate;
        AssigneeUserName = assigneeUserName;
    }
}

public sealed class ActionCommentAddedEvent : DomainEvent
{
    public Guid ItemId { get; init; }
    public Guid PlanId { get; init; }
    public string ItemTitle { get; init; } = string.Empty;
    public Guid CommentId { get; init; }
    public Guid? AuthorUserId { get; init; }
    public string AuthorUserName { get; init; } = string.Empty;

    public ActionCommentAddedEvent(
        Guid itemId, Guid planId, string itemTitle,
        Guid commentId, Guid? authorUserId, string authorUserName)
    {
        ItemId = itemId;
        PlanId = planId;
        ItemTitle = itemTitle;
        CommentId = commentId;
        AuthorUserId = authorUserId;
        AuthorUserName = authorUserName;
    }
}

public sealed class ActionEvidenceUploadedEvent : DomainEvent
{
    public Guid ItemId { get; init; }
    public Guid PlanId { get; init; }
    public string ItemTitle { get; init; } = string.Empty;
    public Guid EvidenceId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public Guid? UploadedByUserId { get; init; }

    public ActionEvidenceUploadedEvent(
        Guid itemId, Guid planId, string itemTitle,
        Guid evidenceId, string fileName, long sizeBytes, Guid? uploadedByUserId)
    {
        ItemId = itemId;
        PlanId = planId;
        ItemTitle = itemTitle;
        EvidenceId = evidenceId;
        FileName = fileName;
        FileSizeBytes = sizeBytes;
        UploadedByUserId = uploadedByUserId;
    }
}
