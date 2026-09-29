using ODCC.Application.Abstractions;
using ODCC.Domain.Modules.Integration.Enums;

namespace ODCC.Application.Modules.Integration.Dtos;

// --- اندپوینت: درخواست‌ها ---------------------------------------------------

/// <summary>فیلتر جستجوی اندپوینت‌های یکپارچه‌سازی.</summary>
public sealed record IntegrationEndpointSearchRequest
{
    public string? SearchText { get; init; }
    public IntegrationType? Type { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>شامل اندپوینت‌های بایگانی‌شده (حذف نرم) شود؟</summary>
    public bool IncludeArchived { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record SaveIntegrationEndpointRequest
{
    public required string Name { get; init; }
    public required string Code { get; init; }
    public string? Description { get; init; }
    public IntegrationType Type { get; init; } = IntegrationType.OutboundWebhook;
    public required string Url { get; init; }
    public string HttpMethod { get; init; } = "POST";
    public IntegrationAuthType AuthType { get; init; } = IntegrationAuthType.HmacSignature;

    /// <summary>
    /// نام منطقی راز. مقدار واقعی از پیکربندی <c>Integrations:Secrets:{SecretRef}</c>
    /// خوانده می‌شود و هرگز در پایگاه داده ذخیره نمی‌شود.
    /// </summary>
    public string? SecretRef { get; init; }

    /// <summary>نام هدر احراز هویت (برای ApiKey/Bearer). پیش‌فرض برای Hmac: X-ODCC-Signature.</summary>
    public string? AuthHeaderName { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 3;

    /// <summary>فقط این نوع رویدادها ارسال شوند (خالی یعنی همه).</summary>
    public IReadOnlyList<string>? SubscribedEvents { get; init; }

    /// <summary>آیا بلافاصله پس از ساخت فعال شود؟</summary>
    public bool ActivateImmediately { get; init; } = true;
}

/// <summary>آزمون اتصال یک اندپوینت.</summary>
public sealed record TestIntegrationEndpointRequest
{
    /// <summary>بدنه‌ی آزمونی (JSON). اختیاری.</summary>
    public string? PayloadJson { get; init; }
}

// --- تحویل وب‌هوک: درخواست‌ها -------------------------------------------------

/// <summary>فیلتر جستجوی تحویل‌های وب‌هوک.</summary>
public sealed record WebhookDeliverySearchRequest
{
    public string? SearchText { get; init; }
    public DeliveryStatus? Status { get; init; }
    public Guid? EndpointId { get; init; }
    public string? EventType { get; init; }

    /// <summary>فقط تحویل‌های نیازمند تلاش مجدد؟</summary>
    public bool RetryableOnly { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// رویدادی برای ارسال به وب‌هوک‌ها. فقط متادیتای عمومی — هرگز شناسه‌ی پاسخ‌گو.
/// </summary>
public sealed record DispatchWebhookRequest
{
    /// <summary>نوع رویداد (مثلاً «response.submitted»).</summary>
    public required string EventType { get; init; }

    /// <summary>شناسه‌ی یکتای رویداد (برای idempotency).</summary>
    public required string EventId { get; init; }

    /// <summary>
    /// payload به‌صورت دیکشنری تخت (JSON-serializable). فقط متادیتای عمومی
    /// (شناسه‌ها، شاخص‌های تجمعی، کدها) — هرگز شناسه‌ی پاسخ‌گوی نظرسنجی.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Payload { get; init; }
}

// --- وب‌هوک ورودی: درخواست‌ها -------------------------------------------------

/// <summary>دریافت یک وب‌هوک ورودی.</summary>
public sealed class ReceiveInboundWebhookRequest
{
    /// <summary>کد اندپوینت ورودی.</summary>
    public required string EndpointCode { get; init; }

    /// <summary>بدنه‌ی دریافتی (JSON خام).</summary>
    public required string PayloadJson { get; init; }

    /// <summary>امضای دریافتی از هدر.</summary>
    public string? SignatureHeader { get; init; }

    /// <summary>
    /// برچسب زمانی دریافتی از هدر (اختیاری). اگر فرستنده آن را ارسال کرده باشد
    /// و <c>Integrations:InboundTimestampToleranceSeconds</c> بیشتر از صفر باشد،
    /// در برابر بازپخش‌های قدیمی محافظت می‌کند.
    /// </summary>
    public string? TimestampHeader { get; init; }

    /// <summary>آدرس IP فرستنده (برای ممیزی).</summary>
    public string? SourceIp { get; init; }
}

// --- خروجی‌ها -----------------------------------------------------------------

/// <summary>
/// خروجی اندپوینت. <b>هرگز</b> مقدار راز را برنمی‌گرداند — فقط نام منطقی آن.
/// </summary>
public sealed record IntegrationEndpointDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IntegrationType Type { get; init; }
    public string Url { get; init; } = string.Empty;
    public string HttpMethod { get; init; } = "POST";
    public IntegrationAuthType AuthType { get; init; }

    /// <summary>فقط نام منطقی راز — مقدار واقعی هرگز برگردانده نمی‌شود.</summary>
    public string? SecretRef { get; init; }

    /// <summary>آیا رازِ این اندپوینت در پیکربندی موجود است؟</summary>
    public bool SecretConfigured { get; init; }

    public string? AuthHeaderName { get; init; }
    public bool IsActive { get; init; }
    public int TimeoutSeconds { get; init; }
    public int MaxRetries { get; init; }
    public IReadOnlyList<string> SubscribedEvents { get; init; } = [];
    public Guid? CreatedByUserId { get; init; }
    public string? CreatedByUserName { get; init; }
    public DateTime CreatedAt { get; init; }
    public int SuccessfulDeliveries { get; init; }
    public string? LastDeliveryError { get; init; }
    public DateTime? LastDeliveryAt { get; init; }
}

public sealed record WebhookDeliveryDto
{
    public Guid Id { get; init; }
    public Guid EndpointId { get; init; }
    public string EndpointCode { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public DeliveryStatus Status { get; init; }
    public int AttemptCount { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public DateTime? NextAttemptAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public int? ResponseStatusCode { get; init; }
    public string? LastError { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>payload فقط در جستجوی جزئیات برگردانده می‌شود (نه فهرست).</summary>
    public string? PayloadJson { get; init; }
}

/// <summary>نتیجه‌ی آزمون اتصال.</summary>
public sealed record TestEndpointResultDto
{
    public bool Success { get; init; }
    public int? StatusCode { get; init; }
    public string? Error { get; init; }
    public long ElapsedMilliseconds { get; init; }
}

/// <summary>آمار یکپارچه‌سازی برای داشبورد.</summary>
public sealed record IntegrationStatsDto
{
    public int TotalEndpoints { get; init; }
    public int ActiveEndpoints { get; init; }
    public int PendingDeliveries { get; init; }
    public int FailedDeliveries { get; init; }
    public int SuccessfulDeliveries { get; init; }
}
