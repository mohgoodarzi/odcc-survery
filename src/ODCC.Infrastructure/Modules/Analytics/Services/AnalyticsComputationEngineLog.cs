using Microsoft.Extensions.Logging;

namespace ODCC.Infrastructure.Modules.Analytics.Services;

/// <summary>
/// تعریف لاگ‌های ساختار یافته‌ی موتور محاسبه‌ی تحلیلات (در یک فایل جزئی جداگانه
/// تا تعاریف <c>LoggerMessage</c> از منطق محاسبه جدا بمانند).
/// </summary>
public sealed partial class AnalyticsComputationEngine
{
    private static readonly Action<ILogger, Guid, Exception?> ExplicitUnitDenied = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(1, "AnalyticsExplicitOrgUnitDenied"),
        "درخواست بخش‌بندی واحد سازمانی {OrgUnitId} خارج از دامنه‌ی قابل‌مشاهده‌ی کاربر است؛ نتیجه خالی برمی‌گردد.");

    private static readonly Action<ILogger, Exception?> TextAnalyticsFailed = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(2, "AnalyticsTextAnalyticsFailed"),
        "تحلیل متن پاسخ‌های متنی با خطا مواجه شد؛ این بخش نادیده گرفته می‌شود.");

    private static readonly Action<ILogger, Guid, Exception?> ComputeFailed = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(3, "AnalyticsComputationFailed"),
        "محاسبه‌ی تحلیلات نظرسنجی {SurveyId} ناموفق بود.");

    private static readonly Action<ILogger, Guid, Exception?> ProjectorFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(4, "AnalyticsProjectionFailed"),
        "به‌روزرسانی عکس‌العمل تحلیلات نظرسنجی {SurveyId} پس از ارسال پاسخ ناموفق بود.");
}
