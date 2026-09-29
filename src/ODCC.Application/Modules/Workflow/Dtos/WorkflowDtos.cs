using ODCC.Application.Abstractions;
using ODCC.Domain.Modules.Workflow.Enums;

namespace ODCC.Application.Modules.Workflow.Dtos;

// --- تعریف گردش کار: درخواست‌ها ---------------------------------------------

/// <summary>فیلتر جستجوی تعاریف گردش کار.</summary>
public sealed record WorkflowSearchRequest
{
    public string? SearchText { get; init; }
    public WorkflowStatus? Status { get; init; }
    public WorkflowEntityType? EntityType { get; init; }

    /// <summary>شامل تعاریف بایگانی‌شده (حذف نرم) شود؟</summary>
    public bool IncludeArchived { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>یک وضعیت در تعریف.</summary>
public sealed record WorkflowStateRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public bool IsInitial { get; init; }
    public bool IsFinal { get; init; }
    public int DisplayOrder { get; init; }
}

/// <summary>یک گذار در تعریف.</summary>
public sealed record WorkflowTransitionRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string FromStateCode { get; init; }
    public required string ToStateCode { get; init; }
    public bool RequiresApproval { get; init; }

    /// <summary>مجوز لازم برای تأیید (الزامی اگر RequiresApproval باشد).</summary>
    public string? ApproverPermission { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record SaveWorkflowRequest
{
    public required string Name { get; init; }
    public required string Code { get; init; }
    public string? Description { get; init; }
    public WorkflowEntityType EntityType { get; init; } = WorkflowEntityType.Custom;

    /// <summary>وضعیت‌ها. حداقل یک وضعیت اولیه الزامی است.</summary>
    public required IReadOnlyList<WorkflowStateRequest> States { get; init; }

    /// <summary>گذارها. می‌تواند خالی باشد.</summary>
    public IReadOnlyList<WorkflowTransitionRequest>? Transitions { get; init; }

    /// <summary>آیا بلافاصله پس از ساخت فعال شود؟</summary>
    public bool ActivateImmediately { get; init; }
}

// --- نمونه: درخواست‌ها -------------------------------------------------------

/// <summary>فیلتر جستجوی نمونه‌های گردش کار.</summary>
public sealed record WorkflowInstanceSearchRequest
{
    public string? SearchText { get; init; }
    public WorkflowInstanceState? Status { get; init; }
    public Guid? WorkflowId { get; init; }
    public WorkflowEntityType? EntityType { get; init; }

    /// <summary>فیلتر بر اساس موجودیت هدف.</summary>
    public Guid? EntityId { get; init; }

    /// <summary>فقط نمونه‌های در حال اجرا؟</summary>
    public bool RunningOnly { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>شروع یک نمونه‌ی جدید.</summary>
public sealed record StartWorkflowInstanceRequest
{
    /// <summary>کد تعریف گردش کار (باید فعال باشد).</summary>
    public required string WorkflowCode { get; init; }

    /// <summary>نوع موجودیت هدف.</summary>
    public WorkflowEntityType EntityType { get; init; } = WorkflowEntityType.Custom;

    /// <summary>شناسه‌ی موجودیت هدف.</summary>
    public required Guid EntityId { get; init; }

    /// <summary>زمینه‌ی اختیاری (JSON). فقط متادیتای عمومی.</summary>
    public string? ContextJson { get; init; }
}

/// <summary>انجام یک گذار روی نمونه.</summary>
public sealed record TransitionWorkflowInstanceRequest
{
    /// <summary>کد گذار مورد نظر.</summary>
    public required string TransitionCode { get; init; }

    /// <summary>یادداشت اختیاری (برای درخواست تأیید استفاده می‌شود).</summary>
    public string? Note { get; init; }

    /// <summary>
    /// زمان انقضای اختیاری برای درخواست تأیید (UTC). اگر گذار نیازمند تأیید باشد
    /// و این مقدار تنظیم شود، درخواست تأیید پس از این زمان به‌صورت خودکار
    /// منقضی می‌شود (زمان‌بند انقضا). باید در آینده باشد.
    /// </summary>
    public DateTime? ApprovalExpiresAtUtc { get; init; }
}

// --- تأییدها: درخواست‌ها -----------------------------------------------------

/// <summary>فیلتر جستجوی درخواست‌های تأیید.</summary>
public sealed record WorkflowApprovalSearchRequest
{
    public ApprovalStatus? Status { get; init; }
    public Guid? InstanceId { get; init; }

    /// <summary>فقط درخواست‌های قابل تأیید توسط کاربر جاری (بر اساس مجوز).</summary>
    public bool ApprovableByMe { get; init; }

    /// <summary>فقط درخواست‌های در انتظار؟</summary>
    public bool PendingOnly { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record DecideWorkflowApprovalRequest
{
    /// <summary>یادداشت دلیل (اختیاری).</summary>
    public string? Note { get; init; }
}

// --- خروجی‌ها -----------------------------------------------------------------

public sealed record WorkflowStateDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsInitial { get; init; }
    public bool IsFinal { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record WorkflowTransitionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FromStateCode { get; init; } = string.Empty;
    public string ToStateCode { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public string? ApproverPermission { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record WorkflowDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public WorkflowEntityType EntityType { get; init; }
    public WorkflowStatus Status { get; init; }
    public int Version { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public string? CreatedByUserName { get; init; }
    public DateTime CreatedAt { get; init; }

    public IReadOnlyList<WorkflowStateDto> States { get; init; } = [];
    public IReadOnlyList<WorkflowTransitionDto> Transitions { get; init; } = [];

    /// <summary>نقشه‌ی گذارهای مجاز از هر وضعیت: «از کد» → کدهای گذار.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> AvailableTransitions { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}

public sealed record WorkflowInstanceDto
{
    public Guid Id { get; init; }
    public Guid WorkflowId { get; init; }
    public string WorkflowCode { get; init; } = string.Empty;
    public int WorkflowVersion { get; init; }
    public WorkflowEntityType EntityType { get; init; }
    public Guid EntityId { get; init; }
    public string CurrentStateCode { get; init; } = string.Empty;
    public WorkflowInstanceState Status { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public Guid? StartedByUserId { get; init; }
    public string? StartedByUserName { get; init; }
    public string? ContextJson { get; init; }
    public int TransitionCount { get; init; }

    /// <summary>گذارهای مجاز از وضعیت جاری (موجودیت کاربر برای تصمیم بعدی).</summary>
    public IReadOnlyList<WorkflowTransitionDto> NextTransitions { get; init; } = [];

    /// <summary>درخواست تأیید فعلیِ این نمونه (در صورت وجود).</summary>
    public WorkflowApprovalRequestDto? PendingApproval { get; init; }
}

public sealed record WorkflowApprovalRequestDto
{
    public Guid Id { get; init; }
    public Guid InstanceId { get; init; }
    public Guid TransitionId { get; init; }
    public string TransitionCode { get; init; } = string.Empty;
    public string FromStateCode { get; init; } = string.Empty;
    public string ToStateCode { get; init; } = string.Empty;
    public string? ApproverPermission { get; init; }
    public ApprovalStatus Status { get; init; }
    public DateTime RequestedAt { get; init; }
    public Guid? RequestedById { get; init; }
    public DateTime? DecidedAt { get; init; }
    public Guid? DecidedById { get; init; }
    public string? DecidedByUserName { get; init; }
    public string? DecisionNote { get; init; }
    public DateTime? ExpiresAt { get; init; }

    /// <summary>آیا کاربر جاری می‌تواند روی این درخواست تصمیم بگیرد؟</summary>
    public bool CanCurrentUserDecide { get; init; }
}

/// <summary>آمار گردش کار برای داشبورد.</summary>
public sealed record WorkflowStatsDto
{
    public int TotalWorkflows { get; init; }
    public int ActiveWorkflows { get; init; }
    public int RunningInstances { get; init; }
    public int CompletedInstances { get; init; }
    public int PendingApprovals { get; init; }
}
