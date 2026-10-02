using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.ActionManagement.Dtos;
using ODCC.Domain.Modules.ActionManagement.Entities;
using ODCC.Domain.Modules.ActionManagement.Enums;
using ODCC.Infrastructure.Modules.ActionManagement.Persistence;

namespace ODCC.Infrastructure.Modules.ActionManagement.Repositories;

/// <summary>
/// پیاده‌سازی مخزن برنامه‌های اقدام.
///
/// <b>مرز سازمانی:</b> جستجوها همواره با <see cref="OrgScope"/> (محاسبه‌شده در
/// سرویس از کاربر جاری) فیلتر می‌شوند. این مقدار هرگز از کلاینت نمی‌آید تا
/// جعل دامنه‌ی قابل‌مشاهده ممکن نباشد. اگر دامنه قابل‌مشاهده نامشخص باشد،
/// هیچ داده‌ای برنمی‌گردد (fail-closed).
/// </summary>
public sealed class ActionPlanRepository(ActionManagementDbContext dbContext) : IActionPlanRepository
{
    private readonly ActionManagementDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ActionPlan>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.ActionPlans
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<ActionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.ActionPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.ActionPlans.CountAsync(ct);

    public async Task AddAsync(ActionPlan entity, CancellationToken ct = default) =>
        await _dbContext.ActionPlans.AddAsync(entity, ct);

    public void Remove(ActionPlan entity) => entity.IsDeleted = true;

