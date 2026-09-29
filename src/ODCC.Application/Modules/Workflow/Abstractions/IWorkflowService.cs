using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Workflow.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Workflow.Enums;

namespace ODCC.Application.Modules.Workflow.Abstractions;

/// <summary>
/// سرویس گردش کار: تعریف ماشین وضعیت و چرخه‌ی عمر قابل پیکربندی برای
/// موجودیت‌های سامانه (نظرسنجی، کمپین، ...) همراه با گذارهای نیازمند تأیید.
///
/// <b>مجوزها:</b> مدیریت تعاریف نیازمند <c>workflows.manage</c> است. تأیید
/// گذارها نیازمند مجوزی است که روی گذار مشخص شده (مثلاً <c>surveys.publish</c>) —
/// این بررسی در این سرویس اعمال می‌شود، نه در کنترلر، تا هیچ مسیری دور نزند.
///
/// <b>مرز ماژول‌ها:</b> این سرویس فقط شناسه‌ی موجودیت هدف را نگه می‌دارد و
/// هرگز به موجودیت دامنه‌ی ماژول دیگر ارجاع مستقیم نمی‌دهد.
/// </summary>
public interface IWorkflowService
{
    // --- تعاریف ----------------------------------------------------------------

    Task<PagedResult<WorkflowDto>> SearchWorkflowsAsync(WorkflowSearchRequest request, CancellationToken ct = default);

    Task<Result<WorkflowDto>> GetWorkflowByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>یافتن تعریف فعال بر اساس کد (برای شروع نمونه).</summary>
    Task<Result<WorkflowDto>> FindActiveWorkflowAsync(string code, CancellationToken ct = default);

    Task<Result<WorkflowDto>> CreateWorkflowAsync(SaveWorkflowRequest request, CancellationToken ct = default);

    Task<Result<WorkflowDto>> UpdateWorkflowAsync(Guid id, SaveWorkflowRequest request, CancellationToken ct = default);

    Task<Result<WorkflowDto>> ActivateWorkflowAsync(Guid id, CancellationToken ct = default);

    Task<Result> ArchiveWorkflowAsync(Guid id, CancellationToken ct = default);

    // --- نمونه‌ها --------------------------------------------------------------

    Task<PagedResult<WorkflowInstanceDto>> SearchInstancesAsync(WorkflowInstanceSearchRequest request, CancellationToken ct = default);

    Task<Result<WorkflowInstanceDto>> GetInstanceByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// یافتن نمونه‌ی فعال یک موجودیت (در صورت وجود). مفید برای ماژول‌هایی که
    /// می‌خواهند بدانند آیا موجودیتشان در گردش کاری است.
    /// </summary>
    Task<Result<WorkflowInstanceDto>> FindActiveInstanceAsync(WorkflowEntityType entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>شروع یک نمونه‌ی جدید از یک تعریف فعال.</summary>
    Task<Result<WorkflowInstanceDto>> StartInstanceAsync(StartWorkflowInstanceRequest request, CancellationToken ct = default);

    /// <summary>
    /// انجام یک گذار روی نمونه. اگر گذار نیازمند تأیید باشد، یک درخواست تأیید
    /// ایجاد می‌کند و نمونه در وضعیت قبلی می‌ماند تا تصمیم گرفته شود.
    /// </summary>
    Task<Result<WorkflowInstanceDto>> TransitionInstanceAsync(
        Guid instanceId, TransitionWorkflowInstanceRequest request, CancellationToken ct = default);

    /// <summary>لغو یک نمونه (و درخواست تأیید باز آن).</summary>
    Task<Result<WorkflowInstanceDto>> CancelInstanceAsync(Guid instanceId, CancellationToken ct = default);

    // --- تأییدها ---------------------------------------------------------------

    Task<PagedResult<WorkflowApprovalRequestDto>> SearchApprovalsAsync(
        WorkflowApprovalSearchRequest request, CancellationToken ct = default);

    /// <summary>دریافت یک درخواست تأیید با شناسه.</summary>
    Task<Result<WorkflowApprovalRequestDto>> GetApprovalByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>تأیید یک درخواست تأیید. کاربر باید مجوز مشخص‌شده روی گذار را داشته باشد.</summary>
    Task<Result<WorkflowApprovalRequestDto>> ApproveAsync(Guid approvalRequestId, DecideWorkflowApprovalRequest request, CancellationToken ct = default);

    /// <summary>رد یک درخواست تأیید. کاربر باید مجوز مشخص‌شده روی گذار را داشته باشد.</summary>
    Task<Result<WorkflowApprovalRequestDto>> RejectAsync(Guid approvalRequestId, DecideWorkflowApprovalRequest request, CancellationToken ct = default);

    /// <summary>
    /// منقضی‌کردن درخواست‌های تأیید سررسیده‌شده. یک اثر جانبی است و توسط
    /// زمان‌بند پس‌زمینه (با تأیید صریح) صدا زده می‌شود.
    /// </summary>
    /// <returns>تعداد درخواست‌های منقضی‌شده.</returns>
    Task<int> ExpireDueApprovalsAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>آمار گردش کار برای داشبورد.</summary>
    Task<Result<WorkflowStatsDto>> GetStatsAsync(CancellationToken ct = default);
}
