namespace ODCC.Domain.Modules.Integration.Enums;

/// <summary>
/// نوع اندپوینت یکپارچه‌سازی.
/// </summary>
public enum IntegrationType
{
    /// <summary>وب‌هوک خروجی: رویدادهای سامانه به یک URL خارجی ارسال می‌شوند.</summary>
    OutboundWebhook = 0,

    /// <summary>وب‌هوک ورودی: سامانه رویدادها را از یک سرویس خارجی دریافت می‌کند.</summary>
    InboundWebhook = 1,

    /// <summary>همگام‌سازی منابع انسانی (کارمندان/واحدها).</summary>
    HrSync = 2,

    /// <summary>ورود یکپارچه (SSO).</summary>
    Sso = 3,

    /// <summary>ارائه‌دهنده‌ی تحلیل هوش مصنوعی.</summary>
    AiProvider = 4
}

/// <summary>
/// روش احراز هویت اندپوینت خارجی.
/// </summary>
public enum IntegrationAuthType
{
    /// <summary>بدون احراز هویت (عمومی).</summary>
    None = 0,

    /// <summary>امضای HMAC-SHA256 بدنه با یک کلید مشترک (پیشنهادی برای وب‌هوک).</summary>
    HmacSignature = 1,

    /// <summary>توکن Bearer در هدر Authorization.</summary>
    BearerToken = 2,

    /// <summary>کلید API در هدر اختصاصی.</summary>
    ApiKey = 3,

    /// <summary>احراز هویت پایه (Basic).</summary>
    Basic = 4
}

/// <summary>
/// وضعیت تحویل یک وب‌هوک.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>در انتظار ارسال (یا ارسال مجدد برنامه‌ریزی‌شده).</summary>
    Pending = 0,

    /// <summary>تحویل با موفقیت (کد وضعیت ۲xx).</summary>
    Succeeded = 1,

    /// <summary>شکست دائمی (کد وضعیت ۴xx یا پایان تعداد تلاش).</summary>
    Failed = 2
}
