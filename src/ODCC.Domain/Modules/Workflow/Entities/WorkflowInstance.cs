using ODCC.Domain.Common;
using ODCC.Domain.Modules.Workflow.Enums;
using ODCC.Domain.Modules.Workflow.Events;

namespace ODCC.Domain.Modules.Workflow.Entities;

/// <summary>
/// یک نمونه‌ی در حال اجرا از یک <see cref="WorkflowDefinition"/> که به یک موجودیت
/// مشخص (مثلاً یک نظرسنجی) متصل است.
///
/// <b>طراحی:</b> نمونه فقط کد وضعیت جاری را نگه می‌دارد و هرگز به موجودیت
/// دامنه‌ی ماژول دیگر ارجاع مستقیم نمی‌دهد — فقط <see cref="EntityId"/> را
/// به‌عنوان مرجع ذخیره می‌کند (قرارداد ساده). کارگذاری واقعی گذارها بر عهده
/// سرویس کاربردی است که با مجوزها و قوانین کسب‌وکار هماهنگ می‌شود.
/// </summary>
public sealed class WorkflowInstance : BaseEntity
{
    /// <summary>شناسه‌ی تعریف گردش کار.</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>کد تعریف (snapshot برای نمایش).</summary>
    public string WorkflowCode { get; set; } = string.Empty;

    /// <summary>نسخه‌ی تعریف در زمان ساخت نمونه (snapshot).</summary>
    public int WorkflowVersion { get; set; }

    /// <summary>نوع موجودیتی که نمونه به آن متصل است.</summary>
    public WorkflowEntityType EntityType { get; set; }

    /// <summary>شناسه‌ی موجودیت هدف (مثلاً شناسه‌ی نظرسنجی).</summary>
    public Guid EntityId { get; set; }

    /// <summary>کد وضعیت جاری.</summary>
    public string CurrentStateCode { get; set; } = string.Empty;

    /// <summary>وضعیت نمونه.</summary>
    public WorkflowInstanceState Status { get; set; } = WorkflowInstanceState.Running;

    /// <summary>زمان شروع (UTC).</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>زمان تکمیل (UTC).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>زمان لغو (UTC).</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>کاربری که نمونه را شروع کرده.</summary>
    public Guid? StartedByUserId { get; set; }

    /// <summary>نام نمایشی شروع‌کننده (snapshot).</summary>
    public string? StartedByUserName { get; set; }

    /// <summary>
    /// واحد سازمانیِ کاربری که نمونه را شروع کرده (snapshot). لنگر دامنه‌ی
    /// دسترسی برای فیلتر کردن نمونه‌ها بر اساس دامنه‌ی سازمانی کاربر نگاه
    ///‌کنند‌. <c>null</c> اگر کاربر لنگر سازمانی معتبری نداشته باشد.
    /// </summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>مسیر مادی واحد سازمانی شروع‌کننده (snapshot، مثلاً «/hq/fin»).</summary>
    public string? OrgUnitPath { get; set; }

    /// <summary>
    /// زمینه‌ی اختیاری (JSON). فقط متادیتای عمومی (عناوین، کدها) را می‌تواند
    /// نگه دارد — هرگز داده‌ی حساس یا شناسه‌ی پاسخ‌گو.
    /// </summary>
    public string? ContextJson { get; set; }

    /// <summary>تعداد گذارهای انجام‌شده (برای آمار و تشخیص حلقه).</summary>
    public int TransitionCount { get; set; }

    // --- محاسبات ---------------------------------------------------------------

    public bool IsRunning => Status == WorkflowInstanceState.Running;
    public bool IsFinished => Status is WorkflowInstanceState.Completed or WorkflowInstanceState.Cancelled or WorkflowInstanceState.Failed;

    // --- چرخه‌ی عمر -------------------------------------------------------------

