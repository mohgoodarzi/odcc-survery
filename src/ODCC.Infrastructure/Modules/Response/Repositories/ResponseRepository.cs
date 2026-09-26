using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Application.Modules.Response.Dtos;
using ODCC.Domain.Modules.Response.Entities;
using ODCC.Domain.Modules.Response.Enums;
using ResponseSessionEntity = ODCC.Domain.Modules.Response.Entities.ResponseSession;
using ODCC.Infrastructure.Modules.Response.Persistence;

namespace ODCC.Infrastructure.Modules.Response.Repositories;

/// <summary>
/// پیاده‌سازی مخزن نشست‌های پاسخ.
/// </summary>
public sealed class ResponseRepository(ResponseDbContext dbContext) : IResponseRepository
{
    private readonly ResponseDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ResponseSessionEntity>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Sessions
            .Include(s => s.Answers)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<ResponseSessionEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Sessions
            .Include(s => s.Answers)
            .ThenInclude(a => a.Selections)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Sessions.CountAsync(ct);

    public async Task AddAsync(ResponseSessionEntity entity, CancellationToken ct = default) =>
        await _dbContext.Sessions.AddAsync(entity, ct);

    public void Remove(ResponseSessionEntity entity) => entity.IsDeleted = true;

    public void Update(ResponseSessionEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Sessions.Update(entity);
    }

    public async Task<IReadOnlyList<ResponseSessionEntity>> SearchAsync(ResponseSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(s => s.SubmittedAt ?? s.LastActivityAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(ResponseSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    public Task<ResponseSessionEntity?> GetByIdWithAnswersAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.Sessions
            .Include(s => s.Answers)
            .ThenInclude(a => a.Selections)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<ResponseSessionEntity?> FindInProgressAsync(Guid surveyId, Guid respondentUserId, CancellationToken ct = default) =>
        _dbContext.Sessions
            .Include(s => s.Answers)
            .ThenInclude(a => a.Selections)
            .FirstOrDefaultAsync(s =>
                s.SurveyId == surveyId
                && s.RespondentUserId == respondentUserId
                && s.Status == ResponseStatus.InProgress, ct);

    public Task<ResponseSessionEntity?> FindSubmittedAsync(Guid surveyId, Guid respondentUserId, CancellationToken ct = default) =>
        _dbContext.Sessions
            .Include(s => s.Answers)
            .ThenInclude(a => a.Selections)
            .FirstOrDefaultAsync(s =>
                s.SurveyId == surveyId
                && s.RespondentUserId == respondentUserId
                && s.Status == ResponseStatus.Submitted, ct);

    public async Task<IReadOnlyList<ResponseSessionEntity>> ListByRespondentAsync(Guid respondentUserId, CancellationToken ct = default) =>
        await _dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.RespondentUserId == respondentUserId)
            .ToListAsync(ct);

    public async Task<int> CountSubmittedBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        await _dbContext.Sessions.CountAsync(s =>
            s.SurveyId == surveyId && s.Status == ResponseStatus.Submitted, ct);

    /// <summary>
    /// همه‌ی نشست‌های ارسال‌شده‌ی یک نظرسنجی به‌همراه پاسخ‌ها و گزینه‌های انتخابی.
    /// فقط برای محاسبه‌ی تجمع‌ها در ماژول تحلیلات (از طریق قرارداد).
    /// </summary>
    public async Task<IReadOnlyList<ResponseSessionEntity>> ListSubmittedBySurveyAsync(
        Guid surveyId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        var query = _dbContext.Sessions
            .Include(s => s.Answers)
            .ThenInclude(a => a.Selections)
            .AsNoTracking()
            .Where(s => s.SurveyId == surveyId && s.Status == ResponseStatus.Submitted);

        if (fromUtc.HasValue)
            query = query.Where(s => s.SubmittedAt >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(s => s.SubmittedAt <= toUtc.Value);

        return await query.ToListAsync(ct);
    }

    /// <summary>
    /// همه‌ی نشست‌های یک نظرسنجی (شامل در حال تکمیل و ارسال‌شده) — بدون پاسخ‌ها.
    /// فقط برای محاسبه‌ی مخرج نرخ تکمیل در ماژول تحلیلات (از طریق قرارداد):
    /// نشست‌هایی که شروع شده‌اند ولی هنوز ارسال نشده‌اند، افت پاسخ کامل را
    /// نشان می‌دهند. فیلتر زمانی بر اساس شروع نشست است.
    /// </summary>
    public async Task<IReadOnlyList<ResponseSessionEntity>> ListAllBySurveyAsync(
        Guid surveyId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        var query = _dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.SurveyId == surveyId);

        if (fromUtc.HasValue)
            query = query.Where(s => s.StartedAt >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(s => s.StartedAt <= toUtc.Value);

        return await query.ToListAsync(ct);
    }

    /// <summary>
    /// خلاصه‌ی نشست‌های ارسال‌شده‌ی چند نظرسنجی — فقط فیلدهای تجمعی، بدون
    /// جابه‌جایی پاسخ‌های خام (projection سمت پایگاه داده).
    /// </summary>
    public async Task<IReadOnlyList<SubmittedSessionSummary>> ListSubmittedAsync(
        IReadOnlyCollection<Guid> surveyIds, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        if (surveyIds.Count == 0)
            return [];

        var query = _dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Status == ResponseStatus.Submitted && surveyIds.Contains(s.SurveyId));

        if (fromUtc.HasValue)
            query = query.Where(s => s.SubmittedAt >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(s => s.SubmittedAt <= toUtc.Value);

        return await query
            .Select(s => new SubmittedSessionSummary
            {
                SessionId = s.Id,
                SurveyId = s.SurveyId,
                CampaignId = s.CampaignId,
                SubmittedAt = s.SubmittedAt!.Value,
                StartedAt = s.StartedAt,
                RespondentEmployeeId = s.RespondentEmployeeId,
                Source = s.Source,
                AnswerCount = s.AnswerCount
            })
            .ToListAsync(ct);
    }

    private IQueryable<ResponseSessionEntity> BuildSearchQuery(ResponseSearchRequest request)
    {
        var query = _dbContext.Sessions
            .Include(s => s.Answers)
            .AsNoTracking();

        if (request.SurveyId is { } surveyId)
        {
            query = query.Where(s => s.SurveyId == surveyId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(s =>
                s.SurveyCode.Contains(text) ||
                (s.CampaignCode != null && s.CampaignCode.Contains(text)) ||
                (s.RespondentDisplayName != null && s.RespondentDisplayName.Contains(text)));
        }

        return query;
    }
}
