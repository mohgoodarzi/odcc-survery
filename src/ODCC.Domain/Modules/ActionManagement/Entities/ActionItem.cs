using ODCC.Domain.Common;
using ODCC.Domain.Modules.ActionManagement.Enums;
using ODCC.Domain.Modules.ActionManagement.Events;

namespace ODCC.Domain.Modules.ActionManagement.Entities;

/// <summary>
/// یک گام اجرایی داخل یک <see cref="ActionPlan"/>. این موجودیت واحد کار است که
/// پیگیری می‌شود: دارای مسئول، اولویت، مهلت، وضعیت چرخه‌ی عمر، یادآور،
/// شدیدسازی، دیدگاه‌ها (کامنت)، مستندات (پیوست) و سنجش اثربخشی است.
/// </summary>
public sealed class ActionItem : BaseEntity
{
    /// <summary>برنامه‌ی مالک این آیتم.</summary>
    public Guid ActionPlanId { get; set; }

    public ActionPlan? ActionPlan { get; set; }

    /// <summary>عنوان کوتاه کار.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>شرح کامل کار (در صورت نیاز).</summary>
    public string? Description { get; set; }

    /// <summary>مسئول اجرای این آیتم (کاربر سامانه).</summary>
    public Guid? AssigneeUserId { get; set; }

    /// <summary>نام نمایشی مسئول در زمان انتصاب (snapshot).</summary>
    public string? AssigneeUserName { get; set; }

    /// <summary>اولویت.</summary>
    public ActionPriority Priority { get; set; } = ActionPriority.Medium;

    /// <summary>وضعیت چرخه‌ی عمر.</summary>
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;

    /// <summary>ترتیب نمایش داخل برنامه.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>مهلت نهایی (UTC).</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>زمان یادآوری بعدی (UTC) که زمان‌بند پیگیری آن را فعال می‌کند.</summary>
    public DateTime? RemindAt { get; set; }

    /// <summary>درجه‌ی شدیدسازی فعلی.</summary>
    public EscalationLevel EscalationLevel { get; set; } = EscalationLevel.None;

    /// <summary>زمان آخرین شدیدسازی (UTC).</summary>
    public DateTime? EscalatedAt { get; set; }

    /// <summary>زمان شروع (گذار به InProgress).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>زمان تکمیل (UTC).</summary>
    public DateTime? CompletedAt { get; set; }

    // --- سنجش اثربخشی -----------------------------------------------------------

    /// <summary>ارزیابی اثربخشی پس از اجرا.</summary>
    public EffectivenessRating Effectiveness { get; set; } = EffectivenessRating.NotAssessed;

    /// <summary>یادداشت توضیحی درباره‌ی اثربخشی.</summary>
    public string? EffectivenessNote { get; set; }

    /// <summary>زمان ثبت ارزیابی اثربخشی (UTC).</summary>
    public DateTime? EffectivenessAssessedAt { get; set; }

    /// <summary>کاربری که اثربخشی را ارزیابی کرده.</summary>
    public Guid? EffectivenessAssessedByUserId { get; set; }

    // --- مجموعه‌های فرزند ---------------------------------------------------------

    public List<ActionComment> Comments { get; set; } = [];

    public List<ActionEvidence> Evidence { get; set; } = [];

    /// <summary>آیا این آیتم هنوز باز است (انجام‌نشده و لغو‌نشده)؟</summary>
    public bool IsOpen => Status is ActionItemStatus.Open or ActionItemStatus.InProgress;

    /// <summary>آیا مهلت این آیتم گذشته ولی هنوز باز است؟</summary>
    public bool IsOverdue =>
        IsOpen
        && DueDate is { } due
        && due < DateTime.UtcNow;

    // --- چرخه‌ی عمر --------------------------------------------------------------

