using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Domain.Modules.QuestionBank.Entities;
using ODCC.Infrastructure.Modules.QuestionBank.Persistence;

namespace ODCC.Infrastructure.Modules.QuestionBank.Repositories;

/// <summary>
/// پیاده‌سازی مخزن سؤال‌های کتابخانه.
/// </summary>
public sealed class QuestionRepository(QuestionBankDbContext dbContext) : IQuestionRepository
{
    private readonly QuestionBankDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<Question>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Questions
            .Include(q => q.Localizations)
            .Include(q => q.Options).ThenInclude(o => o.Localizations)
            .Include(q => q.Tags)
            .AsNoTracking()
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);

    public async Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Questions
            .Include(q => q.Localizations)
            .Include(q => q.Options).ThenInclude(o => o.Localizations)
            .Include(q => q.Tags)
            .Include(q => q.Versions)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Questions.CountAsync(ct);

    public async Task AddAsync(Question entity, CancellationToken ct = default) =>
        await _dbContext.Questions.AddAsync(entity, ct);

    public void Remove(Question entity) => entity.IsDeleted = true;

    public void Update(Question entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Questions.Update(entity);
    }

    public Task<Question?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _dbContext.Questions.FirstOrDefaultAsync(q => q.Code == code, ct);

    public async Task<IReadOnlyList<Question>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var distinctIds = ids.Distinct().ToList();

        return await _dbContext.Questions
            .Include(q => q.Localizations)
            .Include(q => q.Options).ThenInclude(o => o.Localizations)
            .Include(q => q.Versions)
            .Where(q => distinctIds.Contains(q.Id))
            .ToListAsync(ct);
    }

    public Task<int> CountVersionsAsync(Guid questionId, CancellationToken ct = default) =>
        _dbContext.QuestionVersions.CountAsync(v => v.QuestionId == questionId, ct);

    public async Task<int> GetLatestVersionNumberAsync(Guid questionId, CancellationToken ct = default)
    {
        var versions = await _dbContext.QuestionVersions
            .AsNoTracking()
            .Where(v => v.QuestionId == questionId)
            .Select(v => (int?)v.VersionNumber)
            .ToListAsync(ct);

        return versions.Max() ?? 0;
    }

    public async Task<IReadOnlyList<QuestionVersion>> GetVersionsAsync(Guid questionId, CancellationToken ct = default) =>
        await _dbContext.QuestionVersions
            .AsNoTracking()
            .Where(v => v.QuestionId == questionId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
}
