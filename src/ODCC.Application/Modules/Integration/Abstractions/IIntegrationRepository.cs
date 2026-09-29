using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Integration.Entities;

namespace ODCC.Application.Modules.Integration.Abstractions;

/// <summary>
/// مخزن اندپوینت‌های یکپارچه‌سازی.
/// </summary>
public interface IIntegrationEndpointRepository
{
    Task<IReadOnlyList<IntegrationEndpoint>> ListAsync(CancellationToken ct = default);

    Task<IntegrationEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>یافتن اندپوینت (غیر بایگانی‌شده) بر اساس کد یکتا.</summary>
    Task<IntegrationEndpoint?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>فهرست اندپوینت‌های فعال از یک نوع خاص.</summary>
    Task<IReadOnlyList<IntegrationEndpoint>> ListActiveByTypeAsync(Domain.Modules.Integration.Enums.IntegrationType type, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    Task AddAsync(IntegrationEndpoint entity, CancellationToken ct = default);

    void Remove(IntegrationEndpoint entity);

    void Update(IntegrationEndpoint entity);

    Task<IReadOnlyList<IntegrationEndpoint>> SearchAsync(IntegrationEndpointSearchRequest request, CancellationToken ct = default);

    Task<int> CountAsync(IntegrationEndpointSearchRequest request, CancellationToken ct = default);
}

/// <summary>
/// مخزن تحویل‌های وب‌هوک.
/// </summary>
public interface IWebhookDeliveryRepository
{
    Task<WebhookDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>یافتن تحویل یک رویداد خاص برای یک اندپوینت (idempotency).</summary>
    Task<WebhookDelivery?> FindByEndpointAndEventIdAsync(Guid endpointId, string eventId, CancellationToken ct = default);

    /// <summary>تحویل‌های در انتظار به همراه اندپوینت مقصد (برای تلاش مجدد).</summary>
    Task<IReadOnlyList<(WebhookDelivery Delivery, IntegrationEndpoint Endpoint)>> ListRetryableWithEndpointsAsync(
        DateTime asOf, int maxResults, CancellationToken ct = default);

    Task AddAsync(WebhookDelivery entity, CancellationToken ct = default);

    void Update(WebhookDelivery entity);

    Task<IReadOnlyList<WebhookDelivery>> SearchAsync(WebhookDeliverySearchRequest request, CancellationToken ct = default);

    Task<int> CountAsync(WebhookDeliverySearchRequest request, CancellationToken ct = default);

    /// <summary>تحویل‌های در انتظارِ آماده‌ی تلاش مجدد (سررسیده‌شده).</summary>
    Task<IReadOnlyList<WebhookDelivery>> ListRetryableAsync(DateTime asOf, int maxResults, CancellationToken ct = default);

    /// <summary>تعداد تحویل‌های در انتظار.</summary>
    Task<int> CountPendingAsync(CancellationToken ct = default);

    /// <summary>تعداد کل تحویل‌های موفق.</summary>
    Task<int> CountSucceededAsync(CancellationToken ct = default);
}

/// <summary>
/// مرز تراکنشی ماژول یکپارچه‌سازی.
/// </summary>
public interface IIntegrationUnitOfWork : IUnitOfWork;
