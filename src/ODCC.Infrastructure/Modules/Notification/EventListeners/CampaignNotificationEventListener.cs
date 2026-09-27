using CampaignEntity = ODCC.Domain.Modules.Campaign.Entities.Campaign;
using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Languages;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Entities;
using ODCC.Domain.Modules.Campaign.Events;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Infrastructure.Modules.Notification.EventListeners;

/// <summary>
/// پل بین ماژول کمپین و ماژول اعلان‌ها: تبدیل رویدادهای چرخه‌ی عمر کمپین به
/// پیام‌های واقعی.
///
/// <b>چرا شنونده در ماژول اعلان‌ها است:</b> موجودیت‌های ساخته‌شده
/// (<c>Notification</c>) متعلق به این ماژول‌اند، پس مالکیت طرح حفظ می‌شود
/// (الگوی مجاز: ماژول الف رویدادی منتشر می‌کند و ماژول ب به آن گوش می‌دهد).
/// ماژول کمپین فقط رویداد منتشر می‌کند و از وجود اعلان‌ها بی‌خبر است.
///
/// دو رویداد پردازش می‌شود:
/// <list type="bullet">
///   <item><c>CampaignLaunchedEvent</c>: دعوت‌نامه برای گیرندگان ارسال می‌شود و
///   نتیجه از طریق <c>RecordDistributionResultAsync</c> روی ردیف توزیع ثبت
///   می‌شود.</item>
///   <item><c>ReminderDueEvent</c>: یادآور برای گیرندگانی که هنوز پاسخ نداده‌اند
///   ارسال می‌شود.</item>
/// </list>
///
/// <b>حریم خصوصی:</b> کمپین مخاطب خود را می‌شناسد (کارمندان هدف)، بنابراین
/// ارسال دعوت‌نامه به آن‌ها نقض ناشناس بودن نیست. ناشناس بودن فقط در ماژول
/// پاسخ‌ها اعمال می‌شود (هیچ شناسه‌ی پاسخ‌گو در جداول پاسخ‌ها ذخیره نمی‌شود).
///
/// <b>خطا:</b> شکست این شنونده نباید عملیات اصلی (اجرا/یادآور) را لغو کند؛
/// فقط لاگ می‌شود.
/// </summary>
public sealed class CampaignNotificationEventListener(
    ICampaignRepository campaignRepository,
    IDistributionRepository distributionRepository,
    INotificationRepository notificationRepository,
    INotificationRecipientResolver recipientResolver,
    INotificationService notificationService,
    ICampaignService campaignService,
    ILogger<CampaignNotificationEventListener> logger) :
    IDomainEventListener<CampaignLaunchedEvent>,
    IDomainEventListener<ReminderDueEvent>
{
    private readonly ICampaignRepository _campaignRepository = campaignRepository;
    private readonly IDistributionRepository _distributionRepository = distributionRepository;
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly INotificationRecipientResolver _recipientResolver = recipientResolver;
    private readonly INotificationService _notificationService = notificationService;
    private readonly ICampaignService _campaignService = campaignService;
    private readonly ILogger<CampaignNotificationEventListener> _logger = logger;

    private const string DistributionSourceType = "distribution";

    private static readonly Action<ILogger, Guid, Exception?> CampaignFlowFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(20, "CampaignNotificationFailed"),
        "ارسال اعلان‌های کمپین {CampaignId} ناموفق بود.");

    /// <summary>
    /// ارسال دعوت‌نامه‌های یک کمپین تازه‌راه‌اندازی‌شده. فقط ردیف‌های توزیع
    /// «در صف» پردازش می‌شوند تا این شنونده <b>خودتوان</b> باشد: پردازش مجدد
    /// یک رویداد دعوت‌نامه‌ای دوباره نمی‌فرستد.
    /// </summary>
    public async Task HandleAsync(CampaignLaunchedEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            var (campaign, distributions) = await LoadAsync(
                domainEvent.CampaignId, Domain.Modules.Campaign.Enums.DistributionStatus.Pending, ct);

            if (campaign is null || distributions.Count == 0)
            {
                return;
            }

            // فقط دعوت‌نامه‌هایی که هنوز اعلانی ندارند (محافظت اضافی در برابر
            // پردازش مضاعف).
            distributions = await FilterUndeliveredAsync(distributions, "campaign_invitation", ct);

            if (distributions.Count == 0)
            {
                return;
            }

            var results = await SendCampaignMessagesAsync(campaign, distributions, "campaign_invitation", ct);

            // ثبت نتیجه‌ی ارسال روی ردیف‌های توزیع (قرارداد مستندشده‌ی کمپین).
            foreach (var (distributionId, delivered) in results)
            {
                await _campaignService.RecordDistributionResultAsync(
                    distributionId,
                    success: delivered,
                    failureReason: delivered ? null : "گیرنده کاربر سامانه یا آدرس ایمیل قابل‌استفاده ندارد.",
                    ct);
            }
        }
        catch (Exception ex)
        {
            CampaignFlowFailed(_logger, domainEvent.CampaignId, ex);
        }
    }

    /// <summary>
    /// ارسال یادآور یک کمپین برای گیرندگانی که هنوز پاسخ نداده‌اند. شناسه‌ی
    /// این گیرندگان از رویداد می‌آید تا پرس‌وجوی دوباره با DbContext ماژول
    /// کمپین لازم نباشد.
    /// </summary>
    public async Task HandleAsync(ReminderDueEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _campaignRepository.GetByIdAsync(domainEvent.CampaignId, ct);

            if (campaign is null)
            {
                return;
            }

            var all = await _distributionRepository.ListByCampaignAsync(domainEvent.CampaignId, ct);
            var targetIds = domainEvent.DistributionIds.ToHashSet();

            var distributions = all
                .Where(d => targetIds.Contains(d.Id))
                .ToList();

            if (distributions.Count == 0)
            {
                return;
            }

            // یک گیرنده نباید دو یادآور برای یک یادآور کمپین دریافت کند.
            distributions = await FilterUndeliveredAsync(distributions, "campaign_reminder", ct);

            if (distributions.Count == 0)
            {
                return;
            }

            await SendCampaignMessagesAsync(campaign, distributions, "campaign_reminder", ct);
        }
        catch (Exception ex)
        {
            CampaignFlowFailed(_logger, domainEvent.CampaignId, ex);
        }
    }

    /// <summary>
    /// بارگذاری کمپین و ردیف‌های توزیع در یک وضعیت خاص.
    /// </summary>
    private async Task<(CampaignEntity? campaign, List<Distribution> distributions)> LoadAsync(
        Guid campaignId, Domain.Modules.Campaign.Enums.DistributionStatus status, CancellationToken ct)
    {
        var campaign = await _campaignRepository.GetByIdAsync(campaignId, ct);

        if (campaign is null)
        {
            return (null, []);
        }

        var distributions = (await _distributionRepository.ListByCampaignAsync(campaignId, ct))
            .Where(d => d.Status == status)
            .ToList();

        return (campaign, distributions);
    }

    /// <summary>حذف توزیع‌هایی که از قبل اعلانی با این قالب دارند (idempotency).</summary>
    private async Task<List<Distribution>> FilterUndeliveredAsync(
        List<Distribution> distributions, string templateCode, CancellationToken ct)
    {
        var existing = await _notificationRepository.ListBySourceAsync(
            DistributionSourceType, distributions.Select(d => d.Id).ToList(), ct);

        var delivered = existing
            .Where(n => n.TemplateCode == templateCode)
            .Select(n => n.SourceId)
            .ToHashSet();

        return distributions.Where(d => !delivered.Contains(d.Id)).ToList();
    }

    /// <summary>
    /// ارسال پیام‌های یک کمپین به گیرندگان. کانال به‌ازای هر گیرنده انتخاب
    /// می‌شود: درون‌برنامه‌ای اگر کاربر سامانه دارد، در غیر این صورت ایمیل.
    /// </summary>
    /// <returns>به‌ازای هر توزیع: آیا تحویل امکان‌پذیر بود؟</returns>
    private async Task<List<(Guid distributionId, bool delivered)>> SendCampaignMessagesAsync(
        CampaignEntity campaign, IReadOnlyList<Distribution> distributions, string templateCode, CancellationToken ct)
    {
        var campaignTitle = campaign.Localizations.Pick(Language.Fa)?.Title ?? campaign.Code;
        var link = $"/surveys/{campaign.SurveyId}";

        var employeeIds = distributions.Select(d => d.EmployeeId).Distinct().ToList();
        var recipients = await _recipientResolver.ResolveByEmployeesAsync(employeeIds, ct);
        var byEmployee = recipients.ToDictionary(r => r.EmployeeId!.Value);

        // به‌ازای هر گیرنده، کانال مناسب انتخاب می‌شود.
        var byChannel = new Dictionary<NotificationChannel, List<(Distribution distribution, NotificationRecipient recipient)>>();

        foreach (var distribution in distributions)
        {
            if (!byEmployee.TryGetValue(distribution.EmployeeId, out var recipient))
            {
                continue;
            }

            var channel = recipient.SupportsInApp
                ? NotificationChannel.InApp
                : recipient.SupportsEmail
                    ? NotificationChannel.Email
                    : (NotificationChannel?)null;

            if (channel is null)
            {
                continue;
            }

            if (!byChannel.TryGetValue(channel.Value, out var list))
            {
                byChannel[channel.Value] = list = [];
            }

            list.Add((distribution, recipient));
        }

        foreach (var (channel, group) in byChannel)
        {
            var request = new SendNotificationsRequest
            {
                TemplateCode = templateCode,
                Channel = channel,
                Category = NotificationCategory.Campaign,
                Language = Language.Fa,
                SourceType = DistributionSourceType,
                Url = link,
                Recipients = group.Select(g => new NotificationRecipientDto
                {
                    UserId = g.recipient.UserId,
                    EmployeeId = g.recipient.EmployeeId,
                    Name = g.recipient.Name,
                    Email = g.recipient.Email,
                    Phone = g.recipient.Phone,
                    SourceId = g.distribution.Id,
                    Properties = new Dictionary<string, string?>
                    {
                        ["recipient_name"] = g.recipient.Name,
                        ["survey_title"] = campaignTitle,
                        ["link"] = link
                    }
                }).ToList()
            };

            await _notificationService.SendToManyAsync(request, ct);
        }

        // نتیجه به‌ازای هر توزیع (برای ثبت روی ردیف توزیع): توزیع‌هایی که
        // گیرنده‌ی قابل‌تحویل داشتند موفق‌اند.
        var reachableIds = byChannel.Values.SelectMany(g => g.Select(x => x.distribution.Id)).ToHashSet();

        return distributions
            .Select(d => (distributionId: d.Id, delivered: reachableIds.Contains(d.Id)))
            .ToList();
    }
}
