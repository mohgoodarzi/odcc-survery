using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Integration.Entities;
using ODCC.Domain.Modules.Integration.Enums;
using ODCC.Domain.Modules.Integration.Events;
using ODCC.Infrastructure.Modules.Integration.Scheduled;

namespace ODCC.Infrastructure.Modules.Integration.Services;

/// <summary>
/// سرویس مدیریت یکپارچه‌سازی‌های خارجی.
///
/// <b>امنیت (حیاتی):</b> مقدار رازها هرگز از این سرویس خارج نمی‌شود. فقط
/// نام منطقی (<c>SecretRef</c>) و وضعیت «پیکربندی‌شده یا نه» در خروجی‌ها
/// قرار می‌گیرد. خود مقدار در زمان استفاده از <see cref="ISecretResolver"/>
/// خوانده می‌شود.
///
/// <b>حریم خصوصی:</b> payloadهای وب‌هوک فقط متادیتای عمومی (شناسه‌ها، شاخص‌های
/// تجمعی، کدها) هستند. این سرویس payload را اعتبارسنجی نمی‌کند چون تولید آن
/// بر عهده‌ی شنونده‌های رویداد است که داده‌ی تجمعی می‌سازند.
/// </summary>
public sealed class IntegrationService(
    IIntegrationEndpointRepository endpointRepository,
    IWebhookDeliveryRepository deliveryRepository,
    IWebhookSigner signer,
    ISecretResolver secretResolver,
    IHttpClientFactory httpClientFactory,
    IWebhookDispatcher webhookDispatcher,
    IIntegrationUnitOfWork unitOfWork,
    IOptions<WebhookDeliveryOptions> deliveryOptions,
    IOptions<IntegrationOptions> integrationOptions,
    ICurrentUserService currentUserService,
    ILogger<IntegrationService> logger) : IIntegrationService
{
    private readonly IIntegrationEndpointRepository _endpointRepository = endpointRepository;
    private readonly IWebhookDeliveryRepository _deliveryRepository = deliveryRepository;
    private readonly IWebhookSigner _signer = signer;
    private readonly ISecretResolver _secretResolver = secretResolver;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IWebhookDispatcher _webhookDispatcher = webhookDispatcher;
    private readonly IIntegrationUnitOfWork _unitOfWork = unitOfWork;
    private readonly WebhookDeliveryOptions _deliveryOptions = deliveryOptions.Value;
    private readonly IntegrationOptions _integrationOptions = integrationOptions.Value;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<IntegrationService> _logger = logger;

    private static readonly Action<ILogger, string, string, Exception?> InvalidSignature = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(2, "IntegrationInvalidSignature"),
        "وب‌هوک ورودی با امضای نامعتبر برای اندپوینت {EndpointCode} از {SourceIp} رد شد.");

    private static readonly Action<ILogger, Guid, Exception> RetryFailed = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(3, "IntegrationRetryFailed"),
        "تلاش مجدد تحویل {DeliveryId} شکست خورد.");

    // --- اندپوینت‌ها ------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<IntegrationEndpointDto>> SearchEndpointsAsync(
        IntegrationEndpointSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _endpointRepository.CountAsync(request, ct);
        var endpoints = await _endpointRepository.SearchAsync(request, ct);

        return new PagedResult<IntegrationEndpointDto>
        {
            Items = endpoints.Select(ToEndpointDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<IntegrationEndpointDto>> GetEndpointByIdAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, ct);

        if (endpoint is null)
        {
            return Result.Failure<IntegrationEndpointDto>("integration_endpoint_not_found", "اندپوینت یافت نشد.");
        }

        return Result.Success(ToEndpointDto(endpoint));
    }

    /// <inheritdoc/>
    public async Task<Result<IntegrationEndpointDto>> CreateEndpointAsync(
        SaveIntegrationEndpointRequest request, CancellationToken ct = default)
    {
        var existing = await _endpointRepository.FindByCodeAsync(request.Code, ct);

        if (existing is not null)
        {
            return Result.Failure<IntegrationEndpointDto>("integration_code_exists", "اندپوینتی با این کد از قبل وجود دارد.");
        }

        var endpoint = new IntegrationEndpoint
        {
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            Description = request.Description,
            Type = request.Type,
            Url = request.Url.Trim(),
            HttpMethod = request.HttpMethod.ToUpperInvariant(),
            AuthType = request.AuthType,
            SecretRef = request.SecretRef,
            AuthHeaderName = request.AuthHeaderName,
            TimeoutSeconds = request.TimeoutSeconds,
            MaxRetries = request.MaxRetries,
            SubscribedEvents = (request.SubscribedEvents ?? []).Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList(),
            IsActive = request.ActivateImmediately,
            CreatedByUserId = _currentUserService.UserId,
            CreatedByUserName = _currentUserService.UserName
        };

        // هشدار امنیتی: اندپوینت احراز هویت‌شده بدون راز پیکربندی‌شده.
        if (endpoint.AuthType != IntegrationAuthType.None && !_secretResolver.IsConfigured(endpoint.SecretRef))
        {
            return Result.Failure<IntegrationEndpointDto>(
                "integration_secret_not_configured",
                $"رازی با نام «{endpoint.SecretRef}» در پیکربندی یافت نشد. آن را با متغیر محیطی Integrations__Secrets__{endpoint.SecretRef} یا user secrets تامین کنید.");
        }

        endpoint.RaiseCreatedEvent();

        await _endpointRepository.AddAsync(endpoint, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToEndpointDto(endpoint));
    }

    /// <inheritdoc/>
    public async Task<Result<IntegrationEndpointDto>> UpdateEndpointAsync(
        Guid id, SaveIntegrationEndpointRequest request, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, ct);

        if (endpoint is null)
        {
            return Result.Failure<IntegrationEndpointDto>("integration_endpoint_not_found", "اندپوینت یافت نشد.");
        }

        if (endpoint.IsDeleted)
        {
            return Result.Failure<IntegrationEndpointDto>("integration_archived", "اندپوینت بایگانی‌شده قابل ویرایش نیست.");
        }

        var conflict = await _endpointRepository.FindByCodeAsync(request.Code, ct);

        if (conflict is not null && conflict.Id != endpoint.Id)
        {
            return Result.Failure<IntegrationEndpointDto>("integration_code_exists", "اندپوینتی با این کد از قبل وجود دارد.");
        }

        endpoint.Name = request.Name.Trim();
        endpoint.Code = request.Code.Trim();
        endpoint.Description = request.Description;
        endpoint.Type = request.Type;
        endpoint.Url = request.Url.Trim();
        endpoint.HttpMethod = request.HttpMethod.ToUpperInvariant();
        endpoint.AuthType = request.AuthType;
        endpoint.SecretRef = request.SecretRef;
        endpoint.AuthHeaderName = request.AuthHeaderName;
        endpoint.TimeoutSeconds = request.TimeoutSeconds;
        endpoint.MaxRetries = request.MaxRetries;
        endpoint.SubscribedEvents = (request.SubscribedEvents ?? []).Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList();

        if (endpoint.AuthType != IntegrationAuthType.None && !_secretResolver.IsConfigured(endpoint.SecretRef))
        {
            return Result.Failure<IntegrationEndpointDto>(
                "integration_secret_not_configured",
                $"رازی با نام «{endpoint.SecretRef}» در پیکربندی یافت نشد. آن را با متغیر محیطی Integrations__Secrets__{endpoint.SecretRef} یا user secrets تامین کنید.");
        }

        endpoint.RaiseUpdatedEvent();

        _endpointRepository.Update(endpoint);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToEndpointDto(endpoint));
    }

    /// <inheritdoc/>
    public async Task<Result<IntegrationEndpointDto>> ActivateEndpointAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, ct);

        if (endpoint is null)
        {
            return Result.Failure<IntegrationEndpointDto>("integration_endpoint_not_found", "اندپوینت یافت نشد.");
        }

        endpoint.Activate();
        endpoint.RaiseActivatedEvent();

        _endpointRepository.Update(endpoint);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToEndpointDto(endpoint));
    }

    /// <inheritdoc/>
    public async Task<Result> DeactivateEndpointAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, ct);

        if (endpoint is null)
        {
            return Result.Failure("integration_endpoint_not_found", "اندپوینت یافت نشد.");
        }

        endpoint.Deactivate();

        _endpointRepository.Update(endpoint);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ArchiveEndpointAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, ct);

        if (endpoint is null)
        {
            return Result.Failure("integration_endpoint_not_found", "اندپوینت یافت نشد.");
        }

        endpoint.Deactivate();
        endpoint.RaiseArchivedEvent();

        _endpointRepository.Remove(endpoint);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<TestEndpointResultDto>> TestEndpointAsync(
        Guid id, TestIntegrationEndpointRequest request, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, ct);

        if (endpoint is null)
        {
            return Result.Failure<TestEndpointResultDto>("integration_endpoint_not_found", "اندپوینت یافت نشد.");
        }

        var payload = string.IsNullOrWhiteSpace(request.PayloadJson)
            ? "{\"event\":\"test_connection\",\"timestamp\":\"" + DateTime.UtcNow.ToString("O") + "\"}"
            : request.PayloadJson;

        using var httpClient = _httpClientFactory.CreateClient(DependencyInjection.IntegrationHttpClientName);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var (success, statusCode, error) = await WebhookHttpSender.SendAsync(
            httpClient, endpoint, payload, _signer, _secretResolver, ct);

        stopwatch.Stop();

        return Result.Success(new TestEndpointResultDto
        {
            Success = success,
            StatusCode = statusCode == 0 ? null : statusCode,
            Error = error,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
        });
    }

    // --- تحویل وب‌هوک -----------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<WebhookDeliveryDto>> SearchDeliveriesAsync(
        WebhookDeliverySearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _deliveryRepository.CountAsync(request, ct);
        var deliveries = await _deliveryRepository.SearchAsync(request, ct);

        return new PagedResult<WebhookDeliveryDto>
        {
            Items = deliveries.Select(d => ToDeliveryDto(d)).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<WebhookDeliveryDto>> GetDeliveryByIdAsync(Guid id, CancellationToken ct = default)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(id, ct);

        if (delivery is null)
        {
            return Result.Failure<WebhookDeliveryDto>("webhook_delivery_not_found", "تحویل یافت نشد.");
        }

        return Result.Success(ToDeliveryDto(delivery, includePayload: true));
    }

    /// <inheritdoc/>
    public async Task<Result<WebhookDeliveryDto>> RetryDeliveryAsync(Guid deliveryId, CancellationToken ct = default)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(deliveryId, ct);

        if (delivery is null)
        {
            return Result.Failure<WebhookDeliveryDto>("webhook_delivery_not_found", "تحویل یافت نشد.");
        }

        if (delivery.Status == DeliveryStatus.Succeeded)
        {
            return Result.Failure<WebhookDeliveryDto>("webhook_delivery_succeeded", "این تحویل قبلاً موفق بوده است.");
        }

        var endpoint = await _endpointRepository.GetByIdAsync(delivery.EndpointId, ct);

        if (endpoint is null || !endpoint.IsActive)
        {
            return Result.Failure<WebhookDeliveryDto>("integration_endpoint_not_active", "اندپوینت مقصد فعال نیست.");
        }

        // تلاش مجدد دستی هم باید از سیاست تلاش مجدد پیروی کند: یک تحویل که
        // شکست دائمی خورده (اتمام سقف تلاش یا خطای ۴xx غیرقابل‌تلاش) نباید
        // دوباره زنده شود.
        if (!delivery.CanRetry(Math.Max(endpoint.MaxRetries, 1)))
        {
            return Result.Failure<WebhookDeliveryDto>("webhook_delivery_not_retryable",
                "این تحویل قابل تلاش مجدد نیست (شکست دائمی یا اتمام سقف تلاش).");
        }

        using var httpClient = _httpClientFactory.CreateClient(DependencyInjection.IntegrationHttpClientName);

        await _webhookDispatcher.AttemptDeliveryAsync(httpClient, delivery, endpoint, ct);

        return Result.Success(ToDeliveryDto(delivery, includePayload: true));
    }

    // --- وب‌هوک ورودی -----------------------------------------------------------

    /// <inheritdoc/>
    public async Task<Result<WebhookDeliveryDto>> ReceiveInboundWebhookAsync(
        ReceiveInboundWebhookRequest request, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepository.FindByCodeAsync(request.EndpointCode, ct);

        if (endpoint is null || endpoint.Type != IntegrationType.InboundWebhook)
        {
            // پیام خطا عمداً مبهم است تا وجود اندپوینت فاش نشود.
            return Result.Failure<WebhookDeliveryDto>("integration_endpoint_not_found", "اندپوینت ورودی یافت نشد.");
        }

        // تأیید امضا برای اندپوینت‌های احراز هویت‌شده — الزامی.
        if (endpoint.AuthType != IntegrationAuthType.None)
        {
            var secret = _secretResolver.Resolve(endpoint.SecretRef);

            if (string.IsNullOrEmpty(secret))
            {
                return Result.Failure<WebhookDeliveryDto>("integration_secret_not_configured", "راز اندپوینت پیکربندی نشده است.");
            }

            // هدر امضای ورودی همیشه از تنظیمات سراسری خوانده می‌شود تا یک منبع
            // واحد برای قرار داد امضا وجود داشته باشد (پیش‌فرض: X-ODCC-Signature).
            // AuthHeaderName اندپوینت فقط برای تماس‌های خروجی استفاده می‌شود.
            var headerName = string.IsNullOrWhiteSpace(_integrationOptions.SignatureHeaderName)
                ? "X-ODCC-Signature"
                : _integrationOptions.SignatureHeaderName;

            if (string.IsNullOrWhiteSpace(request.SignatureHeader))
            {
                return Result.Failure<WebhookDeliveryDto>("integration_signature_missing", $"هدر امضا ({headerName}) ارسال نشده است.");
            }

            if (!_signer.Verify(secret, request.PayloadJson, request.SignatureHeader))
            {
                // امضای نامعتبر: رد می‌کنیم. این یک تلاش نفوذ احتمالی است.
                InvalidSignature(_logger, endpoint.Code, request.SourceIp ?? "نامشخص", null);

                return Result.Failure<WebhookDeliveryDto>("integration_signature_invalid", "امضای وب‌هوک نامعتبر است.");
            }
        }

        // محافظت در برابر بازپخش: اگر فرستنده برچسب زمانی ارسال کرده و تلرانس
        // فعال است، وب‌هوک‌های قدیمی‌تر از پنجره رد می‌شوند. فقط در صورتی اعمال
        // می‌شود که هدر موجود باشد تا با فرستنده‌هایی که برچسب نمی‌فرستند
        // سازگار بماند.
        if (_integrationOptions.InboundTimestampToleranceSeconds > 0
            && !string.IsNullOrWhiteSpace(request.TimestampHeader))
        {
            if (!DateTime.TryParse(request.TimestampHeader, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp)
                || timestamp < DateTime.UtcNow.AddSeconds(-_integrationOptions.InboundTimestampToleranceSeconds))
            {
                return Result.Failure<WebhookDeliveryDto>("integration_timestamp_expired",
                    "برچسب زمانی وب‌هوک نامعتبر است یا بیش از حد قدیمی است (بازپخش).");
            }
        }

        // شناسه‌ی رویداد از payload (در صورت وجود) یا تولید جدید.
        var eventId = TryExtractEventId(request.PayloadJson) ?? Guid.CreateVersion7().ToString("N");

        // Idempotency: اگر این رویداد قبلاً برای این اندپوینت ثبت شده، همان
        // نتیجه را برمی‌گردانیم تا بازپخشِ فرستنده باعث ردیف مضاعف (و خطای
        // کلید یکتا) نشود. این قرارداد با مسیر تحویل خروجی یکسان است.
        var existingDelivery = await _deliveryRepository.FindByEndpointAndEventIdAsync(endpoint.Id, eventId, ct);

        if (existingDelivery is not null)
        {
            return Result.Success(ToDeliveryDto(existingDelivery));
        }

        var delivery = new WebhookDelivery
        {
            EndpointId = endpoint.Id,
            EndpointCode = endpoint.Code,
            EventType = TryExtractEventType(request.PayloadJson) ?? "inbound",
            EventId = eventId,
            PayloadJson = request.PayloadJson,
            Status = DeliveryStatus.Succeeded,
            DeliveredAt = DateTime.UtcNow,
            LastAttemptAt = DateTime.UtcNow,
            AttemptCount = 1,
            ResponseStatusCode = 200
        };

        delivery.RaiseDomainEvent(new InboundWebhookReceivedEvent(
            endpoint.Id, endpoint.Code, delivery.EventType, eventId,
            request.SourceIp ?? "نامشخص", request.PayloadJson));

        await _deliveryRepository.AddAsync(delivery, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDeliveryDto(delivery));
    }

    // --- زمان‌بند ----------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<int> ProcessPendingDeliveriesAsync(DateTime asOf, CancellationToken ct = default)
    {
        var pending = await _deliveryRepository.ListRetryableWithEndpointsAsync(
            asOf, Math.Clamp(_deliveryOptions.MaxPerCycle, 1, 500), ct);

        if (pending.Count == 0)
        {
            return 0;
        }

        using var httpClient = _httpClientFactory.CreateClient(DependencyInjection.IntegrationHttpClientName);

        foreach (var (delivery, endpoint) in pending)
        {
            try
            {
                await _webhookDispatcher.AttemptDeliveryAsync(httpClient, delivery, endpoint, ct);
            }
            catch (Exception ex)
            {
                // یک تحویل نباید سایرین را متوقف کند.
                RetryFailed(_logger, delivery.Id, ex);
            }
        }

        return pending.Count;
    }

    /// <inheritdoc/>
    public async Task<Result<IntegrationStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new IntegrationStatsDto
        {
            TotalEndpoints = await _endpointRepository.CountAsync(ct),
            ActiveEndpoints = await _endpointRepository.CountAsync(
                new IntegrationEndpointSearchRequest { IsActive = true }, ct),
            PendingDeliveries = await _deliveryRepository.CountPendingAsync(ct),
            FailedDeliveries = await _deliveryRepository.CountAsync(
                new WebhookDeliverySearchRequest { Status = DeliveryStatus.Failed }, ct),
            SuccessfulDeliveries = await _deliveryRepository.CountSucceededAsync(ct)
        };

        return Result.Success(stats);
    }

    // --- کمک‌ها -----------------------------------------------------------------

    /// <summary>
    /// تبدیل به DTO. <b>هرگز</b> مقدار راز را برنمی‌گرداند — فقط نام منطقی و
    /// وضعیت پیکربندی‌بودن.
    /// </summary>
    private IntegrationEndpointDto ToEndpointDto(IntegrationEndpoint endpoint) => new()
    {
        Id = endpoint.Id,
        Name = endpoint.Name,
        Code = endpoint.Code,
        Description = endpoint.Description,
        Type = endpoint.Type,
        Url = endpoint.Url,
        HttpMethod = endpoint.HttpMethod,
        AuthType = endpoint.AuthType,
        SecretRef = endpoint.SecretRef,
        SecretConfigured = _secretResolver.IsConfigured(endpoint.SecretRef),
        AuthHeaderName = endpoint.AuthHeaderName,
        IsActive = endpoint.IsActive,
        TimeoutSeconds = endpoint.TimeoutSeconds,
        MaxRetries = endpoint.MaxRetries,
        SubscribedEvents = endpoint.SubscribedEvents,
        CreatedByUserId = endpoint.CreatedByUserId,
        CreatedByUserName = endpoint.CreatedByUserName,
        CreatedAt = endpoint.CreatedAt,
        SuccessfulDeliveries = endpoint.SuccessfulDeliveries,
        LastDeliveryError = endpoint.LastDeliveryError,
        LastDeliveryAt = endpoint.LastDeliveryAt
    };

    /// <summary>
    /// تبدیل به DTO. payload فقط در حالت جزئیات (با مجوز مشاهده) برمی‌گردد.
    /// </summary>
    private static WebhookDeliveryDto ToDeliveryDto(WebhookDelivery delivery, bool includePayload = false) => new()
    {
        Id = delivery.Id,
        EndpointId = delivery.EndpointId,
        EndpointCode = delivery.EndpointCode,
        EventType = delivery.EventType,
        EventId = delivery.EventId,
        Status = delivery.Status,
        AttemptCount = delivery.AttemptCount,
        LastAttemptAt = delivery.LastAttemptAt,
        NextAttemptAt = delivery.NextAttemptAt,
        DeliveredAt = delivery.DeliveredAt,
        ResponseStatusCode = delivery.ResponseStatusCode,
        LastError = delivery.LastError,
        CreatedAt = delivery.CreatedAt,
        PayloadJson = includePayload ? delivery.PayloadJson : null
    };

    /// <summary>استخراج شناسه‌ی رویداد از payload ورودی (در صورت وجود فیلد eventId).</summary>
    private static string? TryExtractEventId(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);

            if (doc.RootElement.TryGetProperty("eventId", out var id) && id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }

            if (doc.RootElement.TryGetProperty("id", out var rawId) && rawId.ValueKind == JsonValueKind.String)
            {
                return rawId.GetString();
            }
        }
        catch
        {
            // payload نامعتبر JSON نیست — شناسه تولید می‌شود.
        }

        return null;
    }

    /// <summary>استخراج نوع رویداد از payload ورودی (در صورت وجود).</summary>
    private static string? TryExtractEventType(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);

            if (doc.RootElement.TryGetProperty("eventType", out var type) && type.ValueKind == JsonValueKind.String)
            {
                return type.GetString();
            }

            if (doc.RootElement.TryGetProperty("type", out var rawType) && rawType.ValueKind == JsonValueKind.String)
            {
                return rawType.GetString();
            }
        }
        catch
        {
            // payload نامعتبر.
        }

        return null;
    }
}
