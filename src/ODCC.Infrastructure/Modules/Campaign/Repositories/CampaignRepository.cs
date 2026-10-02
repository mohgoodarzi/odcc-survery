using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Domain.Modules.Campaign.Entities;
using ODCC.Domain.Modules.Campaign.Enums;
using CampaignEntity = ODCC.Domain.Modules.Campaign.Entities.Campaign;
using ODCC.Infrastructure.Modules.Campaign.Persistence;

namespace ODCC.Infrastructure.Modules.Campaign.Repositories;

/// <summary>
/// پیاده‌سازی مخزن کمپین‌ها.
/// </summary>
public sealed class CampaignRepository(CampaignDbContext dbContext) : ICampaignRepository
{
    private readonly CampaignDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<CampaignEntity>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Campaigns
            .Include(c => c.Localizations)
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// بارگذاری یک کمپین با تمام مجموعه‌های کوچک (ترجمه‌ها، اهداف، یادآورها).
    /// ردیف‌های توزیع عمداً بارگذاری نمی‌شوند چون می‌توانند زیاد باشند.
    /// </summary>
    public async Task<CampaignEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Campaigns
            .Include(c => c.Localizations)
            .Include(c => c.TargetUnits)
            .Include(c => c.TargetMembers)
            .Include(c => c.Reminders).ThenInclude(r => r.Localizations)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Campaigns.CountAsync(ct);

    public async Task AddAsync(CampaignEntity entity, CancellationToken ct = default) =>
        await _dbContext.Campaigns.AddAsync(entity, ct);

    public void Remove(CampaignEntity entity) => entity.IsDeleted = true;

    public void Update(CampaignEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Campaigns.Update(entity);
    }

    public Task<CampaignEntity?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _dbContext.Campaigns.FirstOrDefaultAsync(c => c.Code == code, ct);

    public async Task<IReadOnlyList<CampaignEntity>> SearchAsync(CampaignSearchRequest request, CancellationToken ct = default) =>
        await SearchAsync(request, scope: null, ct);

    /// <summary>
    /// جستجوی کمپین‌ها در دامنه‌ی سازمانی قابل‌مشاهده (fail-closed).
    /// </summary>
    /// <param name="request">درخواست جستجو.</param>
    /// <param name="scope">
    /// دامنه‌ی سازمانی قابل‌مشاهده (fail-closed). <c>null</c> یعنی بدون محدودیت.
    /// </param>
    /// <param name="ct">توکن لغو.</param>
    public async Task<IReadOnlyList<CampaignEntity>> SearchAsync(
        CampaignSearchRequest request, ODCC.Application.Authorization.OrgScope? scope, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request, scope);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(CampaignSearchRequest request, CancellationToken ct = default) =>
        await CountAsync(request, scope: null, ct);

    public async Task<int> CountAsync(
        CampaignSearchRequest request, ODCC.Application.Authorization.OrgScope? scope, CancellationToken ct = default) =>
        await BuildSearchQuery(request, scope).CountAsync(ct);

