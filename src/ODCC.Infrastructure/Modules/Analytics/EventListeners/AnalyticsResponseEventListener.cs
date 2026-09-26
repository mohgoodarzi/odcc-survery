using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Response.Events;
using ODCC.Infrastructure.Modules.Analytics.Services;
using Microsoft.Extensions.Logging;

namespace ODCC.Infrastructure.Modules.Analytics.EventListeners;

/// <summary>
/// شنونده‌ی رویداد «ارسال پاسخ» از ماژول پاسخ‌ها: عکس‌العمل تحلیلات نظرسنجی را
/// به‌روزرسانی می‌کند تا داشبوردها بدون محاسبه‌ی دستی به‌روز بمانند.
///
/// **چرا شنونده در ماژول تحلیلات است:** تغییر روی موجودیت <see cref="Domain.Modules.Analytics.Entities.SurveyMetric"/>
/// انجام می‌شود که متعلق به این ماژول است (مالکیت طرح حفظ می‌شود). ماژول پاسخ‌ها
/// فقط رویداد منتشر می‌کند و از وجود تحلیلات بی‌خبر است.
///
/// **حریم خصوصی:** این شنونده از رویداد فقط شناسه‌ی نظرسنجی را می‌گیرد و
/// <b>هرگز</b> شناسه‌ی پاسخ‌گو را پردازش نمی‌کند — حتی در رویدادهای غیرناشناس.
/// شناسه‌ی کاربر پاسخ‌گو عمداً نادیده گرفته می‌شود تا تجمع‌ها فقط بر اساس
/// داده‌ی جمعی محاسبه شوند.
///
/// **خطا:** شکست این شنونده نباید پاسخ ثبت‌شده‌ی کاربر را لغو کند؛ فقط لاگ
/// می‌شود. محاسبه‌ی نهایی در گزارش‌های بعدی قابل پیگیری است.
/// </summary>
public sealed partial class AnalyticsResponseEventListener(
    IAnalyticsService analyticsService,
    ILogger<AnalyticsResponseEventListener> logger) : IDomainEventListener<ResponseSubmittedEvent>
{
    private readonly IAnalyticsService _analyticsService = analyticsService;
    private readonly ILogger<AnalyticsResponseEventListener> _logger = logger;

    public async Task HandleAsync(ResponseSubmittedEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            // محاسبه‌ی عکس‌العمل کلی نظرسنجی. شناسه‌ی پاسخ‌گو عمداً استفاده نمی‌شود
            // تا تجمع بر اساس همه‌ی پاسخ‌ها انجام شود.
            await _analyticsService.ComputeAsync(new ComputeAnalyticsRequest
            {
                SurveyId = domainEvent.SurveyId,
                Filter = new AnalyticsFilter(),
                SegmentType = AnalyticsSegment.Survey,
                IncludeTextAnalytics = false
            }, ct);
        }
        catch (Exception ex)
        {
            ProjectorFailed(_logger, domainEvent.SurveyId, ex);
        }
    }
}