    public void Update(ActionPlan entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.ActionPlans.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActionPlan>> SearchAsync(
        ActionPlanSearchRequest request, OrgScope scope, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request, scope);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(
        ActionPlanSearchRequest request, OrgScope scope, CancellationToken ct = default) =>
        await BuildSearchQuery(request, scope).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<ActionPlan?> FindBySourceKeyAsync(string sourceKey, CancellationToken ct = default) =>
        await _dbContext.ActionPlans.FirstOrDefaultAsync(p => p.SourceKey == sourceKey, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActionPlan>> ListOpenBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        await _dbContext.ActionPlans
            .Where(p => p.SurveyId == surveyId
                && (p.Status == ActionPlanStatus.Draft || p.Status == ActionPlanStatus.Active))
            .ToListAsync(ct);

    private IQueryable<ActionPlan> BuildSearchQuery(ActionPlanSearchRequest request, OrgScope scope)
    {
        var query = _dbContext.ActionPlans.AsNoTracking();

        if (request.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }
        else if (!request.IncludeArchived)
        {
            // اگر وضعیت خاصی (حتی Archived) صریحاً خواسته نشده، بایگانی‌شده‌ها حذف می‌شوند.
            query = query.Where(p => p.Status != ActionPlanStatus.Archived);
        }

        if (request.Priority is { } priority)
            query = query.Where(p => p.Priority == priority);

        if (request.Source is { } source)
            query = query.Where(p => p.Source == source);

        if (request.SurveyId is { } surveyId)
            query = query.Where(p => p.SurveyId == surveyId);

        // --- مرز سازمانی (fail-closed) -----------------------------------
        if (scope.IsUnrestricted)
        {
            // دامنه‌ی شرکت: هیچ فیلتر سازمانی لازم نیست.
        }
        else if (!string.IsNullOrWhiteSpace(scope.VisiblePathPrefix))
        {
            var prefix = scope.VisiblePathPrefix;

            query = request.IncludeDescendants
                ? query.Where(p => p.OrgUnitPath != null && p.OrgUnitPath.StartsWith(prefix))
                : query.Where(p => p.OrgUnitId == scope.AnchorOrgUnitId);
        }
        else if (scope.AnchorOrgUnitId is { } anchorId)
        {
            // مسیر در دسترس نیست: فقط برنامه‌های دقیقاً روی واحد لنگر.
            query = query.Where(p => p.OrgUnitId == anchorId);
        }
        else
        {
            // دامنه‌ی Own یا لنگر نامعتبر: هیچ برنامه‌ی سازمانی قابل‌مشاهده نیست.
            query = query.Where(p => false);
        }

        // فیلتر صریح کاربر روی واحد سازمانی (فقط اگر در دامنه‌ی او باشد).
        if (request.OrgUnitId is { } requestedUnit)
        {
            if (scope.IsUnrestricted || scope.CanAccess(requestedUnit, resourceOrgPath: null))
            {
                query = query.Where(p => p.OrgUnitId == requestedUnit);
            }
            else
            {
                // درخواست واحد خارج از دامنه: نتیجه خالی.
                query = query.Where(p => false);
            }
        }

        if (request.MineOnly && scope is { AnchorOrgUnitId: null } && !scope.IsUnrestricted)
        {
            // بدون لنگر سازمانی، «فقط برنامه‌های من» به مالکیت/ایجاد مستقیم محدود می‌شود.
            query = query.Where(p => false);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(p =>
                p.Title.Contains(text) ||
                (p.Description != null && p.Description.Contains(text)) ||
                (p.SurveyTitle != null && p.SurveyTitle.Contains(text)) ||
                (p.SurveyCode != null && p.SurveyCode.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن آیتم‌های اقدام.
///
/// <b>مرز سازمانی:</b> مثل برنامه‌ها، با یک استثناء‌ی مستند: کاربر همیشه
/// آیتم‌های منتسب به خودش را می‌بیند تا بتواند کارهای واگذاری‌شده‌اش را
/// انجام دهد، حتی اگر آیتم در واحد سازمانی دیگری باشد.
/// </summary>
public sealed class ActionItemRepository(ActionManagementDbContext dbContext) : IActionItemRepository
{
    private readonly ActionManagementDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ActionItem>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.ActionItems
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async Task<ActionItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.ActionItems
            .Include(i => i.ActionPlan)
            .Include(i => i.Comments)
            .Include(i => i.Evidence)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.ActionItems.CountAsync(ct);

    public async Task AddAsync(ActionItem entity, CancellationToken ct = default) =>
        await _dbContext.ActionItems.AddAsync(entity, ct);

    public void Remove(ActionItem entity) => entity.IsDeleted = true;

    public void Update(ActionItem entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.ActionItems.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActionItem>> SearchAsync(
        ActionItemSearchRequest request, OrgScope scope, Guid currentUserId, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request, scope, currentUserId);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        // Include برنامه برای فیلتر دامنه‌ی سازمانی و نمایش عنوان برنامه.
        // شمارش دیدگاه/پیوست در فهرست صفر است؛ جزئیات از مسیر اختصاصی خوانده می‌شود.
        return await query
            .Include(i => i.ActionPlan)
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(
        ActionItemSearchRequest request, OrgScope scope, Guid currentUserId, CancellationToken ct = default) =>
        await BuildSearchQuery(request, scope, currentUserId).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActionItem>> ListByPlanAsync(Guid planId, CancellationToken ct = default) =>
        await _dbContext.ActionItems
            .Where(i => i.ActionPlanId == planId)
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActionItem>> ListDueRemindersAsync(
        DateTime asOf, int maxResults, CancellationToken ct = default) =>
        await _dbContext.ActionItems
            .Where(i => i.RemindAt != null && i.RemindAt <= asOf
                && (i.Status == ActionItemStatus.Open || i.Status == ActionItemStatus.InProgress))
            .OrderBy(i => i.RemindAt)
            .Take(Math.Clamp(maxResults, 1, 500))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActionItem>> ListOverdueAsync(
        DateTime asOf, int maxResults, CancellationToken ct = default) =>
        await _dbContext.ActionItems
            .Where(i => i.DueDate != null && i.DueDate < asOf
                && (i.Status == ActionItemStatus.Open || i.Status == ActionItemStatus.InProgress))
            .OrderBy(i => i.DueDate)
            .Take(Math.Clamp(maxResults, 1, 500))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActionComment>> ListCommentsAsync(Guid itemId, CancellationToken ct = default) =>
        await _dbContext.ActionComments
            .Where(c => c.ActionItemId == itemId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task AddCommentAsync(ActionComment comment, CancellationToken ct = default) =>
        await _dbContext.ActionComments.AddAsync(comment, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActionEvidence>> ListEvidenceAsync(Guid itemId, CancellationToken ct = default) =>
        await _dbContext.ActionEvidence
            .Where(e => e.ActionItemId == itemId)
            .OrderByDescending(e => e.UploadedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task AddEvidenceAsync(ActionEvidence evidence, CancellationToken ct = default) =>
        await _dbContext.ActionEvidence.AddAsync(evidence, ct);

    /// <inheritdoc/>
    public async Task<ActionEvidence?> GetEvidenceByIdAsync(Guid evidenceId, CancellationToken ct = default) =>
        await _dbContext.ActionEvidence.FirstOrDefaultAsync(e => e.Id == evidenceId, ct);

    /// <inheritdoc/>
    public void RemoveEvidence(ActionEvidence evidence) => evidence.IsDeleted = true;

    private IQueryable<ActionItem> BuildSearchQuery(ActionItemSearchRequest request, OrgScope scope, Guid currentUserId)
    {
        var query = _dbContext.ActionItems.AsNoTracking();

        if (request.Status is { } status)
            query = query.Where(i => i.Status == status);

        if (request.Priority is { } priority)
            query = query.Where(i => i.Priority == priority);

        if (request.PlanId is { } planId)
            query = query.Where(i => i.ActionPlanId == planId);

        if (request.SurveyId is { } surveyId)
            query = query.Where(i => i.ActionPlan != null && i.ActionPlan.SurveyId == surveyId);

        if (request.OverdueOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(i =>
                i.DueDate != null && i.DueDate < now
                && (i.Status == ActionItemStatus.Open || i.Status == ActionItemStatus.InProgress));
        }

        // --- مرز سازمانی (fail-closed) + استثناء‌ی آیتم‌های من -------------
        var hasOrgScope = scope.IsUnrestricted || !string.IsNullOrWhiteSpace(scope.VisiblePathPrefix);

        if (request.AssignedToMe)
        {
            // «کارهای من»: فقط آیتم‌های منتسب به کاربر جاری (مرز سازمانی اعمال نمی‌شود
            // چون انتصاب خودش یک اعطای دسترسی است).
            query = query.Where(i => i.AssigneeUserId == currentUserId);
        }
        else if (hasOrgScope)
        {
            if (scope.IsUnrestricted)
            {
                // دامنه‌ی شرکت: همه‌ی آیتم‌ها.
            }
            else
            {
                var prefix = scope.VisiblePathPrefix!;

                // آیتم داخل دامنه‌ی سازمانی کاربر، یا منتسب به خود او.
                query = query.Where(i =>
                    (i.ActionPlan != null
                        && i.ActionPlan.OrgUnitPath != null
                        && i.ActionPlan.OrgUnitPath.StartsWith(prefix))
                    || (i.AssigneeUserId == currentUserId && i.AssigneeUserId != null));
            }

            if (request.OrgUnitId is { } requestedUnit)
            {
                // فیلتر صریح کاربر: در دامنه‌ی شرکت همه‌ی واحدها قابل‌انتخاب است،
                // و در دامنه‌ی سازمانی فقط اگر داخل دامنه‌ی قابل‌مشاهده باشد.
                if (scope.IsUnrestricted)
                {
                    query = query.Where(i =>
                        i.ActionPlan != null && i.ActionPlan.OrgUnitId == requestedUnit);
                }
                else
                {
                    var prefix = scope.VisiblePathPrefix!;
                    query = query.Where(i =>
                        (i.ActionPlan != null && i.ActionPlan.OrgUnitId == requestedUnit
                            && i.ActionPlan.OrgUnitPath != null
                            && i.ActionPlan.OrgUnitPath.StartsWith(prefix)));
                }
            }
        }
        else
        {
            // هیچ دامنه‌ی سازمانی: فقط آیتم‌های منتسب به خود کاربر.
            query = query.Where(i => i.AssigneeUserId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(i =>
                i.Title.Contains(text) || (i.Description != null && i.Description.Contains(text)));
        }

        return query;
    }
}