    /// <summary>کمپین‌های «در حال اجرا» که حداقل یک یادآور سررسیده دارند (برای پردازش دوره‌ای).</summary>
    public async Task<IReadOnlyList<CampaignEntity>> GetDueForRemindersAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await _dbContext.Campaigns
            .Include(c => c.Reminders)
            .Where(c => c.Status == CampaignStatus.Running
                        && c.Reminders.Any(r => r.Status == ReminderStatus.Scheduled && r.SendAt <= now))
            .ToListAsync(ct);
    }

    private IQueryable<CampaignEntity> BuildSearchQuery(
        CampaignSearchRequest request, ODCC.Application.Authorization.OrgScope? scope = null)
    {
        var query = _dbContext.Campaigns
            .Include(c => c.Localizations)
            .AsNoTracking();

        // مرز سازمانی (fail-closed): دامنه‌ی Company نامحدود؛ دامنه‌ی دارای
        // لنگر فقط زیردرخت لنگر را می‌بیند؛ دامنه‌ی Own/بدون لنگر هیچ چیزی.
        // کمپین‌های بدون مالک سازمانی (قبل از این رفتار) فقط Company می‌بیند.
        if (scope is not null && !scope.IsUnrestricted)
        {
            if (!scope.HasVisibleOrgScope)
            {
                query = query.Where(c => false);
            }
            else
            {
                var prefix = scope.VisiblePathPrefix!;
                query = query.Where(c => c.OrgUnitPath != null && c.OrgUnitPath.StartsWith(prefix));
            }
        }

        if (request.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }
        else if (!request.IncludeArchived)
        {
            query = query.Where(c => c.Status != CampaignStatus.Archived);
        }

        if (request.SurveyId is { } surveyId)
        {
            query = query.Where(c => c.SurveyId == surveyId);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(c =>
                c.Code.Contains(text) ||
                c.Localizations.Any(l => l.Title.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن ردیف‌های توزیع.
/// </summary>
public sealed class DistributionRepository(CampaignDbContext dbContext) : IDistributionRepository
{
    private readonly CampaignDbContext _dbContext = dbContext;

    public Task<Distribution?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.Distributions.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddRangeAsync(IReadOnlyCollection<Distribution> distributions, CancellationToken ct = default) =>
        await _dbContext.Distributions.AddRangeAsync(distributions, ct);

    public Task<int> CountByCampaignAsync(Guid campaignId, CancellationToken ct = default) =>
        _dbContext.Distributions.CountAsync(d => d.CampaignId == campaignId, ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountByCampaignsAsync(IReadOnlyCollection<Guid> campaignIds, CancellationToken ct = default) =>
        await _dbContext.Distributions
            .Where(d => campaignIds.Contains(d.CampaignId))
            .GroupBy(d => d.CampaignId)
            .Select(group => new { CampaignId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.CampaignId, x => x.Count, ct);

    public Task<int> CountByStatusAsync(Guid campaignId, DistributionStatus status, CancellationToken ct = default) =>
        _dbContext.Distributions.CountAsync(d => d.CampaignId == campaignId && d.Status == status, ct);

    public async Task<IReadOnlyList<Distribution>> SearchByCampaignAsync(Guid campaignId, DistributionSearchRequest request, CancellationToken ct = default)
    {
        var query = _dbContext.Distributions.AsNoTracking().Where(d => d.CampaignId == campaignId);

        if (request.Status is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Distribution>> ListByCampaignAsync(Guid campaignId, CancellationToken ct = default) =>
        await _dbContext.Distributions.AsNoTracking().Where(d => d.CampaignId == campaignId).ToListAsync(ct);

    /// <summary>ردیف‌های توزیعی که هنوز پاسخ نداده‌اند (در صف یا ارسال‌شده) — برای یادآورها.</summary>
    public async Task<IReadOnlyList<Distribution>> GetRemindableAsync(Guid campaignId, CancellationToken ct = default) =>
        await _dbContext.Distributions
            .Where(d => d.CampaignId == campaignId
                        && (d.Status == DistributionStatus.Pending || d.Status == DistributionStatus.Sent))
            .ToListAsync(ct);

    /// <summary>
    /// دعوت‌نامه‌های باز یک کارمند (ارسال‌شده ولی هنوز پاسخ‌داده‌نشده) به‌همراه
    /// کد و کانال کمپین. این متد فقط برای خواندن توسط ماژول پاسخ‌هاست و DTO
    /// برمی‌گرداند تا مرز ماژول‌ها حفظ شود.
    /// </summary>
    public async Task<IReadOnlyList<OpenDistributionDto>> GetOpenByEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var rows = await (
            from distribution in _dbContext.Distributions.AsNoTracking()
            join campaign in _dbContext.Campaigns.AsNoTracking() on distribution.CampaignId equals campaign.Id
            where distribution.EmployeeId == employeeId && distribution.Status == DistributionStatus.Sent
            select new
            {
                distribution.Id,
                distribution.CampaignId,
                CampaignCode = campaign.Code,
                Channel = campaign.Channel,
                campaign.SurveyId,
                distribution.EmployeeId
            }).ToListAsync(ct);

        return rows.ConvertAll(r => new OpenDistributionDto
        {
            Id = r.Id,
            CampaignId = r.CampaignId,
            CampaignCode = r.CampaignCode,
            Channel = r.Channel,
            SurveyId = r.SurveyId,
            EmployeeId = r.EmployeeId
        });
    }

    public void Update(Distribution distribution)
    {
        distribution.UpdatedAt = DateTime.UtcNow;
        _dbContext.Distributions.Update(distribution);
    }
}
