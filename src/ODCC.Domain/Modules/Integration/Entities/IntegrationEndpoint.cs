using ODCC.Domain.Common;
using ODCC.Domain.Modules.Integration.Enums;
using ODCC.Domain.Modules.Integration.Events;

namespace ODCC.Domain.Modules.Integration.Entities;

/// <summary>
/// یک اندپوینت یکپارچه‌سازی خارجی: وب‌هوک خروجی/ورودی، همگام‌سازی HR، SSO یا
/// ارائه‌دهنده‌ی هوش مصنوعی.
///
/// <b>امنیت (حیاتی):</b> مقدار واقعی هرگز در پایگاه داده ذخیره نمی‌شود. فقط
/// <see cref="SecretRef"/> (یک نام منطقی) نگه‌داری می‌شود و خود مقدار در زمان
/// استفاده از پیکربندی (user secrets / متغیرهای محیطی) خوانده می‌شود. این
/// رازها هرگز در خروجی‌های API ظاهر نمی‌شوند.
/// </summary>
public sealed class IntegrationEndpoint : BaseEntity
{
    /// <summary>نام نمایشی.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>کد یکتا (مثلاً «hr-sync-prod»).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>توضیح هدف.</summary>
    public string? Description { get; set; }

    /// <summary>نوع یکپارچه‌سازی.</summary>
    public IntegrationType Type { get; set; } = IntegrationType.OutboundWebhook;

    /// <summary>
    /// آدرس مقصد. برای وب‌هوک خروجی URL کامل، برای وب‌هوک ورودی مسیر نسبی
    /// (مثلاً «/webhooks/hr») است که سامانه روی آن گوش می‌دهد.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>روش HTTP (POST پیش‌فرض).</summary>
    public string HttpMethod { get; set; } = "POST";

    /// <summary>روش احراز هویت.</summary>
    public IntegrationAuthType AuthType { get; set; } = IntegrationAuthType.HmacSignature;

    /// <summary>
    /// نام منطقی راز (مثلاً «hr-sync-secret»). مقدار واقعی از پیکربندی
    /// <c>Integrations:Secrets:{SecretRef}</c> خوانده می‌شود و هرگز در DB نیست.
    /// </summary>
    public string? SecretRef { get; set; }

    /// <summary>نام هدری که کلید/توکن در آن قرار می‌گیرد (برای ApiKey/Bearer).</summary>
    public string? AuthHeaderName { get; set; }

    /// <summary>آیا فعال است؟ فقط اندپوینت‌های فعال پردازش می‌شوند.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>مهلت زمانی تماس (ثانیه).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>حداکثر تعداد تلاش مجدد.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// فقط این نوع رویدادها به این اندپوینت ارسال شوند (خالی یعنی همه).
    /// </summary>
    public List<string> SubscribedEvents { get; set; } = [];

    /// <summary>کاربری که اندپوینت را ایجاد کرده.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>نام نمایشی ایجادکننده (snapshot).</summary>
    public string? CreatedByUserName { get; set; }

    /// <summary>تعداد تحویل‌های موفق (برای مانیتورینگ).</summary>
    public int SuccessfulDeliveries { get; set; }

    /// <summary>تعداد تحویل‌های ناموفق (برای مانیتورینگ).</summary>
    public string? LastDeliveryError { get; set; }

    /// <summary>زمان آخرین تحویل (UTC).</summary>
    public DateTime? LastDeliveryAt { get; set; }

    // --- چرخه‌ی عمر -------------------------------------------------------------

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>ثبت نتیجه‌ی یک تحویل برای مانیتورینگ.</summary>
    public void RecordDelivery(bool success, string? error)
    {
        LastDeliveryAt = DateTime.UtcNow;

        if (success)
        {
            SuccessfulDeliveries++;
            LastDeliveryError = null;
        }
        else
        {
            LastDeliveryError = error;
        }
    }

    // --- رویدادهای دامنه -------------------------------------------------------

    public void RaiseCreatedEvent()
    {
        RaiseDomainEvent(new IntegrationEndpointCreatedEvent(Id, Name, Code, Type, Url, AuthType));
    }

    public void RaiseUpdatedEvent()
    {
        RaiseDomainEvent(new IntegrationEndpointUpdatedEvent(Id, Name, Code, Url, AuthType));
    }

    public void RaiseActivatedEvent()
    {
        RaiseDomainEvent(new IntegrationEndpointActivatedEvent(Id, Name, Code));
    }

    public void RaiseArchivedEvent()
    {
        RaiseDomainEvent(new IntegrationEndpointArchivedEvent(Id, Name, Code));
    }
}

