using Microsoft.EntityFrameworkCore;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Application.Modules.Reporting.Dtos;
using ODCC.Domain.Modules.Reporting.Entities;
using ODCC.Domain.Modules.Reporting.Enums;
using ODCC.Infrastructure.Modules.Reporting.Persistence;

namespace ODCC.Infrastructure.Modules.Reporting.Repositories;

/// <summary>
/// پیاده‌سازی مخزن تعاریف گزارش.
/// </summary>
public sealed class ReportDefinitionRepository(ReportingDbContext dbContext) : IReportDefinitionRepository
{
    private readonly ReportingDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ReportDefinition>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.ReportDefinitions
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<ReportDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.ReportDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.ReportDefinitions.CountAsync(ct);

    public async Task AddAsync(ReportDefinition entity, CancellationToken ct = default) =>
        await _dbContext.ReportDefinitions.AddAsync(entity, ct);

    public void Remove(ReportDefinition entity) => entity.IsDeleted = true;

    public void Update(ReportDefinition entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.ReportDefinitions.Update(entity);
    }

    /// <inheritdoc/>
    /// <param name="request">درخواست جستجوی تعاریف گزارش.</param>
    /// <param name="scope">
    /// دامنه‌ی سازمانی قابل‌مشاهده (fail-closed). <c>null</c> یعنی بدون محدودیت
    /// (فقط زمان کاربر Company، یا زمانی که فراخوان محدودیت را خودش اعمال کرده).
    /// </param>
    /// <param name="ct">توکن لغو.</param>
    public async Task<IReadOnlyList<ReportDefinition>> SearchAsync(
        ReportSearchRequest request, ODCC.Application.Authorization.OrgScope? scope = null, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request, scope);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        ReportSearchRequest request, ODCC.Application.Authorization.OrgScope? scope = null, CancellationToken ct = default) =>
        await BuildSearchQuery(request, scope).CountAsync(ct);

    /// <summary>
    /// شناسه‌ی تعاریف قابل‌مشاهده در دامنه‌ی سازمانی فعلی (fail-closed).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ListVisibleIdsAsync(ODCC.Application.Authorization.OrgScope scope, CancellationToken ct = default)
    {
        var query = _dbContext.ReportDefinitions.AsNoTracking();

        // دامنه‌ی Company نامحدود است؛ دامنه‌ی دارای لنگر فقط زیردرخت لنگر را
        // می‌بیند؛ دامنه‌ی Own/بدون لنگر معتبر هیچ چیزی.
        if (scope.IsUnrestricted)
        {
            return await query.Select(d => d.Id).ToListAsync(ct);
        }

        if (!scope.HasVisibleOrgScope)
        {
            return [];
        }

        var prefix = scope.VisiblePathPrefix!;
        return await query
            .Where(d => d.OrgUnitPath != null && d.OrgUnitPath.StartsWith(prefix))
            .Select(d => d.Id)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportDefinition>> ListDueAsync(DateTime asOf, CancellationToken ct = default) =>
        await _dbContext.ReportDefinitions
            .Where(d => d.Status == ReportStatus.Active
                && d.NextRunAt != null
                && d.NextRunAt <= asOf)
            .OrderBy(d => d.NextRunAt)
            .ToListAsync(ct);

    private IQueryable<ReportDefinition> BuildSearchQuery(ReportSearchRequest request, ODCC.Application.Authorization.OrgScope? scope = null)
    {
        // جستجوی تعاریف پیش‌فرض فقط غیربایگانی است (حذف نرم توسط HasQueryFilter
        // و فیلتر صریح وضعیت اعمال می‌شود).
        var query = _dbContext.ReportDefinitions.AsNoTracking();

        // مرز سازمانی (fail-closed): دامنه‌ی Company نامحدود است؛ دامنه‌ی دارای
        // لنگر فقط زیردرخت لنگر را می‌بیند؛ دامنه‌ی Own/بدون لنگر هیچ چیزی.
        if (scope is not null && !scope.IsUnrestricted)
        {
            if (!scope.HasVisibleOrgScope)
            {
                query = query.Where(d => false);
            }
            else
            {
                var prefix = scope.VisiblePathPrefix!;
                query = query.Where(d =>
                    d.OrgUnitPath != null && d.OrgUnitPath.StartsWith(prefix));
            }
        }

        if (request.Type is { } type)
            query = query.Where(d => d.Type == type);

        if (request.Status is { } status)
            query = query.Where(d => d.Status == status);

        // اگر یک وضعیت خاص (حتی Archived) به‌صراحت خواسته شده است، فیلتر
        // «بدون بایگانی» نباید آن را خنثی کند؛ وگرنه جستجوی گزارش‌های
        // بایگانی‌شده همیشه نتیجه‌ی خالی برمی‌گرداند.
        if (!request.IncludeArchived && !request.Status.HasValue)
            query = query.Where(d => d.Status != ReportStatus.Archived);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(d =>
                d.Name.Contains(text) ||
                (d.Description != null && d.Description.Contains(text)) ||
                (d.SurveyTitle != null && d.SurveyTitle.Contains(text)) ||
                (d.SurveyCode != null && d.SurveyCode.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن اجراهای گزارش.
/// </summary>
public sealed class ReportExecutionRepository(ReportingDbContext dbContext) : IReportExecutionRepository
{
    private readonly ReportingDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ReportExecution>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.ReportExecutions
            .AsNoTracking()
            .OrderByDescending(e => e.QueuedAt)
            .ToListAsync(ct);

    public async Task<ReportExecution?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.ReportExecutions.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.ReportExecutions.CountAsync(ct);

    public async Task AddAsync(ReportExecution entity, CancellationToken ct = default) =>
        await _dbContext.ReportExecutions.AddAsync(entity, ct);

    public void Remove(ReportExecution entity) => entity.IsDeleted = true;

    public void Update(ReportExecution entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.ReportExecutions.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportExecution>> SearchAsync(
        ExecutionSearchRequest request, IReadOnlyCollection<Guid>? visibleDefinitionIds = null, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request, visibleDefinitionIds);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(e => e.QueuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(
        ExecutionSearchRequest request, IReadOnlyCollection<Guid>? visibleDefinitionIds = null, CancellationToken ct = default) =>
        await BuildSearchQuery(request, visibleDefinitionIds).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportExecution>> ListByDefinitionAsync(
        Guid reportDefinitionId, int take, CancellationToken ct = default) =>
        await _dbContext.ReportExecutions
            .Where(e => e.ReportDefinitionId == reportDefinitionId)
            .OrderByDescending(e => e.QueuedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

    private IQueryable<ReportExecution> BuildSearchQuery(
        ExecutionSearchRequest request, IReadOnlyCollection<Guid>? visibleDefinitionIds = null)
    {
        var query = _dbContext.ReportExecutions.AsNoTracking();

        // مرز سازمانی (fail-closed): لیست خالی یعنی هیچ اجرایی قابل‌مشاهده نیست.
        // null یعنی کاربر دامنه‌ی Company دارد (نامحدود).
        if (visibleDefinitionIds is { Count: 0 })
        {
            query = query.Where(e => false);
        }
        else if (visibleDefinitionIds is { Count: > 0 } ids)
        {
            query = query.Where(e => ids.Contains(e.ReportDefinitionId));
        }

        if (request.ReportDefinitionId is { } definitionId)
            query = query.Where(e => e.ReportDefinitionId == definitionId);

        if (request.Status is { } status)
            query = query.Where(e => e.Status == status);

        return query;
    }
}
