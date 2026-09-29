using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Workflow.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Workflow.Entities;
using ODCC.Domain.Modules.Workflow.Enums;

namespace ODCC.Application.Modules.Workflow.Abstractions;

/// <summary>
/// مخزن تعاریف گردش کار.
/// </summary>
public interface IWorkflowRepository
{
    Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken ct = default);

    Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>یافتن تعریف (غیر بایگانی‌شده) بر اساس کد یکتا.</summary>
    Task<WorkflowDefinition?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>یافتن تعریف فعال بر اساس کد یکتا.</summary>
    Task<WorkflowDefinition?> FindActiveByCodeAsync(string code, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    Task AddAsync(WorkflowDefinition entity, CancellationToken ct = default);

    void Remove(WorkflowDefinition entity);

    void Update(WorkflowDefinition entity);

    Task<IReadOnlyList<WorkflowDefinition>> SearchAsync(WorkflowSearchRequest request, CancellationToken ct = default);

    Task<int> CountAsync(WorkflowSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد تعاریف فعال.</summary>
    Task<int> CountActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// جایگزینی ساختار یک تعریف (وضعیت‌ها و گذارها). این متد فرزندان قدیمی را
    /// به‌صورت صریح از مخزن حذف می‌کند. حذف مستقیم به‌جای اتکا به
    /// <c>Collection.Clear</c> ضروری است چون گذارها با کلید خارجی غیرنال و
    /// <c>DeleteBehavior.Restrict</c> به وضعیت‌ها اشاره می‌کنند و قطع کردن
    /// ناگهانی آن وابستگیِ الزامی، در زمان ذخیره‌سازی خطا می‌دهد.
    /// </summary>
    void ReplaceStructure(WorkflowDefinition workflow, IEnumerable<WorkflowState> states, IEnumerable<WorkflowTransition> transitions);
}

/// <summary>
/// مخزن نمونه‌های گردش کار.
/// </summary>
public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>یافتن نمونه‌ی فعال (در حال اجرا) یک موجودیت.</summary>
    Task<WorkflowInstance?> FindActiveAsync(WorkflowEntityType entityType, Guid entityId, CancellationToken ct = default);

    Task AddAsync(WorkflowInstance entity, CancellationToken ct = default);

    void Update(WorkflowInstance entity);

    /// <summary>
    /// جستجوی نمونه‌ها. دامنه‌ی سازمانی کاربر (<paramref name="scope"/>) در همان
    /// کوئری پایگاه داده اعمال می‌شود تا صفحه‌بندی بر اساس نتیجه‌ی فیلترشده
    /// صحیح بماند. <c>null</c> یعنی بدون فیلر سازمانی (فقط مسیرهای داخل سرویس).
    /// </summary>
    Task<IReadOnlyList<WorkflowInstance>> SearchAsync(
        WorkflowInstanceSearchRequest request, OrgScope? scope = null, CancellationToken ct = default);

    /// <summary>شمارش نمونه‌ها با همان فیلتر دامنه‌ی سازمانی.</summary>
    Task<int> CountAsync(
        WorkflowInstanceSearchRequest request, OrgScope? scope = null, CancellationToken ct = default);

    /// <summary>تعداد نمونه‌های در حال اجرا.</summary>
    Task<int> CountRunningAsync(CancellationToken ct = default);

    /// <summary>تعداد نمونه‌های تکمیل‌شده.</summary>
    Task<int> CountCompletedAsync(CancellationToken ct = default);
}

/// <summary>
/// مخزن درخواست‌های تأیید.
/// </summary>
public interface IWorkflowApprovalRepository
{
    Task<WorkflowApprovalRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>یافتن درخواست تأیید بازِ یک نمونه (در صورت وجود).</summary>
    Task<WorkflowApprovalRequest?> FindPendingByInstanceAsync(Guid instanceId, CancellationToken ct = default);

    /// <summary>یافتن همه‌ی درخواست‌های یک نمونه (تاریخچه).</summary>
    Task<IReadOnlyList<WorkflowApprovalRequest>> ListByInstanceAsync(Guid instanceId, CancellationToken ct = default);

    Task AddAsync(WorkflowApprovalRequest entity, CancellationToken ct = default);

    void Update(WorkflowApprovalRequest entity);

    /// <summary>
    /// جستجوی درخواست‌های تأیید. دامنه‌ی سازمانی (<paramref name="scope"/>) از طریق
    /// نمونه‌ی مرتبط در همان کوئری اعمال می‌شود.
    /// </summary>
    Task<IReadOnlyList<WorkflowApprovalRequest>> SearchAsync(
        WorkflowApprovalSearchRequest request, OrgScope? scope = null, CancellationToken ct = default);

    /// <summary>شمارش درخواست‌ها با همان فیلتر دامنه‌ی سازمانی.</summary>
    Task<int> CountAsync(
        WorkflowApprovalSearchRequest request, OrgScope? scope = null, CancellationToken ct = default);

    /// <summary>تعداد درخواست‌های در انتظار.</summary>
    Task<int> CountPendingAsync(CancellationToken ct = default);

    /// <summary>
    /// فهرست درخواست‌های در انتظارِ سررسیده‌شده (برای منقضی‌کردن توسط زمان‌بند).
    /// </summary>
    Task<IReadOnlyList<WorkflowApprovalRequest>> ListExpiredAsync(DateTime asOf, int maxResults, CancellationToken ct = default);
}

/// <summary>
/// مرز تراکنشی ماژول گردش کار.
/// </summary>
public interface IWorkflowUnitOfWork : IUnitOfWork;
