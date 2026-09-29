using ODCC.Domain.Common;
using ODCC.Domain.Modules.Workflow.Enums;
using ODCC.Domain.Modules.Workflow.Events;

namespace ODCC.Domain.Modules.Workflow.Entities;

/// <summary>
/// تعریف یک گردش کار: مجموعه‌ای از وضعیت‌ها و گذارهای مجاز بین آن‌ها که
/// چرخه‌ی عمر یک نوع موجودیت (مثلاً نظرسنجی) را کنترل می‌کند.
///
/// این موجودیت aggregate ریشه است و <see cref="WorkflowState"/> و
/// <see cref="WorkflowTransition"/> فرزندان آن هستند. گذارهای requiring approval
/// توسط <see cref="WorkflowApprovalRequest"/> (در نمونه) نگه داشته می‌شوند.
///
/// <b>طراحی:</b> تعریف گردش کار روی چندین نمونه به‌صورت ایمن تغییرناپذیر است؛
/// هر نمونه شماره‌ی نسخه‌ی تعریف را در زمان ساخت snapshot می‌گیرد تا تغییر
/// تعریف، نمونه‌های در حال اجرا را نشکند.
/// </summary>
public sealed class WorkflowDefinition : BaseEntity
{
    /// <summary>نام نمایشی گردش کار.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>کد یکتا (مثلاً «survey-approval»).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>توضیح هدف گردش کار.</summary>
    public string? Description { get; set; }

    /// <summary>نوع موجودیتی که این گردش کار بر آن حاکم است.</summary>
    public WorkflowEntityType EntityType { get; set; } = WorkflowEntityType.Custom;

    /// <summary>چرخه‌ی عمر تعریف.</summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;

    /// <summary>
    /// نسخه‌ی تعریف. با هر انتشار یکتا می‌شود تا نمونه‌ها بتوانند snapshot
    /// نسخه‌ی خود را نگه دارند.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>کاربری که تعریف را ایجاد کرده.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>نام نمایشی ایجادکننده (snapshot).</summary>
    public string? CreatedByUserName { get; set; }

    /// <summary>وضعیت‌های این گردش کار.</summary>
    public List<WorkflowState> States { get; set; } = [];

    /// <summary>گذارهای مجاز بین وضعیت‌ها.</summary>
    public List<WorkflowTransition> Transitions { get; set; } = [];

    // --- محاسبات ---------------------------------------------------------------

    /// <summary>وضعیت اولیه (دقیقاً یکی باید وجود داشته باشد).</summary>
    public WorkflowState? InitialState => States.OrderBy(s => s.DisplayOrder).FirstOrDefault(s => s.IsInitial);

    /// <summary>آیا تعریف قابل استفاده است؟</summary>
    public bool IsUsable => Status == WorkflowStatus.Active && InitialState is not null;

    // --- چرخه‌ی عمر -------------------------------------------------------------

    /// <summary>فعال‌سازی تعریف (انتشار). نیاز به حداقل یک وضعیت اولیه دارد.</summary>
    public void Activate()
    {
        Status = WorkflowStatus.Active;
    }

    /// <summary>بایگانی نرم تعریف.</summary>
    public void Archive()
    {
        Status = WorkflowStatus.Archived;
    }

    /// <summary>افزودن یک وضعیت جدید.</summary>
    public WorkflowState AddState(string code, string name, bool isInitial = false, bool isFinal = false, int displayOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (States.Any(s => s.Code == code))
        {
            throw new InvalidOperationException($"وضعیتی با کد «{code}» از قبل وجود دارد.");
        }

        if (isInitial)
        {
            // فقط یک وضعیت اولیه مجاز است.
            foreach (var existing in States)
            {
                existing.IsInitial = false;
            }
        }

        var state = new WorkflowState
        {
            WorkflowId = Id,
            Code = code,
            Name = name,
            IsInitial = isInitial,
            IsFinal = isFinal,
            DisplayOrder = displayOrder
        };

        States.Add(state);

        return state;
    }