    /// <summary>انتقال نمونه به وضعیت جدید. این متد فقط ماشین وضعیت را
    /// به‌روزرسانی می‌کند؛ اعتبارسنجی مجازبودن گذار باید پیش از آن انجام شود.</summary>
    public void TransitionTo(string toStateCode, bool toFinalState)
    {
        CurrentStateCode = toStateCode;
        TransitionCount++;

        if (toFinalState)
        {
            Status = WorkflowInstanceState.Completed;
            CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>لغو نمونه.</summary>
    public void Cancel()
    {
        Status = WorkflowInstanceState.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }

    /// <summary>علامت‌گذاری نمونه به‌عنوان ناموفق.</summary>
    public void Fail()
    {
        Status = WorkflowInstanceState.Failed;
        CompletedAt = DateTime.UtcNow;
    }

    // --- رویدادهای دامنه -------------------------------------------------------

    public void RaiseStartedEvent()
    {
        RaiseDomainEvent(new WorkflowInstanceStartedEvent(Id, WorkflowId, WorkflowCode, EntityId, EntityType, CurrentStateCode, StartedByUserId));
    }

    public void RaiseTransitionedEvent(string fromStateCode, string toStateCode, Guid? actorUserId, bool approved)
    {
        RaiseDomainEvent(new WorkflowInstanceTransitionedEvent(
            Id, WorkflowId, WorkflowCode, EntityId, EntityType, fromStateCode, toStateCode, actorUserId, approved));
    }

    public void RaiseCompletedEvent()
    {
        RaiseDomainEvent(new WorkflowInstanceCompletedEvent(Id, WorkflowId, WorkflowCode, EntityId, EntityType, CurrentStateCode));
    }

    public void RaiseCancelledEvent()
    {
        RaiseDomainEvent(new WorkflowInstanceCancelledEvent(Id, WorkflowId, WorkflowCode, EntityId, EntityType));
    }
}

/// <summary>
/// درخواست تأیید یک گذارِ نیازمند تأیید. تا تصمیم‌گیری، نمونه در وضعیت
/// قبلی خود می‌ماند.
///
/// <b>مجوزها:</b> تأییدکننده باید مجوز مشخص‌شده روی گذار را داشته باشد.
/// این بررسی در سرویس (نه کنترلر) انجام می‌شود تا هیچ مسیری دور نزند.
/// </summary>
public sealed class WorkflowApprovalRequest : BaseEntity
{
    /// <summary>شناسه‌ی نمونه.</summary>
    public Guid InstanceId { get; set; }

    /// <summary>شناسه‌ی گذار.</summary>
    public Guid TransitionId { get; set; }

    /// <summary>کد گذار (snapshot).</summary>
    public string TransitionCode { get; set; } = string.Empty;

    /// <summary>کد وضعیتی که نمونه در آن منتظر تصمیم است.</summary>
    public string FromStateCode { get; set; } = string.Empty;

    /// <summary>کد وضعیت مقصد در صورت تأیید.</summary>
    public string ToStateCode { get; set; } = string.Empty;

    /// <summary>مجوز لازم برای تأیید (snapshot).</summary>
    public string? ApproverPermission { get; set; }

    /// <summary>وضعیت درخواست.</summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>زمان درخواست (UTC).</summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>کاربر درخواست‌کننده.</summary>
    public Guid? RequestedById { get; set; }

    /// <summary>زمان تصمیم (UTC).</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>کاربر تصمیم‌گیرنده.</summary>
    public Guid? DecidedById { get; set; }

    /// <summary>نام نمایشی تصمیم‌گیرنده (snapshot).</summary>
    public string? DecidedByUserName { get; set; }

    /// <summary>یادداشت تصمیم (دلیل تأیید/رد).</summary>
    public string? DecisionNote { get; set; }

    /// <summary>زمان انقضای درخواست (UTC). اختیاری.</summary>
    public DateTime? ExpiresAt { get; set; }

    // --- محاسبات ---------------------------------------------------------------

    public bool IsPending => Status == ApprovalStatus.Pending;

    public bool IsExpired(DateTime asOf) => Status == ApprovalStatus.Pending && ExpiresAt is { } expires && asOf > expires;

    // --- چرخه‌ی عمر -------------------------------------------------------------

    public void Approve(Guid? decidedByUserId, string? decidedByUserName, string? note)
    {
        Status = ApprovalStatus.Approved;
        DecidedAt = DateTime.UtcNow;
        DecidedById = decidedByUserId;
        DecidedByUserName = decidedByUserName;
        DecisionNote = note;
    }

    public void Reject(Guid? decidedByUserId, string? decidedByUserName, string? note)
    {
        Status = ApprovalStatus.Rejected;
        DecidedAt = DateTime.UtcNow;
        DecidedById = decidedByUserId;
        DecidedByUserName = decidedByUserName;
        DecisionNote = note;
    }

    public void Cancel()
    {
        Status = ApprovalStatus.Cancelled;
        DecidedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        Status = ApprovalStatus.Expired;
        DecidedAt = DateTime.UtcNow;
    }

    // --- رویدادهای دامنه -------------------------------------------------------

    public void RaiseRequestedEvent()
    {
        RaiseDomainEvent(new WorkflowApprovalRequestedEvent(
            Id, InstanceId, TransitionId, TransitionCode, FromStateCode, ToStateCode, ApproverPermission, RequestedById, ExpiresAt));
    }

    public void RaiseDecidedEvent(bool approved)
    {
        RaiseDomainEvent(new WorkflowApprovalDecidedEvent(
            Id, InstanceId, TransitionCode, approved, DecidedById, DecidedByUserName, DecisionNote));
    }
}
