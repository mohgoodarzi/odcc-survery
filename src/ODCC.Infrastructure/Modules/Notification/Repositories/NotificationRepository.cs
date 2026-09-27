using NotificationEntity = ODCC.Domain.Modules.Notification.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Modules.Notification.Entities;
using ODCC.Domain.Modules.Notification.Enums;
using ODCC.Infrastructure.Modules.Notification.Persistence;

namespace ODCC.Infrastructure.Modules.Notification.Repositories;

/// <summary>
/// مخزن اعلان‌ها.
/// </summary>
public sealed class NotificationRepository(NotificationDbContext dbContext) : INotificationRepository
{
    private readonly NotificationDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<NotificationEntity>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.Notifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task<NotificationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    /// <summary>
    /// گرفتن اعلان برای تحویل: tracked (نه AsNoTracking) تا تغییر وضعیت مستقیم
    /// ذخیره شود. قفل سطری با RowVersion (optimistic concurrency) تضمین می‌کند
    /// که دو پردازش همزمان یک اعلان را دو بار نفرستند.
    /// </summary>
    public async Task<NotificationEntity?> GetForDeliveryAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Notifications.CountAsync(ct);

    public async Task AddAsync(NotificationEntity entity, CancellationToken ct = default) =>
        await _dbContext.Notifications.AddAsync(entity, ct);

    /// <summary>افزودن دسته‌ای از اعلان‌ها (برای حجم بالای دعوت‌نامه‌ها).</summary>
    public async Task AddRangeAsync(IReadOnlyCollection<NotificationEntity> entities, CancellationToken ct = default) =>
        await _dbContext.Notifications.AddRangeAsync(entities, ct);

    public void Remove(NotificationEntity entity) => entity.IsDeleted = true;

    public void Update(NotificationEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Notifications.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationEntity>> SearchAsync(NotificationSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(NotificationSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        await _dbContext.Notifications
            .Where(n => n.RecipientUserId == userId
                && n.Channel == NotificationChannel.InApp
                && n.ReadAt == null
                && n.Status != NotificationStatus.Failed
                && n.Status != NotificationStatus.Suppressed)
            .CountAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationEntity>> ListRecentInAppAsync(Guid userId, int take, CancellationToken ct = default) =>
        await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId && n.Channel == NotificationChannel.InApp)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> ListUnreadIdsAsync(Guid userId, NotificationChannel? channel, CancellationToken ct = default) =>
        await _dbContext.Notifications
            .Where(n => n.RecipientUserId == userId
                && n.ReadAt == null
                && n.Status != NotificationStatus.Failed
                && n.Status != NotificationStatus.Suppressed
                && (channel == null || n.Channel == channel))
            .Select(n => n.Id)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationEntity>> ListPendingAsync(int maxBatch, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // اعلان‌های در صف که زمان تلاششان رسیده است. مرتب بر اساس زمان ایجاد
        // تا پیام‌های قدیمی‌تر اول فرست شوند.
        return await _dbContext.Notifications
            .Where(n => n.Status == NotificationStatus.Pending
                && (n.NextTryAt == null || n.NextTryAt <= now))
            .OrderBy(n => n.CreatedAt)
            .Take(Math.Clamp(maxBatch, 1, 1000))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationEntity>> ListBySourceAsync(
        string sourceType, IReadOnlyCollection<Guid> sourceIds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceType) || sourceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Notifications
            .Where(n => n.SourceType == sourceType && sourceIds.Contains(n.SourceId!.Value))
            .ToListAsync(ct);
    }

    private IQueryable<NotificationEntity> BuildSearchQuery(NotificationSearchRequest request)
    {
        // فیلتر حذف نرم توسط HasQueryFilter اعمال می‌شود.
        var query = _dbContext.Notifications.AsNoTracking();

        if (request.RecipientUserId is { } userId)
            query = query.Where(n => n.RecipientUserId == userId);

        if (request.Channel is { } channel)
            query = query.Where(n => n.Channel == channel);

        if (request.Category is { } category)
            query = query.Where(n => n.Category == category);

        if (request.Status is { } status)
            query = query.Where(n => n.Status == status);

        if (request.UnreadOnly == true)
            query = query.Where(n => n.ReadAt == null && n.Channel == NotificationChannel.InApp);

        return query;
    }
}

/// <summary>مخزن قالب‌های اعلان.</summary>
public sealed class NotificationTemplateRepository(NotificationDbContext dbContext) : INotificationTemplateRepository
{
    private readonly NotificationDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<NotificationTemplate>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.NotificationTemplates
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.NotificationTemplates
            .Include(t => t.Localizations)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.NotificationTemplates.CountAsync(ct);

    public async Task AddAsync(NotificationTemplate entity, CancellationToken ct = default) =>
        await _dbContext.NotificationTemplates.AddAsync(entity, ct);

    public void Remove(NotificationTemplate entity) => entity.IsDeleted = true;

    public void Update(NotificationTemplate entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.NotificationTemplates.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<NotificationTemplate?> FindActiveByCodeAsync(string code, CancellationToken ct = default) =>
        await _dbContext.NotificationTemplates
            .Include(t => t.Localizations)
            .FirstOrDefaultAsync(t => t.Code == code && t.IsActive, ct);

    /// <inheritdoc/>
    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct = default) =>
        await _dbContext.NotificationTemplates
            .AnyAsync(t => t.Code == code && (excludingId == null || t.Id != excludingId), ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationTemplate>> SearchAsync(NotificationTemplateSearchRequest request, CancellationToken ct = default)
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

    /// <inheritdoc/>
    public async Task<int> CountAsync(NotificationTemplateSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    private IQueryable<NotificationTemplate> BuildSearchQuery(NotificationTemplateSearchRequest request)
    {
        var query = _dbContext.NotificationTemplates.AsNoTracking();

        if (request.Channel is { } channel)
            query = query.Where(t => t.Channel == channel);

        if (!request.IncludeArchived)
            query = query.Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(t => t.Code.Contains(text) || t.Name.Contains(text));
        }

        return query;
    }
}

/// <summary>مخزن ترجیحات تحویل.</summary>
public sealed class NotificationPreferenceRepository(NotificationDbContext dbContext) : INotificationPreferenceRepository
{
    private readonly NotificationDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<NotificationPreference>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.NotificationPreferences
            .AsNoTracking()
            .OrderBy(p => p.Channel)
            .ThenBy(p => p.Category)
            .ToListAsync(ct);

    public async Task<NotificationPreference?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.NotificationPreferences.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.NotificationPreferences.CountAsync(ct);

    public async Task AddAsync(NotificationPreference entity, CancellationToken ct = default) =>
        await _dbContext.NotificationPreferences.AddAsync(entity, ct);

    public void Remove(NotificationPreference entity) => entity.IsDeleted = true;

    public void Update(NotificationPreference entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.NotificationPreferences.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<NotificationPreference?> FindAsync(
        Guid userId, NotificationChannel channel, NotificationCategory? category, CancellationToken ct = default)
    {
        // ترجیح عمومی (دسته null) و ترجیح خاص دو ردیف متفاوت هستند؛
        // سرویس اولویت‌بندی می‌کند.
        return await _dbContext.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Channel == channel && p.Category == category, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationPreference>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        await _dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Channel)
            .ThenBy(p => p.Category)
            .ToListAsync(ct);
}