    /// <summary>افزودن یک گذار جدید بین دو وضعیت.</summary>
    public WorkflowTransition AddTransition(
        string code, string name, string fromStateCode, string toStateCode,
        bool requiresApproval = false, string? approverPermission = null, int displayOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var fromState = States.FirstOrDefault(s => s.Code == fromStateCode)
            ?? throw new InvalidOperationException($"وضعیت مبدا «{fromStateCode}» وجود ندارد.");

        var toState = States.FirstOrDefault(s => s.Code == toStateCode)
            ?? throw new InvalidOperationException($"وضعیت مقصد «{toStateCode}» وجود ندارد.");

        if (Transitions.Any(t => t.Code == code))
        {
            throw new InvalidOperationException($"گذاری با کد «{code}» از قبل وجود دارد.");
        }

        if (requiresApproval && string.IsNullOrWhiteSpace(approverPermission))
        {
            throw new InvalidOperationException("گذارِ نیازمند تأیید، یک مجوز تأییدکننده باید مشخص کند.");
        }

        var transition = new WorkflowTransition
        {
            WorkflowId = Id,
            Code = code,
            Name = name,
            FromStateId = fromState.Id,
            ToStateId = toState.Id,
            RequiresApproval = requiresApproval,
            ApproverPermission = approverPermission,
            DisplayOrder = displayOrder
        };

        Transitions.Add(transition);

        return transition;
    }

    /// <summary>یافتن یک گذار با کد.</summary>
    public WorkflowTransition? FindTransition(string transitionCode) =>
        Transitions.FirstOrDefault(t => t.Code == transitionCode);

    /// <summary>یافتن یک وضعیت با کد.</summary>
    public WorkflowState? FindState(string stateCode) =>
        States.FirstOrDefault(s => s.Code == stateCode);

    // --- رویدادهای دامنه -------------------------------------------------------

    public void RaiseCreatedEvent()
    {
        RaiseDomainEvent(new WorkflowCreatedEvent(Id, Name, Code, EntityType, Version));
    }

    public void RaiseUpdatedEvent()
    {
        RaiseDomainEvent(new WorkflowUpdatedEvent(Id, Name, Code));
    }

    public void RaiseActivatedEvent()
    {
        RaiseDomainEvent(new WorkflowActivatedEvent(Id, Name, Code, Version));
    }

    public void RaiseArchivedEvent()
    {
        RaiseDomainEvent(new WorkflowArchivedEvent(Id, Name, Code));
    }
}

/// <summary>
/// یک وضعیت در ماشین وضعیت گردش کار.
/// </summary>
public sealed class WorkflowState : BaseEntity
{
    /// <summary>شناسه‌ی گردش کار والد.</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>کد ماشین وضعیت (مثلاً «draft»).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>نام نمایشی.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>آیا این وضعیت اولیه است؟</summary>
    public bool IsInitial { get; set; }

    /// <summary>آیا این وضعیت پایانی است؟</summary>
    public bool IsFinal { get; set; }

    /// <summary>ترتیب نمایش.</summary>
    public int DisplayOrder { get; set; }
}

/// <summary>
/// یک گذار مجاز بین دو وضعیت. می‌تواند نیازمند تأیید باشد.
/// </summary>
public sealed class WorkflowTransition : BaseEntity
{
    /// <summary>شناسه‌ی گردش کار والد.</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>کد گذار (مثلاً «publish»).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>نام نمایشی.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>وضعیت مبدا.</summary>
    public Guid FromStateId { get; set; }

    /// <summary>وضعیت مقصد.</summary>
    public Guid ToStateId { get; set; }

    /// <summary>آیا این گذار نیازمند تأیید است؟</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// مجوز لازم برای تأیید این گذار (مثلاً «surveys.publish»). فقط وقتی
    /// <see cref="RequiresApproval"/> مقدار دارد. تأییدکننده باید این مجوز را
    /// داشته باشد — مجوز فیلتر سمت سرور است.
    /// </summary>
    public string? ApproverPermission { get; set; }

    /// <summary>ترتیب نمایش.</summary>
    public int DisplayOrder { get; set; }

    // --- پیمایش -----------------------------------------------------------------

    public WorkflowState? FromState { get; set; }
    public WorkflowState? ToState { get; set; }
}
