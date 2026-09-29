using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Workflow.Abstractions;
using ODCC.Application.Modules.Workflow.Dtos;
using ODCC.Domain.Modules.Workflow.Entities;
using ODCC.Domain.Modules.Workflow.Enums;
using ODCC.Infrastructure.Modules.Workflow.Persistence;

namespace ODCC.Infrastructure.Modules.Workflow.Repositories;

/// <summary>
/// پیاده‌سازی مخزن تعاریف گردش کار.
/// </summary>
public sealed class WorkflowRepository(WorkflowDbContext dbContext) : IWorkflowRepository
{
    private readonly WorkflowDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Workflows
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public async Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Workflows
            .Include(w => w.States.OrderBy(s => s.DisplayOrder))
            .Include(w => w.Transitions.OrderBy(t => t.DisplayOrder))
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    /// <inheritdoc/>
    public async Task<WorkflowDefinition?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        await _dbContext.Workflows
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Code == code && w.Status != WorkflowStatus.Archived, ct);

    /// <inheritdoc/>
    public async Task<WorkflowDefinition?> FindActiveByCodeAsync(string code, CancellationToken ct = default) =>
        await _dbContext.Workflows
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Code == code && w.Status == WorkflowStatus.Active, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Workflows.CountAsync(ct);

    public async Task AddAsync(WorkflowDefinition entity, CancellationToken ct = default) =>
        await _dbContext.Workflows.AddAsync(entity, ct);

    public void Remove(WorkflowDefinition entity) => entity.IsDeleted = true;

    public void Update(WorkflowDefinition entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Workflows.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowDefinition>> SearchAsync(WorkflowSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(WorkflowSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountActiveAsync(CancellationToken ct = default) =>
        await _dbContext.Workflows.CountAsync(w => w.Status == WorkflowStatus.Active, ct);

    /// <inheritdoc/>
    public void ReplaceStructure(WorkflowDefinition workflow, IEnumerable<WorkflowState> states, IEnumerable<WorkflowTransition> transitions)
    {
        // حذف صریح فرزندان قدیدی به‌جای اتکا به Collection.Clear: گذارها با
        // کلید خارجی غیرنال و DeleteBehavior.Restrict به وضعیت‌ها اشاره می‌کنند.
        // گذارها باید پیش از وضعیت‌ها حذف شوند تا در زمان DetectChanges،
        // وابستگیِ الزامیِ گذارها به وضعیت‌ها قطع‌شده دیده نشود.
        if (workflow.Transitions.Count > 0)
        {
            foreach (var transition in workflow.Transitions)
            {
                transition.FromState = null;
                transition.ToState = null;
            }

            _dbContext.WorkflowTransitions.RemoveRange(workflow.Transitions);
            workflow.Transitions.Clear();
        }

        if (workflow.States.Count > 0)
        {
            _dbContext.WorkflowStates.RemoveRange(workflow.States);
            workflow.States.Clear();
        }

        foreach (var state in states)
        {
            state.WorkflowId = workflow.Id;
            workflow.States.Add(state);
        }

        foreach (var transition in transitions)
        {
            transition.WorkflowId = workflow.Id;
            workflow.Transitions.Add(transition);
        }
    }

    private IQueryable<WorkflowDefinition> BuildSearchQuery(WorkflowSearchRequest request)
    {
        var query = _dbContext.Workflows
            .Include(w => w.States)
            .AsNoTracking();

        if (request.Status is { } status)
        {
            query = query.Where(w => w.Status == status);
        }
        else if (!request.IncludeArchived)
        {
            query = query.Where(w => w.Status != WorkflowStatus.Archived);
        }

        if (request.EntityType is { } entityType)
        {
            query = query.Where(w => w.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(w =>
                w.Name.Contains(text) ||
                w.Code.Contains(text) ||
                (w.Description != null && w.Description.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن نمونه‌های گردش کار.
/// </summary>
public sealed class WorkflowInstanceRepository(WorkflowDbContext dbContext) : IWorkflowInstanceRepository
{
    private readonly WorkflowDbContext _dbContext = dbContext;

    public async Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.WorkflowInstances
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    /// <inheritdoc/>
    public async Task<WorkflowInstance?> FindActiveAsync(WorkflowEntityType entityType, Guid entityId, CancellationToken ct = default) =>
        await _dbContext.WorkflowInstances
            .FirstOrDefaultAsync(i => i.EntityType == entityType && i.EntityId == entityId
                && i.Status == WorkflowInstanceState.Running, ct);

    public async Task AddAsync(WorkflowInstance entity, CancellationToken ct = default) =>
        await _dbContext.WorkflowInstances.AddAsync(entity, ct);

    public void Update(WorkflowInstance entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.WorkflowInstances.Update(entity);
    }

    /// <summary>
    /// جستجوی نمونه‌ها. دامنه‌ی سازمانی کاربر در همان کوئری اعمال می‌شود.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowInstance>> SearchAsync(
        WorkflowInstanceSearchRequest request, OrgScope? scope = null, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request, scope);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(i => i.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(
        WorkflowInstanceSearchRequest request, OrgScope? scope = null, CancellationToken ct = default) =>
        await BuildSearchQuery(request, scope).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountRunningAsync(CancellationToken ct = default) =>
        await _dbContext.WorkflowInstances.CountAsync(i => i.Status == WorkflowInstanceState.Running, ct);

    /// <inheritdoc/>
    public async Task<int> CountCompletedAsync(CancellationToken ct = default) =>
        await _dbContext.WorkflowInstances.CountAsync(i => i.Status == WorkflowInstanceState.Completed, ct);

    private IQueryable<WorkflowInstance> BuildSearchQuery(WorkflowInstanceSearchRequest request, OrgScope? scope)
    {
        var query = _dbContext.WorkflowInstances.AsNoTracking();

        // فیلتر دامنه‌ی سازمانی: فقط نمونه‌های داخل زیردرخت قابل‌مشاهده‌ی
        // کاربر. دامنه‌ی Company محدودیتی ندارد؛ دامنه‌های دیگر fail-closed
        // هستند (مسیر خالی = هیچ داده‌ای).
        if (scope is { IsUnrestricted: false })
        {
            var prefix = scope.VisiblePathPrefix;

            query = string.IsNullOrWhiteSpace(prefix)
                ? query.Where(i => false)
                : query.Where(i => i.OrgUnitPath != null && i.OrgUnitPath.StartsWith(prefix));
        }

        if (request.Status is { } status)
        {
            query = query.Where(i => i.Status == status);
        }

        if (request.RunningOnly)
        {
            query = query.Where(i => i.Status == WorkflowInstanceState.Running);
        }

        if (request.WorkflowId is { } workflowId)
        {
            query = query.Where(i => i.WorkflowId == workflowId);
        }

        if (request.EntityType is { } entityType)
        {
            query = query.Where(i => i.EntityType == entityType);
        }

        if (request.EntityId is { } entityId)
        {
            query = query.Where(i => i.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(i =>
                i.WorkflowCode.Contains(text) ||
                i.CurrentStateCode.Contains(text) ||
                (i.ContextJson != null && i.ContextJson.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن درخواست‌های تأیید.
/// </summary>
/// <summary>
/// پیاده‌سازی مخزن درخواست‌های تأیید.
///
/// <b>فیلتر «قابل تأیید توسط من»:</b> این مخزن <see cref="ICurrentUserService"/>
/// را تزریق می‌کند تا بتواند درخواست‌هایی که کاربر جاری مجوز تصمیم درباره‌ی
/// آن‌ها را دارد (بر اساس <see cref="WorkflowApprovalRequest.ApproverPermission"/>)
/// در همان کوئری پایگاه داده فیلتر کند. این کار در مخزن انجام می‌شود تا
/// صفحه‌بندی بر اساس نتیجه‌ی فیلترشده صحیح بماند.
/// </summary>
public sealed class WorkflowApprovalRepository(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService) : IWorkflowApprovalRepository
{
    private readonly WorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<WorkflowApprovalRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.WorkflowApprovalRequests.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc/>
    public async Task<WorkflowApprovalRequest?> FindPendingByInstanceAsync(Guid instanceId, CancellationToken ct = default) =>
        await _dbContext.WorkflowApprovalRequests
            .FirstOrDefaultAsync(a => a.InstanceId == instanceId && a.Status == ApprovalStatus.Pending, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowApprovalRequest>> ListByInstanceAsync(Guid instanceId, CancellationToken ct = default) =>
        await _dbContext.WorkflowApprovalRequests
            .Where(a => a.InstanceId == instanceId)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync(ct);

    public async Task AddAsync(WorkflowApprovalRequest entity, CancellationToken ct = default) =>
        await _dbContext.WorkflowApprovalRequests.AddAsync(entity, ct);

    public void Update(WorkflowApprovalRequest entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.WorkflowApprovalRequests.Update(entity);
    }

    /// <summary>
    /// جستجوی درخواست‌های تأیید. دامنه‌ی سازمانی از طریق نمونه‌ی مرتبط
    /// در همان کوئری اعمال می‌شود.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowApprovalRequest>> SearchAsync(
        WorkflowApprovalSearchRequest request, OrgScope? scope = null, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request, scope);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(a => a.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(
        WorkflowApprovalSearchRequest request, OrgScope? scope = null, CancellationToken ct = default) =>
        await BuildSearchQuery(request, scope).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountPendingAsync(CancellationToken ct = default) =>
        await _dbContext.WorkflowApprovalRequests.CountAsync(a => a.Status == ApprovalStatus.Pending, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowApprovalRequest>> ListExpiredAsync(DateTime asOf, int maxResults, CancellationToken ct = default) =>
        await _dbContext.WorkflowApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending && a.ExpiresAt != null && a.ExpiresAt < asOf)
            .OrderBy(a => a.ExpiresAt)
            .Take(Math.Clamp(maxResults, 1, 500))
            .ToListAsync(ct);

    private IQueryable<WorkflowApprovalRequest> BuildSearchQuery(WorkflowApprovalSearchRequest request, OrgScope? scope)
    {
        var query = _dbContext.WorkflowApprovalRequests.AsNoTracking();

        // فیلتر دامنه‌ی سازمانی از طریق نمونه‌ی مرتبط: فقط درخواست‌هایی که
        // نمونه‌شان داخل زیردرخت قابل‌مشاهده‌ی کاربر است. fail-closed.
        if (scope is { IsUnrestricted: false })
        {
            var prefix = scope.VisiblePathPrefix;

            query = string.IsNullOrWhiteSpace(prefix)
                ? query.Where(a => false)
                : query.Where(a => _dbContext.WorkflowInstances
                    .Any(i => i.Id == a.InstanceId && i.OrgUnitPath != null && i.OrgUnitPath.StartsWith(prefix)));
        }

        if (request.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }
        else if (request.PendingOnly)
        {
            query = query.Where(a => a.Status == ApprovalStatus.Pending);
        }

        if (request.InstanceId is { } instanceId)
        {
            query = query.Where(a => a.InstanceId == instanceId);
        }

        if (request.ApprovableByMe)
        {
            // فیلتر سمت سرور: فقط درخواست‌هایی که کاربر جاری مجوز تأیید آن‌ها را
            // دارد. اگر گذار مجوزی مشخص نکرده، مجوز مدیریت گردش کار لازم است.
            var permissions = _currentUserService.Permissions.ToList();

            query = permissions.Count == 0
                ? query.Where(a => false)
                : query.Where(a =>
                    (a.ApproverPermission != null && permissions.Contains(a.ApproverPermission))
                    || (a.ApproverPermission == null && permissions.Contains(ODCC.Application.Authorization.Permissions.Workflows.Manage)));
        }

        return query;
    }
}
