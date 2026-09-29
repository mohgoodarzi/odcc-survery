using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Integration.Entities;
using System.Net.Http;

namespace ODCC.Application.Modules.Integration.Abstractions;

/// <summary>
/// سرویس مدیریت یکپارچه‌سازی‌های خارجی: اندپوینت‌ها (وب‌هوک، همگام‌سازی HR،
/// SSO، ارائه‌دهنده‌ی هوش مصنوعی)، آزمون اتصال، تحویل وب‌هوک و دریافت
/// وب‌هوک‌های ورودی.
///
/// <b>امنیت (حیاتی):</b> مقدار رازها هرگز در پایگاه داده ذخیره نمی‌شود — فقط
/// نام منطقی (<see cref="Dtos.SaveIntegrationEndpointRequest.SecretRef"/>) نگه‌داری
/// می‌شود و خود مقدار در زمان استفاده از پیکربندی خوانده می‌شود. خروجی‌های
/// این سرویس هرگز مقدار راز را برنمی‌گردانند.
///
/// <b>مجوزها:</b> مدیریت اندپوینت‌ها نیازمند <c>integrations.manage</c> است.
/// </summary>
public interface IIntegrationService
{
    // --- اندپوینت‌ها ------------------------------------------------------------

    Task<PagedResult<IntegrationEndpointDto>> SearchEndpointsAsync(
        IntegrationEndpointSearchRequest request, CancellationToken ct = default);

    Task<Result<IntegrationEndpointDto>> GetEndpointByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<IntegrationEndpointDto>> CreateEndpointAsync(SaveIntegrationEndpointRequest request, CancellationToken ct = default);

    Task<Result<IntegrationEndpointDto>> UpdateEndpointAsync(Guid id, SaveIntegrationEndpointRequest request, CancellationToken ct = default);

    Task<Result<IntegrationEndpointDto>> ActivateEndpointAsync(Guid id, CancellationToken ct = default);

    Task<Result> DeactivateEndpointAsync(Guid id, CancellationToken ct = default);

    Task<Result> ArchiveEndpointAsync(Guid id, CancellationToken ct = default);

    /// <summary>آزمون اتصال یک اندپوینت با یک payload آزمونی.</summary>
    Task<Result<TestEndpointResultDto>> TestEndpointAsync(Guid id, TestIntegrationEndpointRequest request, CancellationToken ct = default);

    // --- تحویل وب‌هوک -----------------------------------------------------------

    Task<PagedResult<WebhookDeliveryDto>> SearchDeliveriesAsync(
        WebhookDeliverySearchRequest request, CancellationToken ct = default);

    /// <summary>دریافت جزئیات یک تحویل (شامل payload).</summary>
    Task<Result<WebhookDeliveryDto>> GetDeliveryByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>تلاش مجدد دستی یک تحویل ناموفق.</summary>
    Task<Result<WebhookDeliveryDto>> RetryDeliveryAsync(Guid deliveryId, CancellationToken ct = default);

    // --- وب‌هوک ورودی -----------------------------------------------------------

    /// <summary>
    /// دریافت یک وب‌هوک ورودی: تأیید امضا، ثبت رویداد و انتشار آن.
    /// مسیر عمومی است ولی امضای HMAC باید معتبر باشد.
    /// </summary>
    Task<Result<WebhookDeliveryDto>> ReceiveInboundWebhookAsync(ReceiveInboundWebhookRequest request, CancellationToken ct = default);

    // --- زمان‌بند ----------------------------------------------------------------

    /// <summary>
    /// پردازش تحویل‌های در انتظارِ سررسیده‌شده (تلاش مجدد). یک اثر جانبی است و
    /// توسط زمان‌بند پس‌زمینه (با تأیید صریح) صدا زده می‌شود.
    /// </summary>
    /// <returns>تعداد تحویل‌های پردازش‌شده.</returns>
    Task<int> ProcessPendingDeliveriesAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>آمار یکپارچه‌سازی برای داشبورد.</summary>
    Task<Result<IntegrationStatsDto>> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// ارسال رویدادها به وب‌هوک‌های خروجی فعال. این قرارداد برای سایر ماژول‌ها
/// (از طریق شنونده‌های رویداد دامنه) در دسترس است.
/// </summary>
public interface IWebhookDispatcher
{
    /// <summary>
    /// ارسال یک رویداد به همه‌ی اندپوینت‌های خروجی فعال که این نوع رویداد را
    /// مشترک شده‌اند. payload فقط متادیتای عمومی است — هرگز شناسه‌ی پاسخ‌گو.
    /// </summary>
    Task DispatchAsync(DispatchWebhookRequest request, CancellationToken ct = default);

    /// <summary>
    /// انجام یک تلاش واقعی برای تحویلِ یک ردیف وب‌هوک. نتیجه روی ردیف تحویل و
    /// روی اندپوینت ثبت می‌شود. این متد توسط سرویس و زمان‌بند تحویل استفاده
    /// می‌شود تا یک مسیر واحد برای ارسال وجود داشته باشد.
    /// </summary>
    Task AttemptDeliveryAsync(HttpClient httpClient, WebhookDelivery delivery, IntegrationEndpoint endpoint, CancellationToken ct = default);
}

/// <summary>
/// امضا و تأیید امضای وب‌هوک با HMAC-SHA256.
/// </summary>
public interface IWebhookSigner
{
    /// <summary>محاسبه‌ی امضای یک بدنه با کلید داده‌شده.</summary>
    string Sign(string secret, string payload);

    /// <summary>
    /// تأیید امضای دریافتی. از مقایسه‌ی زمان‌ثابت (constant-time) استفاده
    /// می‌کند تا در برابر حملات timing مقاوم باشد.
    /// </summary>
    bool Verify(string secret, string payload, string signatureHeader);
}

/// <summary>
/// خواندن مقدار یک راز از یک منبع ایمن (پیکربندی / user secrets / متغیرهای
/// محیطی). رازها هرگز در پایگاه داده نیستند.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// مقدار راز با نام منطقی داده‌شده. اگر رازی پیکربندی نشده باشد،
    /// <c>null</c> برمی‌گرداند (fail-closed).
    /// </summary>
    string? Resolve(string? secretRef);

    /// <summary>آیا راز با این نام منطقی پیکربندی شده است؟</summary>
    bool IsConfigured(string? secretRef);
}
