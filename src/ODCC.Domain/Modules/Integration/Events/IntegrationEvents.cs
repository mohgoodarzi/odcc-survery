using ODCC.Domain.Common;
using ODCC.Domain.Modules.Integration.Enums;

namespace ODCC.Domain.Modules.Integration.Events;

/// <summary>
/// رویدادهای دامنه‌ی ماژول یکپارچه‌سازی.
///
/// <b>طراحی:</b> این رویدادها فقط شناسه‌ها و متادیتای عمومی (کدها، نوع،
/// وضعیت تحویل) را حمل می‌کنند. رویدادهای تحویل وب‌هوک هرگز payload کامل را
/// در خود ندارند (payload در موجودیت <c>WebhookDelivery</c> و فقط برای
/// اشکال‌زدایی نگه‌داری می‌شود).
///
/// شنونده‌ها: ممیزی (ثبت رخداد).
/// </summary>
public sealed class IntegrationEndpointCreatedEvent : DomainEvent
{
    public Guid EndpointId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public IntegrationType Type { get; init; }
    public string Url { get; init; } = string.Empty;
    public IntegrationAuthType AuthType { get; init; }

    public IntegrationEndpointCreatedEvent(
        Guid endpointId, string name, string code, IntegrationType type, string url, IntegrationAuthType authType)
    {
        EndpointId = endpointId;
        Name = name;
        Code = code;
        Type = type;
        Url = url;
        AuthType = authType;
    }
}

public sealed class IntegrationEndpointUpdatedEvent : DomainEvent
{
    public Guid EndpointId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public IntegrationAuthType AuthType { get; init; }

    public IntegrationEndpointUpdatedEvent(
        Guid endpointId, string name, string code, string url, IntegrationAuthType authType)
    {
        EndpointId = endpointId;
        Name = name;
        Code = code;
        Url = url;
        AuthType = authType;
    }
}

public sealed class IntegrationEndpointActivatedEvent : DomainEvent
{
    public Guid EndpointId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;

    public IntegrationEndpointActivatedEvent(Guid endpointId, string name, string code)
    {
        EndpointId = endpointId;
        Name = name;
        Code = code;
    }
}

public sealed class IntegrationEndpointArchivedEvent : DomainEvent
{
    public Guid EndpointId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;

    public IntegrationEndpointArchivedEvent(Guid endpointId, string name, string code)
    {
        EndpointId = endpointId;
        Name = name;
        Code = code;
    }
}

/// <summary>
/// یک وب‌هوک با موفقیت تحویل داده شد.
/// </summary>
public sealed class WebhookDeliveredEvent : DomainEvent
{
    public Guid DeliveryId { get; init; }
    public Guid EndpointId { get; init; }
    public string EndpointCode { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public int AttemptCount { get; init; }

    public WebhookDeliveredEvent(
        Guid deliveryId, Guid endpointId, string endpointCode,
        string eventType, string eventId, int statusCode, int attemptCount)
    {
        DeliveryId = deliveryId;
        EndpointId = endpointId;
        EndpointCode = endpointCode;
        EventType = eventType;
        EventId = eventId;
        StatusCode = statusCode;
        AttemptCount = attemptCount;
    }
}

/// <summary>
/// تحویل یک وب‌هوک شکست خورد (دائمی یا موقت).
/// </summary>
public sealed class WebhookDeliveryFailedEvent : DomainEvent
{
    public Guid DeliveryId { get; init; }
    public Guid EndpointId { get; init; }
    public string EndpointCode { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int AttemptCount { get; init; }

    public WebhookDeliveryFailedEvent(
        Guid deliveryId, Guid endpointId, string endpointCode,
        string eventType, string eventId, string error, int attemptCount)
    {
        DeliveryId = deliveryId;
        EndpointId = endpointId;
        EndpointCode = endpointCode;
        EventType = eventType;
        EventId = eventId;
        Error = error;
        AttemptCount = attemptCount;
    }
}

/// <summary>
/// یک وب‌هوک ورودی دریافت شده است. این رویداد به ماژول‌های دیگر اجازه می‌دهد
/// در صورت نیاز به داده‌ی دریافتی واکنش نشان دهند.
/// </summary>
public sealed class InboundWebhookReceivedEvent : DomainEvent
{
    public Guid EndpointId { get; init; }
    public string EndpointCode { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public string SourceIp { get; init; } = string.Empty;

    /// <summary>بدنه‌ی دریافتی (JSON). فقط در شنونده‌ی پردازشگر استفاده می‌شود.</summary>
    public string PayloadJson { get; init; } = string.Empty;

    public InboundWebhookReceivedEvent(
        Guid endpointId, string endpointCode, string eventType, string eventId, string sourceIp, string payloadJson)
    {
        EndpointId = endpointId;
        EndpointCode = endpointCode;
        EventType = eventType;
        EventId = eventId;
        SourceIp = sourceIp;
        PayloadJson = payloadJson;
    }
}
