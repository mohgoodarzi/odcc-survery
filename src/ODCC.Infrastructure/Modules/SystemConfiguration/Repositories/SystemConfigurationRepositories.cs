using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.SystemConfiguration.Abstractions;
using ODCC.Application.Modules.SystemConfiguration.Dtos;
using ODCC.Domain.Modules.SystemConfiguration.Entities;
using ODCC.Domain.Modules.SystemConfiguration.Enums;
using ODCC.Infrastructure.Modules.SystemConfiguration.Persistence;

namespace ODCC.Infrastructure.Modules.SystemConfiguration.Repositories;

/// <summary>
/// پیاده‌سازی مخزن تنظیمات سامانه.
/// </summary>
public sealed class SettingRepository(SystemConfigurationDbContext dbContext) : ISettingRepository
{
    private readonly SystemConfigurationDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<Setting?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await _dbContext.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Setting>> SearchAsync(SettingSearchRequest request, CancellationToken ct = default)
    {
        return await BuildSearchQuery(request)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Skip((Math.Max(request.Page, 1) - 1) * Math.Clamp(request.PageSize, 1, 200))
            .Take(Math.Clamp(request.PageSize, 1, 200))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(SettingSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.Settings.CountAsync(ct);

    /// <inheritdoc/>
    public async Task AddAsync(Setting entity, CancellationToken ct = default) =>
        await _dbContext.Settings.AddAsync(entity, ct);

    /// <inheritdoc/>
    public void Update(Setting entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.Settings.Update(entity);
    }

    private IQueryable<Setting> BuildSearchQuery(SettingSearchRequest request)
    {
        var query = _dbContext.Settings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Group))
        {
            query = query.Where(s => s.Group == request.Group);
        }

        if (request.ValueType is { } valueType)
        {
            query = query.Where(s => s.ValueType == valueType);
        }

        if (request.Scope is { } scope)
        {
            query = query.Where(s => s.Scope == scope);
        }

        if (request.IsSensitive is { } isSensitive)
        {
            query = query.Where(s => s.IsSensitive == isSensitive);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(s =>
                s.Key.Contains(text) ||
                s.Name.Contains(text) ||
                (s.Group != null && s.Group.Contains(text)) ||
                (s.Description != null && s.Description.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن پرچم‌های ویژگی.
/// </summary>
public sealed class FeatureFlagRepository(SystemConfigurationDbContext dbContext) : IFeatureFlagRepository
{
    private readonly SystemConfigurationDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await _dbContext.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FeatureFlag>> SearchAsync(FeatureFlagSearchRequest request, CancellationToken ct = default)
    {
        return await BuildSearchQuery(request)
            .OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt)
            .Skip((Math.Max(request.Page, 1) - 1) * Math.Clamp(request.PageSize, 1, 200))
            .Take(Math.Clamp(request.PageSize, 1, 200))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(FeatureFlagSearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.FeatureFlags.CountAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountEnabledAsync(CancellationToken ct = default) =>
        await _dbContext.FeatureFlags.CountAsync(f =>
            f.State == FeatureFlagState.On &&
            (f.ExpiresAt == null || f.ExpiresAt > DateTime.UtcNow), ct);

    /// <inheritdoc/>
    public async Task AddAsync(FeatureFlag entity, CancellationToken ct = default) =>
        await _dbContext.FeatureFlags.AddAsync(entity, ct);

    /// <inheritdoc/>
    public void Update(FeatureFlag entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.FeatureFlags.Update(entity);
    }

    private IQueryable<FeatureFlag> BuildSearchQuery(FeatureFlagSearchRequest request)
    {
        var query = _dbContext.FeatureFlags.AsNoTracking();

        if (request.State is { } state)
        {
            query = query.Where(f => f.State == state);
        }

        if (request.Scope is { } scope)
        {
            query = query.Where(f => f.Scope == scope);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(f =>
                f.Key.Contains(text) ||
                f.Name.Contains(text) ||
                (f.Description != null && f.Description.Contains(text)));
        }

        return query;
    }
}

/// <summary>
/// پیاده‌سازی مخزن سیاست‌های سیستمی.
/// </summary>
public sealed class SystemPolicyRepository(SystemConfigurationDbContext dbContext) : ISystemPolicyRepository
{
    private readonly SystemConfigurationDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<SystemPolicy?> GetByTypeAndKeyAsync(SystemPolicyType type, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await _dbContext.SystemPolicies.FirstOrDefaultAsync(p => p.Type == type && p.Key == key, ct);
    }

    /// <inheritdoc/>
    public async Task<SystemPolicy?> GetEnabledByTypeAndKeyAsync(SystemPolicyType type, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await _dbContext.SystemPolicies.FirstOrDefaultAsync(p => p.Type == type && p.Key == key && p.IsEnabled, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SystemPolicy>> SearchAsync(SystemPolicySearchRequest request, CancellationToken ct = default)
    {
        return await BuildSearchQuery(request)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Key)
            .Skip((Math.Max(request.Page, 1) - 1) * Math.Clamp(request.PageSize, 1, 200))
            .Take(Math.Clamp(request.PageSize, 1, 200))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(SystemPolicySearchRequest request, CancellationToken ct = default) =>
        await BuildSearchQuery(request).CountAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _dbContext.SystemPolicies.CountAsync(ct);

    /// <inheritdoc/>
    public async Task AddAsync(SystemPolicy entity, CancellationToken ct = default) =>
        await _dbContext.SystemPolicies.AddAsync(entity, ct);

    /// <inheritdoc/>
    public void Update(SystemPolicy entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.SystemPolicies.Update(entity);
    }

    private IQueryable<SystemPolicy> BuildSearchQuery(SystemPolicySearchRequest request)
    {
        var query = _dbContext.SystemPolicies.AsNoTracking();

        if (request.Type is { } type)
        {
            query = query.Where(p => p.Type == type);
        }

        if (request.IsEnabled is { } isEnabled)
        {
            query = query.Where(p => p.IsEnabled == isEnabled);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(p =>
                p.Key.Contains(text) ||
                p.Name.Contains(text) ||
                (p.Description != null && p.Description.Contains(text)));
        }

        return query;
    }
}
