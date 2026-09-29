using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;
using ODCC.Domain.Modules.Integration.Entities;
using ODCC.Domain.Modules.Integration.Enums;
using ODCC.Infrastructure.Modules.Integration.Scheduled;
using System.Net.Http;

namespace ODCC.Infrastructure.Modules.Integration.Services;

/// <summary>
/// ارسال رویدادها به وب‌هوک‌های خروجی فعال.
///
/// <b>طراحی:</b> این سرویس توسط شنونده‌های رویداد دامنه (در همین ماژول) صدا
/// زده می‌شود. payload فقط متادیتای عمومی است — شنونده‌ها مسئول اطمینان از
/// این هستند که هیچ شناسه‌ی پاسخ‌گوی نظرسنجی یا محتوای شخصی در payload نیست.
///
/// <b>خطا:</b> شکست تحویل وب‌هوک نباید رویداد اصلی را لغو کند؛ فقط لاگ و در
/// جدول تحویل ثبت می‌شود تا بعداً تلاش مجدد شود.
/// </summary>
public sealed class WebhookDispatcher(
    IIntegrationEndpointRepository endpointRepository,
    IWebhookDeliveryRepository deliveryRepository,
    IWebhookSigner signer,
    ISecretResolver secretResolver,
    IHttpClientFactory httpClientFactory,
    IIntegrationUnitOfWork unitOfWork,
    IOptions<WebhookDeliveryOptions> deliveryOptions,
    ILogger<WebhookDispatcher> logger) : IWebhookDispatcher
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IIntegrationEndpointRepository _endpointRepository = endpointRepository;
    private readonly IWebhookDeliveryRepository _deliveryRepository = deliveryRepository;
    private readonly IWebhookSigner _signer = signer;
    private readonly ISecretResolver _secretResolver = secretResolver;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IIntegrationUnitOfWork _unitOfWork = unitOfWork;
    private readonly WebhookDeliveryOptions _deliveryOptions = deliveryOptions.Value;
    private readonly ILogger<WebhookDispatcher> _logger = logger;

    private static readonly Action<ILogger, string, Exception> DispatchFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1, "WebhookDispatchFailed"),
        "ارسال رویداد {EventType} به وب‌هوک‌ها ناموفق بود.");

    /// <inheritdoc/>
    public async Task DispatchAsync(DispatchWebhookRequest request, CancellationToken ct = default)
    {
        try
        {
            // همه‌ی اندپوینت‌های خروجی فعال.
            var endpoints = await _endpointRepository.ListActiveByTypeAsync(IntegrationType.OutboundWebhook, ct);

            if (endpoints.Count == 0)
            {
                return;
            }

            // فیلتر: اندپوینت‌هایی که این نوع رویداد را مشترک شده‌اند (یا همه).
            var subscribed = endpoints.Where(e =>
                e.SubscribedEvents.Count == 0 || e.SubscribedEvents.Contains(request.EventType)).ToList();

            if (subscribed.Count == 0)
            {
                return;
            }

            var payloadJson = JsonSerializer.Serialize(request.Payload, PayloadJsonOptions);

            using var httpClient = _httpClientFactory.CreateClient(DependencyInjection.IntegrationHttpClientName);

            foreach (var endpoint in subscribed)
            {
                // Idempotency: اگر این رویداد قبلاً برای این اندپوینت ثبت شده،
                // چیزی جدید ارسال نمی‌کنیم.
                var existing = await _deliveryRepository.FindByEndpointAndEventIdAsync(endpoint.Id, request.EventId, ct);

                if (existing is not null)
                {
                    continue;
                }

                var delivery = new WebhookDelivery
                {
                    EndpointId = endpoint.Id,
                    EndpointCode = endpoint.Code,
                    EventType = request.EventType,
                    EventId = request.EventId,
                    PayloadJson = payloadJson,
                    Status = DeliveryStatus.Pending,
                    // زمان‌بندی تلاش بعدی از همینجا تنظیم می‌شود تا اگر پردازش
                    // بین commit و ارسال متوقف شد، این ردیف توسط زمان‌بند
                    // (در صورت فعال‌بودن) دوباره برداشته شود و برای همیشه
                    // در حالت Pending بدون زمان‌بندی نماند.
                    NextAttemptAt = DateTime.UtcNow
                };

                await _deliveryRepository.AddAsync(delivery, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // تحویل بلافاصله. شکست آن در ردیف ثبت و بعداً تلاش مجدد می‌شود.
                await AttemptDeliveryAsync(httpClient, delivery, endpoint, ct);
            }
        }
        catch (Exception ex)
        {
            // شکست تحویل نباید رویداد اصلی را لغو کند.
            DispatchFailed(_logger, request.EventType, ex);
        }
    }

    /// <summary>
    /// یک تلاش واقعی برای تحویل. نتیجه روی ردیف و روی اندپوینت ثبت می‌شود.
    /// </summary>
    public async Task AttemptDeliveryAsync(
        HttpClient httpClient,
        WebhookDelivery delivery,
        IntegrationEndpoint endpoint,
        CancellationToken ct = default)
    {
        try
        {
            var (success, statusCode, error) = await WebhookHttpSender.SendAsync(
                httpClient, endpoint, delivery.PayloadJson, _signer, _secretResolver, ct);

            if (success)
            {
                delivery.RecordSuccess(statusCode);
                delivery.RaiseDeliveredEvent();
                endpoint.RecordDelivery(success, null);
            }
            else
            {
                delivery.RecordFailure(statusCode, error ?? "خطای نامشخص", endpoint.MaxRetries);
                delivery.RaiseFailedEvent();
                endpoint.RecordDelivery(false, error);
            }
        }
        catch (Exception ex)
        {
            delivery.RecordFailure(0, ex.Message, endpoint.MaxRetries);
            delivery.RaiseFailedEvent();
            endpoint.RecordDelivery(false, ex.Message);
        }

        _deliveryRepository.Update(delivery);
        _endpointRepository.Update(endpoint);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
