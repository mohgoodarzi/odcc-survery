using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Domain.Modules.Analytics.Entities;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Response.Enums;
using ODCC.Infrastructure.Modules.Analytics.Persistence;
using SurveyMetricEntity = ODCC.Domain.Modules.Analytics.Entities.SurveyMetric;

namespace ODCC.Infrastructure.Modules.Analytics.Repositories;

/// <summary>
/// پیاده‌سازی مخزن عکس‌العمل‌های تحلیلی.
/// </summary>
public sealed class AnalyticsRepository(AnalyticsDbContext dbContext) : IAnalyticsRepository
{
    private readonly AnalyticsDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<SurveyMetricEntity>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.SurveyMetrics
            .AsNoTracking()
            .OrderByDescending(m => m.ComputedAt)
            .ToListAsync(ct);

    public async Task<SurveyMetricEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.SurveyMetrics.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.SurveyMetrics.CountAsync(ct);

    public async Task AddAsync(SurveyMetricEntity entity, CancellationToken ct = default) =>
        await _dbContext.SurveyMetrics.AddAsync(entity, ct);

    public void Remove(SurveyMetricEntity entity) => entity.IsDeleted = true;

    public void Update(SurveyMetricEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.SurveyMetrics.Update(entity);
    }

    /// <inheritdoc/>
    public Task<SurveyMetricEntity?> FindAsync(
        Guid surveyId,
        AnalyticsSegment segmentType,
        Guid? orgUnitId,
        Guid? campaignId,
        ResponseSource? source,
        CancellationToken ct = default) =>
        _dbContext.SurveyMetrics.FirstOrDefaultAsync(m =>
            m.SurveyId == surveyId
            && m.SegmentType == segmentType
            && m.OrgUnitId == orgUnitId
            && m.CampaignId == campaignId
            && m.Source == source, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SurveyMetricEntity>> SearchAsync(AnalyticsSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(m => m.ComputedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(AnalyticsSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SurveyMetricEntity>> ListBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        await _dbContext.SurveyMetrics
            .AsNoTracking()
            .Where(m => m.SurveyId == surveyId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task RemoveBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        await _dbContext.SurveyMetrics
            .Where(m => m.SurveyId == surveyId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsDeleted, true), ct);

    private IQueryable<SurveyMetricEntity> BuildSearchQuery(AnalyticsSearchRequest request)
    {
        var query = _dbContext.SurveyMetrics.AsNoTracking();

        if (request.SurveyId is { } surveyId)
            query = query.Where(m => m.SurveyId == surveyId);

        if (request.SegmentType is { } segment)
            query = query.Where(m => m.SegmentType == segment);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(m =>
                m.SurveyCode.Contains(text) ||
                m.SurveyTitle.Contains(text) ||
                (m.CampaignCode != null && m.CampaignCode.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن بنچمارک‌ها.
/// </summary>
public sealed class BenchmarkRepository(AnalyticsDbContext dbContext) : IBenchmarkRepository
{
    private readonly AnalyticsDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<Benchmark>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Benchmarks
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

    public async Task<Benchmark?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Benchmarks.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Benchmarks.CountAsync(ct);

    public async Task AddAsync(Benchmark entity, CancellationToken ct = default) =>
        await _dbContext.Benchmarks.AddAsync(entity, ct);

    public void Remove(Benchmark entity) => entity.IsDeleted = true;

    public void Update(Benchmark entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Benchmarks.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Benchmark>> SearchAsync(string? searchText, bool includeInactive, CancellationToken ct = default)
    {
        var query = _dbContext.Benchmarks.AsNoTracking();

        if (!includeInactive)
            query = query.Where(b => b.IsActive);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var text = searchText.Trim();
            query = query.Where(b => b.Name.Contains(text) || (b.Description != null && b.Description.Contains(text)));
        }

        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Benchmark>> GetApplicableAsync(
        MetricType metric, string? orgUnitPath, CancellationToken ct = default)
    {
        var query = _dbContext.Benchmarks
            .AsNoTracking()
            .Where(b => b.IsActive && b.Metric == metric);

        if (string.IsNullOrWhiteSpace(orgUnitPath))
        {
            // بدون مسیر سازمانی: فقط بنچمارک‌های سراسری شرکت.
            query = query.Where(b => b.IsCompanyWide);
        }
        else
        {
            // بنچمارک سراسری + بنچمارک‌های همین واحد یا اجداد آن
            // (زیردرخت‌ها وارث اهداف اجداد نیستند، برعکس: اجداد مالک اهداف زیردرخت‌ها هستند).
            query = query.Where(b =>
                b.IsCompanyWide ||
                (b.OrgUnitPath != null && orgUnitPath.StartsWith(b.OrgUnitPath)));
        }

        return await query.ToListAsync(ct);
    }
}