/// <summary>
/// ثبت یک تحویل وب‌هوک خروجی: payload، وضعیت، تعداد تلاش و خطا.
///
/// <b>حریم خصوصی:</b> payload فقط شامل متادیتای عمومی رویداد (شناسه‌ها،
/// شاخص‌های تجمعی، کدها) است — هرگز شناسه‌ی پاسخ‌گوی نظرسنجی یا محتوای
/// پاسخ شخصی. این ردیف‌ها برای اشکال‌زدایی و مانیتورینگ نگه‌داری می‌شوند.
/// </summary>
public sealed class WebhookDelivery : BaseEntity
{
    /// <summary>شناسه‌ی اندپوینت مقصد.</summary>
    public Guid EndpointId { get; set; }

    /// <summary>کد اندپوینت (snapshot).</summary>
    public string EndpointCode { get; set; } = string.Empty;

    /// <summary>
    /// نوع رویداد (مثلاً «response.submitted»). برای مسیریابی و فیلتر استفاده می‌شود.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// شناسه‌ی یکتای رویداد (برای idempotency سمت گیرنده).
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>بدنه‌ی ارسالی (JSON). فقط متادیتای عمومی.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>وضعیت تحویل.</summary>
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    /// <summary>تعداد تلاش‌های انجام‌شده.</summary>
    public int AttemptCount { get; set; }

    /// <summary>زمان آخرین تلاش (UTC).</summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>زمان تلاش بعدی برنامه‌ریزی‌شده (UTC).</summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>زمان تحویل موفق (UTC).</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>کد وضعیت HTTP پاسخ (در صورت دریافت).</summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>آخرین خطا (برای اشکال‌زدایی).</summary>
    public string? LastError { get; set; }

    // --- محاسبات ---------------------------------------------------------------

    public bool CanRetry(int maxRetries) => Status == DeliveryStatus.Pending && AttemptCount < maxRetries;

    /// <summary>
    /// آیا این خطای سمت گیرنده (۴xx) قابل تلاش مجدد است؟ ۴۲۹ (درخواست‌های زیاد)
    /// و ۴۰۸ (مهلت زمانی کلاینت) استانداردهای قابل‌تلاش‌مجدد هستند.
    /// </summary>
    private static bool IsRetryableClientError(int statusCode) => statusCode is 408 or 429;

    // --- چرخه‌ی عمر -------------------------------------------------------------

    /// <summary>ثبت یک تلاش ناموفق.</summary>
    public void RecordFailure(int statusCode, string error, int maxRetries)
    {
        AttemptCount++;
        LastAttemptAt = DateTime.UtcNow;
        ResponseStatusCode = statusCode;
        LastError = error;

        if (AttemptCount >= maxRetries || (statusCode >= 400 && statusCode < 500 && !IsRetryableClientError(statusCode)))
        {
            // شکست دائمی: خطای ۴xx غیرقابل‌تلاش‌مجدد (خطای سمت گیرنده) یا اتمام
            // تعداد تلاش. ۴۲۹ (درخواست‌های زیاد) و ۴۰۸ (مهلت زمانی) خطاهای
            // قابل‌تلاش‌مجدد استاندارد هستند و نباید تحویل را خاتمه دهند.
            Status = DeliveryStatus.Failed;
            NextAttemptAt = null;
        }
        else
        {
            // شکست گذرا: تلاش مجدد با تأخیر نمایی (۲^attempt دقیقه، حداکثر ۳۰).
            var delayMinutes = Math.Min(Math.Pow(2, AttemptCount), 30);
            NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
        }
    }

    /// <summary>ثبت تحویل موفق.</summary>
    public void RecordSuccess(int statusCode)
    {
        Status = DeliveryStatus.Succeeded;
        AttemptCount++;
        LastAttemptAt = DateTime.UtcNow;
        DeliveredAt = DateTime.UtcNow;
        ResponseStatusCode = statusCode;
        LastError = null;
        NextAttemptAt = null;
    }

    // --- رویدادهای دامنه -------------------------------------------------------

    public void RaiseDeliveredEvent()
    {
        RaiseDomainEvent(new WebhookDeliveredEvent(Id, EndpointId, EndpointCode, EventType, EventId, ResponseStatusCode ?? 200, AttemptCount));
    }

    public void RaiseFailedEvent()
    {
        RaiseDomainEvent(new WebhookDeliveryFailedEvent(Id, EndpointId, EndpointCode, EventType, EventId, LastError ?? string.Empty, AttemptCount));
    }
}
