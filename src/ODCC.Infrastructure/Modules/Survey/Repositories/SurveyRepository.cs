using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Modules.Survey.Entities;
using SurveyEntity = ODCC.Domain.Modules.Survey.Entities.Survey;
using SurveyTemplateEntity = ODCC.Domain.Modules.Survey.Entities.SurveyTemplate;
using ODCC.Infrastructure.Modules.Survey.Persistence;

namespace ODCC.Infrastructure.Modules.Survey.Repositories;

/// <summary>
/// پیاده‌سازی مخزن نظرسنجی‌ها.
/// </summary>
public sealed class SurveyRepository(SurveyDbContext dbContext) : ISurveyRepository
{
    private readonly SurveyDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<SurveyEntity>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Surveys
            .Include(s => s.Localizations)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<SurveyEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Surveys
            .Include(s => s.Localizations)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Surveys.CountAsync(ct);

    public async Task AddAsync(SurveyEntity entity, CancellationToken ct = default) =>
        await _dbContext.Surveys.AddAsync(entity, ct);

    public void Remove(SurveyEntity entity) => entity.IsDeleted = true;

    public void Update(SurveyEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Surveys.Update(entity);
    }

    public Task<SurveyEntity?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _dbContext.Surveys.FirstOrDefaultAsync(s => s.Code == code, ct);

    public async Task<IReadOnlyList<SurveyEntity>> SearchAsync(SurveySearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(SurveySearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    private IQueryable<SurveyEntity> BuildSearchQuery(SurveySearchRequest request)
    {
        var query = _dbContext.Surveys
            .Include(s => s.Localizations)
            .AsNoTracking();

        if (request.Status is { } status)
        {
            query = query.Where(s => s.Status == status);
        }

        if (request.QuestionnaireId is { } questionnaireId)
        {
            query = query.Where(s => s.QuestionnaireId == questionnaireId);
        }

        if (request.TemplateId is { } templateId)
        {
            query = query.Where(s => s.TemplateId == templateId);
        }

        if (!request.IncludeArchived)
        {
            query = query.Where(s => s.Status != ODCC.Domain.Modules.Survey.Enums.SurveyStatus.Archived);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(s =>
                s.Code.Contains(text) ||
                s.Localizations.Any(l => l.Title.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن قالب‌های نظرسنجی.
/// </summary>
public sealed class SurveyTemplateRepository(SurveyDbContext dbContext) : ISurveyTemplateRepository
{
    private readonly SurveyDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<SurveyTemplateEntity>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Templates
            .Include(t => t.Localizations)
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<SurveyTemplateEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Templates
            .Include(t => t.Localizations)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Templates.CountAsync(ct);

    public async Task AddAsync(SurveyTemplateEntity entity, CancellationToken ct = default) =>
        await _dbContext.Templates.AddAsync(entity, ct);

    public void Remove(SurveyTemplateEntity entity) => entity.IsDeleted = true;

    public void Update(SurveyTemplateEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Templates.Update(entity);
    }

    public Task<SurveyTemplateEntity?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _dbContext.Templates.FirstOrDefaultAsync(t => t.Code == code, ct);

    public async Task<IReadOnlyList<SurveyTemplateEntity>> SearchAsync(SurveyTemplateSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(SurveyTemplateSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    private IQueryable<SurveyTemplateEntity> BuildSearchQuery(SurveyTemplateSearchRequest request)
    {
        var query = _dbContext.Templates
            .Include(t => t.Localizations)
            .AsNoTracking();

        if (request.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(t =>
                t.Code.Contains(text) ||
                t.Localizations.Any(l => l.Title.Contains(text)));
        }

        return query;
    }
}
