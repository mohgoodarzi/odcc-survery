namespace ODCC.Infrastructure.Modules.Integration.Scheduled;

/// <summary>
/// تنظیمات تحویل وب‌هوک.
/// </summary>
public sealed class WebhookDeliveryOptions
{
    public const string SectionName = "Integrations:Delivery";

    /// <summary>
    /// فعال‌سازی زمان‌بند تحویل مجدد. یک اثر جانبی است و باید صریحاً فعال شود
    /// (سیاست پروژه). تحویل بلافاصله همیشه فعال است؛ این زمان‌بند فقط برای
    /// تلاش مجدد و حجم بالا لازم است.
    /// </summary>
    public bool EnableDeliveryScheduler { get; set; }

    /// <summary>فاصله‌ی هر چرخه‌ی بررسی (ثانیه).</summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>حداکثر تعداد تحویل در هر چرخه.</summary>
    public int MaxPerCycle { get; set; } = 100;
}

/// <summary>
/// تنظیمات کلی ماژول یکپارچه‌سازی.
/// </summary>
public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";

    /// <summary>
    /// نام هدر امضای پیش‌فرض برای وب‌هوک‌های خروجی.
    /// </summary>
    public string SignatureHeaderName { get; set; } = "X-ODCC-Signature";

    /// <summary>
    /// نام هدر برچسب زمانی وب‌هوک ورودی (اختیاری). اگر فرستنده این هدر را
    /// ارسال کند، مقدار آن برای محافظت در برابر بازپخش بررسی می‌شود.
    /// </summary>
    public string InboundTimestampHeaderName { get; set; } = "X-ODCC-Timestamp";

    /// <summary>
    /// تلرانس برچسب زمانی وب‌هوک ورودی به ثانیه. <c>0</c> یعنی غیرفعال (سازگار
    /// با فرستنده‌هایی که برچسب زمانی نمی‌فرستند). مقدار مثلاً <c>300</c> یعنی
    /// وب‌هوک‌های قدیمی‌تر از ۵ دقیقه رد می‌شوند. فقط در صورتی اعمال می‌شود
    /// که فرستنده هدر برچسب زمانی را ارسال کرده باشد.
    /// </summary>
    public int InboundTimestampToleranceSeconds { get; set; } = 300;

    /// <summary>
    /// رازهای اندپوینت‌ها. کلید = نام منطقی (<c>SecretRef</c>)، مقدار = خود راز.
    /// <b>هرگز</b> این مقادیر را در مخزن کد قرار ندهید — از user secrets یا
    /// متغیرهای محیطی (Integrations__Secrets__{name}) استفاده کنید.
    /// </summary>
    public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
