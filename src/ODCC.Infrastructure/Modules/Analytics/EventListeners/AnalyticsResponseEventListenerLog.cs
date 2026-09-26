using Microsoft.Extensions.Logging;

namespace ODCC.Infrastructure.Modules.Analytics.EventListeners;

/// <summary>
/// تعریف لاگ‌های ساختار یافته‌ی شنونده‌ی تحلیلات (در یک فایل جزئی جداگانه).
/// </summary>
public sealed partial class AnalyticsResponseEventListener
{
    private static readonly Action<ILogger, Guid, Exception?> ProjectorFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(1, "AnalyticsProjectionFailed"),
        "به‌روزرسانی عکس‌العمل تحلیلات نظرسنجی {SurveyId} پس از ارسال پاسخ ناموفق بود.");
}
