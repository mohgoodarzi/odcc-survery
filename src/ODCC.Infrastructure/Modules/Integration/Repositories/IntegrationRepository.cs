using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;
using ODCC.Domain.Modules.Integration.Entities;
using ODCC.Domain.Modules.Integration.Enums;
using ODCC.Infrastructure.Modules.Integration.Persistence;

namespace ODCC.Infrastructure.Modules.Integration.Repositories;

/// <summary>
/// پیاده‌سازی مخزن اندپوینت‌های یکپارچه‌سازی.
/// </summary>
public sealed class IntegrationEndpointRepository(IntegrationDbContext dbContext) : IIntegrationEndpointRepository
{
    private readonly IntegrationDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<IntegrationEndpoint>> ListAsync(CancellationToken ct = default) =>
        await _dbContext.IntegrationEndpoints
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

    public async Task<IntegrationEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        // فیلتر سراسری حذف نرم نادیده گرفته می‌شود تا اندپوینت‌های بایگانی‌شده
        // قابل دسترسی باشند. سرویس وضعیت IsDeleted را خودش بررسی می‌کند و خطای
        // «integration_archived» را برمی‌گرداند. بدون این نادیده‌گرفتن، آن خطا
        // هرگز تولید نمی‌شد و جستجو با IncludeArchived همیشه خالی برمی‌گشت.
        await _dbContext.IntegrationEndpoints
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IntegrationEndpoint?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        await _dbContext.IntegrationEndpoints.FirstOrDefaultAsync(e => e.Code == code && !e.IsDeleted, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IntegrationEndpoint>> ListActiveByTypeAsync(IntegrationType type, CancellationToken ct = default) =>
        await _dbContext.IntegrationEndpoints
            .Where(e => e.Type == type && e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.IntegrationEndpoints.CountAsync(ct);

    public async Task AddAsync(IntegrationEndpoint entity, CancellationToken ct = default) =>
        await _dbContext.IntegrationEndpoints.AddAsync(entity, ct);

    public void Remove(IntegrationEndpoint entity) => entity.IsDeleted = true;

    public void Update(IntegrationEndpoint entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.IntegrationEndpoints.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IntegrationEndpoint>> SearchAsync(
        IntegrationEndpointSearchRequest request, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(IntegrationEndpointSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    private IQueryable<IntegrationEndpoint> BuildSearchQuery(IntegrationEndpointSearchRequest request)
    {
        // فیلتر سراسری حذف نرم نادیده گرفته می‌شود تا IncludeArchived بتواند
        // اندپوینت‌های بایگانی‌شده را نشان دهد. فیلتر «غیر بایگانی‌شده» در زیر
        // به‌صورت صریح و کنترل‌شده اعمال می‌شود.
        var query = _dbContext.IntegrationEndpoints
            .AsNoTracking()
            .IgnoreQueryFilters();

        if (request.Type is { } type)
        {
            query = query.Where(e => e.Type == type);
        }

        if (request.IsActive is { } isActive)
        {
            query = query.Where(e => e.IsActive == isActive);
        }

        if (!request.IncludeArchived)
        {
            query = query.Where(e => !e.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(e =>
                e.Name.Contains(text) ||
                e.Code.Contains(text) ||
                (e.Url != null && e.Url.Contains(text)) ||
                (e.Description != null && e.Description.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن تحویل‌های وب‌هوک.
/// </summary>
public sealed class WebhookDeliveryRepository(IntegrationDbContext dbContext) : IWebhookDeliveryRepository
{
    private readonly IntegrationDbContext _dbContext = dbContext;

    public async Task<WebhookDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.WebhookDeliveries.FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc/>
    public async Task<WebhookDelivery?> FindByEndpointAndEventIdAsync(Guid endpointId, string eventId, CancellationToken ct = default) =>
        await _dbContext.WebhookDeliveries
            .FirstOrDefaultAsync(d => d.EndpointId == endpointId && d.EventId == eventId, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(WebhookDelivery Delivery, IntegrationEndpoint Endpoint)>> ListRetryableWithEndpointsAsync(
        DateTime asOf, int maxResults, CancellationToken ct = default)
    {
        var deliveries = await _dbContext.WebhookDeliveries
            .Where(d => d.Status == DeliveryStatus.Pending
                && d.NextAttemptAt != null && d.NextAttemptAt <= asOf)
            .OrderBy(d => d.NextAttemptAt)
            .Take(Math.Clamp(maxResults, 1, 500))
            .ToListAsync(ct);

        if (deliveries.Count == 0)
        {
            return [];
        }

        var endpointIds = deliveries.Select(d => d.EndpointId).Distinct().ToList();
        var endpoints = await _dbContext.IntegrationEndpoints
            .Where(e => endpointIds.Contains(e.Id) && e.IsActive && !e.IsDeleted)
            .ToDictionaryAsync(e => e.Id, ct);

        return deliveries
            .Where(d => endpoints.ContainsKey(d.EndpointId))
            .Select(d => (d, endpoints[d.EndpointId]))
            .ToList();
    }

    public async Task AddAsync(WebhookDelivery entity, CancellationToken ct = default) =>
        await _dbContext.WebhookDeliveries.AddAsync(entity, ct);

    public void Update(WebhookDelivery entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.WebhookDeliveries.Update(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookDelivery>> SearchAsync(
        WebhookDeliverySearchRequest request, CancellationToken ct = default)
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
    public async Task<int> CountAsync(WebhookDeliverySearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookDelivery>> ListRetryableAsync(DateTime asOf, int maxResults, CancellationToken ct = default) =>
        await _dbContext.WebhookDeliveries
            .Where(d => d.Status == DeliveryStatus.Pending
                && d.NextAttemptAt != null && d.NextAttemptAt <= asOf)
            .OrderBy(d => d.NextAttemptAt)
            .Take(Math.Clamp(maxResults, 1, 500))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountPendingAsync(CancellationToken ct = default) =>
        await _dbContext.WebhookDeliveries.CountAsync(d => d.Status == DeliveryStatus.Pending, ct);

    /// <inheritdoc/>
    public async Task<int> CountSucceededAsync(CancellationToken ct = default) =>
        await _dbContext.WebhookDeliveries.CountAsync(d => d.Status == DeliveryStatus.Succeeded, ct);

    private IQueryable<WebhookDelivery> BuildSearchQuery(WebhookDeliverySearchRequest request)
    {
        var query = _dbContext.WebhookDeliveries.AsNoTracking();

        if (request.Status is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        if (request.EndpointId is { } endpointId)
        {
            query = query.Where(d => d.EndpointId == endpointId);
        }

        if (request.RetryableOnly)
        {
            query = query.Where(d => d.Status == DeliveryStatus.Pending && d.NextAttemptAt != null);
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            query = query.Where(d => d.EventType == request.EventType);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(d =>
                d.EndpointCode.Contains(text) ||
                d.EventType.Contains(text) ||
                d.EventId.Contains(text) ||
                (d.LastError != null && d.LastError.Contains(text)));
        }

        return query;
    }
}