    /// <summary>شروع کار روی آیتم.</summary>
    public void Start()
    {
        Status = ActionItemStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>تکمیل آیتم.</summary>
    public void Complete()
    {
        Status = ActionItemStatus.Done;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>باز کردن دوباره‌ی آیتمِ تکمیل‌شده (مثلاً برای اصلاح).</summary>
    public void Reopen()
    {
        Status = ActionItemStatus.InProgress;
        CompletedAt = null;
    }

    /// <summary>لغو آیتم.</summary>
    public void Cancel()
    {
        Status = ActionItemStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>تنظیم/تثبیت مسئول آیتم.</summary>
    public void Assign(Guid? assigneeUserId, string? assigneeUserName)
    {
        AssigneeUserId = assigneeUserId;
        AssigneeUserName = assigneeUserName;
    }

    /// <summary>زمان یادآوری را پس از ارسال، پاک می‌کند تا دوباره فعال نشود.</summary>
    public void ClearReminder()
    {
        RemindAt = null;
    }

    /// <summary>
    /// شدیدسازی یک درجه (در صورت امکان). زمان‌بند پیگیری این متد را برای
    /// آیتم‌های سررسیده‌شده‌ی باز صدا می‌زند.
    /// </summary>
    /// <returns>آیا شدیدسازی انجام شد؟</returns>
    public bool Escalate()
    {
        if (EscalationLevel >= EscalationLevel.EscalatedToManagement)
        {
            return false;
        }

        EscalationLevel = (EscalationLevel)((int)EscalationLevel + 1);
        EscalatedAt = DateTime.UtcNow;

        return true;
    }

    /// <summary>ثبت ارزیابی اثربخشی (فقط پس از تکمیل یا لغو).</summary>
    public void AssessEffectiveness(
        EffectivenessRating rating,
        string? note,
        Guid? assessedByUserId)
    {
        Effectiveness = rating;
        EffectivenessNote = note;
        EffectivenessAssessedAt = DateTime.UtcNow;
        EffectivenessAssessedByUserId = assessedByUserId;
    }

    // --- رویدادهای دامنه --------------------------------------------------------

    public void RaiseCreatedEvent()
    {
        RaiseDomainEvent(new ActionItemCreatedEvent(
            Id, ActionPlanId, Title, AssigneeUserId, AssigneeUserName, Priority, DueDate));
    }

    public void RaiseUpdatedEvent()
    {
        RaiseDomainEvent(new ActionItemUpdatedEvent(Id, ActionPlanId, Title, AssigneeUserId, Priority, DueDate));
    }

    /// <summary>انتشار رویداد تغییر وضعیت (شنونده‌ی اعلان‌ها مسئول را مطلع می‌کند).</summary>
    public void RaiseStatusChangedEvent(ActionItemStatus oldStatus, ActionItemStatus newStatus)
    {
        RaiseDomainEvent(new ActionItemStatusChangedEvent(
            Id, ActionPlanId, Title, AssigneeUserId, oldStatus, newStatus));
    }

    public void RaiseCompletedEvent()
    {
        RaiseDomainEvent(new ActionItemCompletedEvent(Id, ActionPlanId, Title, AssigneeUserId, AssigneeUserName));
    }

    /// <summary>انتشار رویداد یادآوری سررسیده (زمان‌بند تولید می‌کند).</summary>
    public void RaiseReminderDueEvent()
    {
        RaiseDomainEvent(new ActionItemReminderDueEvent(Id, ActionPlanId, Title, AssigneeUserId, DueDate, AssigneeUserName));
    }

    /// <summary>انتشار رویداد شدیدسازی (زمان‌بند تولید می‌کند).</summary>
    public void RaiseEscalatedEvent()
    {
        RaiseDomainEvent(new ActionItemEscalatedEvent(
            Id, ActionPlanId, Title, AssigneeUserId, EscalationLevel, DueDate, AssigneeUserName));
    }

    public void RaiseCommentAddedEvent(Guid commentId, Guid authorUserId, string authorName)
    {
        RaiseDomainEvent(new ActionCommentAddedEvent(Id, ActionPlanId, Title, commentId, authorUserId, authorName));
    }

    public void RaiseEvidenceUploadedEvent(Guid evidenceId, string fileName, long sizeBytes, Guid? uploadedByUserId)
    {
        RaiseDomainEvent(new ActionEvidenceUploadedEvent(Id, ActionPlanId, Title, evidenceId, fileName, sizeBytes, uploadedByUserId));
    }
}
