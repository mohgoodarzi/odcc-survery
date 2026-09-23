using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Domain.Modules.Questionnaire.Entities;
using QuestionnaireEntity = ODCC.Domain.Modules.Questionnaire.Entities.Questionnaire;
using ODCC.Infrastructure.Modules.Questionnaire.Persistence;

namespace ODCC.Infrastructure.Modules.Questionnaire.Repositories;

/// <summary>
/// پیاده‌سازی مخزن پرسشنامه‌ها.
/// </summary>
public sealed class QuestionnaireRepository(QuestionnaireDbContext dbContext) : IQuestionnaireRepository
{
    private readonly QuestionnaireDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<QuestionnaireEntity>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Questionnaires
            .Include(q => q.Localizations)
            .Include(q => q.Sections).ThenInclude(s => s.Localizations)
            .AsNoTracking()
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);

    public async Task<QuestionnaireEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Questionnaires
            .Include(q => q.Localizations)
            .Include(q => q.Sections).ThenInclude(s => s.Localizations)
            .Include(q => q.Sections).ThenInclude(s => s.Items).ThenInclude(i => i.BranchingRules)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Questionnaires.CountAsync(ct);

    public async Task AddAsync(QuestionnaireEntity entity, CancellationToken ct = default) =>
        await _dbContext.Questionnaires.AddAsync(entity, ct);

    public void Remove(QuestionnaireEntity entity) => entity.IsDeleted = true;

    public void Update(QuestionnaireEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Questionnaires.Update(entity);
    }

    public Task<QuestionnaireEntity?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _dbContext.Questionnaires.FirstOrDefaultAsync(q => q.Code == code, ct);

    public async Task<IReadOnlyList<QuestionnaireEntity>> SearchAsync(QuestionnaireSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        return await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(QuestionnaireSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    private IQueryable<QuestionnaireEntity> BuildSearchQuery(QuestionnaireSearchRequest request)
    {
        var query = _dbContext.Questionnaires
            .Include(q => q.Localizations)
            .Include(q => q.Sections).ThenInclude(s => s.Items)
            .AsNoTracking();

        if (request.Status is { } status)
        {
            query = query.Where(q => q.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(q =>
                q.Code.Contains(text) ||
                q.Localizations.Any(l => l.Title.Contains(text)));
        }

        return query;
    }
}
