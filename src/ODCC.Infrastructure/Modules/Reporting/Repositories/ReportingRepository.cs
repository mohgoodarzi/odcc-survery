using Microsoft.EntityFrameworkCore;
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
    public async Task<IReadOnlyList<ReportDefinition>> SearchAsync(
        ReportSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(ReportSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportDefinition>> ListDueAsync(DateTime asOf, CancellationToken ct = default) =>
        await _dbContext.ReportDefinitions
            .Where(d => d.Status == ReportStatus.Active
                && d.NextRunAt != null
                && d.NextRunAt <= asOf)
            .OrderBy(d => d.NextRunAt)
            .ToListAsync(ct);

    private IQueryable<ReportDefinition> BuildSearchQuery(ReportSearchRequest request)
    {
        // جستجوی تعاریف پیش‌فرض فقط غیربایگانی است (حذف نرم توسط HasQueryFilter
        // و فیلتر صریح وضعیت اعمال می‌شود).
        var query = _dbContext.ReportDefinitions.AsNoTracking();

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
        ExecutionSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(e => e.QueuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(ExecutionSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportExecution>> ListByDefinitionAsync(
        Guid reportDefinitionId, int take, CancellationToken ct = default) =>
        await _dbContext.ReportExecutions
            .Where(e => e.ReportDefinitionId == reportDefinitionId)
            .OrderByDescending(e => e.QueuedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

    private IQueryable<ReportExecution> BuildSearchQuery(ExecutionSearchRequest request)
    {
        var query = _dbContext.ReportExecutions.AsNoTracking();

        if (request.ReportDefinitionId is { } definitionId)
            query = query.Where(e => e.ReportDefinitionId == definitionId);

        if (request.Status is { } status)
            query = query.Where(e => e.Status == status);

        return query;
    }
}
