using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Domain.Modules.Campaign.Enums;
using ODCC.Domain.Modules.Response.Events;

namespace ODCC.Infrastructure.Modules.Campaign.EventListeners;

/// <summary>
/// شنونده‌ی رویداد «ارسال پاسخ» از ماژول پاسخ‌ها: دعوت‌نامه‌ای که از طریق آن
/// این پاسخ ثبت شده را به‌عنوان «پاسخ‌داده» علامت می‌زند.
///
/// **چرا شنونده در ماژول کمپین است:** تغییر روی موجودیت <see cref="Domain.Modules.Campaign.Entities.Distribution"/>
/// انجام می‌شود که متعلق به این ماژول است (مالکیت طرح حفظ می‌شود). ماژول پاسخ‌ها
/// فقط رویداد منتشر می‌کند و از وجود کمپین بی‌خبر است.
///
/// **چرا شناسه‌ی توزیع از رویداد می‌آید:** در نظرسنجی‌های ناشناس، شناسه‌ی
/// دعوت‌نامه در پایگاه داده‌ی پاسخ‌ها ذخیره نمی‌شود تا پیوند میان پاسخ و
/// پاسخ‌دهنده قطع بماند. این شناسه فقط به‌صورت گذرا در رویداد دامنه منتقل
/// می‌شود و این‌جا پردازش می‌شود.
///
/// **خطا:** شکست این شنونده نباید پاسخ ثبت‌شده‌ی کاربر را لغو کند؛ فقط لاگ
/// می‌شود. تطبیق نهایی دعوت‌نامه در گزارش‌های توزین دیگری قابل پیگیری است.
/// </summary>
public sealed class CampaignResponseEventListener(
    IDistributionRepository distributionRepository,
    ICampaignUnitOfWork unitOfWork,
    ILogger<CampaignResponseEventListener> logger) : IDomainEventListener<ResponseSubmittedEvent>
{
    private readonly IDistributionRepository _distributionRepository = distributionRepository;
    private readonly ICampaignUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CampaignResponseEventListener> _logger = logger;

    private static readonly Action<ILogger, Guid, Exception?> MarkFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(9, "DistributionMarkFailed"),
        "علامت‌زدن دعوت‌نامه {DistributionId} به‌عنوان پاسخ‌داده ناموفق بود.");

    private static readonly Action<ILogger, Exception?> NoDistribution = LoggerMessage.Define(
        LogLevel.Debug,
        new EventId(10, "NoDistribution"),
        "رویداد ارسال پاسخ شناسه‌ی توزیعی نداشت یا دعوت‌نامه یافت نشد.");

    public async Task HandleAsync(ResponseSubmittedEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent.DistributionId is not { } distributionId)
        {
            NoDistribution(_logger, null);
            return;
        }

        try
        {
            var distribution = await _distributionRepository.GetByIdAsync(distributionId, ct);
            if (distribution is null)
            {
                NoDistribution(_logger, null);
                return;
            }

            // دعوت‌نامه‌ای که قبلاً پاسخ داده شده دوباره به‌روزرسانی نمی‌شود
            // (ویرایش مجدد پاسخ رویداد جدیدی منتشر می‌کند).
            if (distribution.Status == DistributionStatus.Responded)
            {
                return;
            }

            distribution.MarkResponded();

            _distributionRepository.Update(distribution);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            MarkFailed(_logger, distributionId, ex);
        }
    }
}
