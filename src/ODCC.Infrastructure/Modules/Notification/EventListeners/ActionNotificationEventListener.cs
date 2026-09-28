using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.ActionManagement.Enums;
using ODCC.Domain.Modules.ActionManagement.Events;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Infrastructure.Modules.Notification.EventListeners;

/// <summary>
/// پل بین ماژول مدیریت اقدامات و ماژول اعلان‌ها: تبدیل رویدادهای چرخه‌ی عمر
/// آیتم‌های اقدام به پیام‌های واقعی برای مسئولان و مالکان.
///
/// <b>چرا شنونده در ماژول اعلان‌ها است:</b> موجودیت‌های ساخته‌شده
/// (<c>Notification</c>) متعلق به این ماژول‌اند، پس مالکیت طرح حفظ می‌شود
/// (الگوی مجاز: ماژول الف رویدادی منتشر می‌کند و ماژول ب به آن گوش می‌دهد).
/// ماژول اقدامات فقط رویداد منتشر می‌کند و از وجود اعلان‌ها بی‌خبر است.
///
/// <b>حریم خصوصی:</b> این شنونده هرگز محتوای پاسخ یک پاسخ‌دهنده را ارسال
/// نمی‌کند. فقط متادیتای عمومی آیتم (عنوان، مسئول، مهلت) استفاده می‌شود.
/// اطلاعات پاسخ‌گویان در ماژول پاسخ‌ها محافظت می‌شود.
///
/// <b>خطا:</b> شکست این شنونده نباید عملیات اصلی را لغو کند؛ فقط لاگ می‌شود.
/// </summary>
public sealed class ActionNotificationEventListener(
    INotificationService notificationService,
    INotificationRecipientResolver recipientResolver,
    IActionPlanRepository planRepository,
    ILogger<ActionNotificationEventListener> logger) :
    IDomainEventListener<ActionItemCreatedEvent>,
    IDomainEventListener<ActionItemCompletedEvent>,
    IDomainEventListener<ActionItemReminderDueEvent>,
    IDomainEventListener<ActionItemEscalatedEvent>
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly INotificationRecipientResolver _recipientResolver = recipientResolver;
    private readonly IActionPlanRepository _planRepository = planRepository;
    private readonly ILogger<ActionNotificationEventListener> _logger = logger;

    private const string ActionSourceType = "action_item";

    private static readonly Action<ILogger, Guid, Exception?> ActionFlowFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(30, "ActionNotificationFailed"),
        "ارسال اعلان‌های آیتم اقدام {ItemId} ناموفق بود.");

    /// <summary>انتصاب کار: اعلان به مسئول آیتم.</summary>
    public async Task HandleAsync(ActionItemCreatedEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent.AssigneeUserId is null)
        {
            return;
        }

        await SendAsync(
            domainEvent.AssigneeUserId.Value,
            "action_assigned",
            domainEvent.ItemId,
            domainEvent.PlanId,
            properties: new Dictionary<string, string?>
            {
                ["recipient_name"] = domainEvent.AssigneeUserName,
                ["action_title"] = domainEvent.Title,
                ["plan_title"] = await GetPlanTitleAsync(domainEvent.PlanId, ct),
                ["due_date"] = FormatDate(domainEvent.DueDate),
                ["priority"] = PriorityLabel(domainEvent.Priority)
            },
            ct: ct);
    }

    /// <summary>تکمیل کار: اعلان به مالک برنامه.</summary>
    public async Task HandleAsync(ActionItemCompletedEvent domainEvent, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(domainEvent.PlanId, ct);

        if (plan?.OwnerUserId is null)
        {
            return;
        }

        await SendAsync(
            plan.OwnerUserId.Value,
            "action_completed",
            domainEvent.ItemId,
            domainEvent.PlanId,
            properties: new Dictionary<string, string?>
            {
                ["recipient_name"] = plan.OwnerUserName,
                ["action_title"] = domainEvent.Title,
                ["plan_title"] = plan.Title,
                ["assignee_name"] = domainEvent.AssigneeUserName ?? "نامشخص"
            },
            ct: ct);
    }

    /// <summary>یادآوری سررسید: اعلان به مسئول آیتم.</summary>
    public async Task HandleAsync(ActionItemReminderDueEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent.AssigneeUserId is null)
        {
            return;
        }

        await SendAsync(
            domainEvent.AssigneeUserId.Value,
            "action_reminder",
            domainEvent.ItemId,
            domainEvent.PlanId,
            properties: new Dictionary<string, string?>
            {
                ["recipient_name"] = domainEvent.AssigneeUserName,
                ["action_title"] = domainEvent.Title,
                ["plan_title"] = await GetPlanTitleAsync(domainEvent.PlanId, ct),
                ["due_date"] = FormatDate(domainEvent.DueDate)
            },
            ct: ct);
    }

    /// <summary>
    /// تشدید: اعلان به مسئول آیتم و (در درجات بالاتر) مالک برنامه یا مدیران.
    /// </summary>
    public async Task HandleAsync(ActionItemEscalatedEvent domainEvent, CancellationToken ct = default)
    {
        var recipients = new List<NotificationRecipient>();

        if (domainEvent.AssigneeUserId is { } assigneeId)
        {
            recipients.Add(new NotificationRecipient
            {
                UserId = assigneeId,
                Name = domainEvent.AssigneeUserName
            });
        }

        // درجات بالاتر: مالک برنامه هم مطلع می‌شود.
        if (domainEvent.EscalationLevel >= EscalationLevel.EscalatedToOwner)
        {
            var plan = await _planRepository.GetByIdAsync(domainEvent.PlanId, ct);

            if (plan?.OwnerUserId is { } ownerId && ownerId != domainEvent.AssigneeUserId)
            {
                recipients.Add(new NotificationRecipient
                {
                    UserId = ownerId,
                    Name = plan.OwnerUserName
                });
            }

            // تشدید نهایی: مدیران دارای مجوز مدیریت اقدامات.
            if (domainEvent.EscalationLevel >= EscalationLevel.EscalatedToManagement)
            {
                var managers = await _recipientResolver.ResolveByPermissionAsync(
                    ODCC.Application.Authorization.Permissions.Actions.Manage, ct);

                recipients.AddRange(managers.Where(m =>
                    m.UserId is not null && !recipients.Any(r => r.UserId == m.UserId)));
            }
        }

        if (recipients.Count == 0)
        {
            return;
        }

        var planTitle = await GetPlanTitleAsync(domainEvent.PlanId, ct);

        foreach (var recipient in recipients)
        {
            await SendAsync(
                recipient,
                "action_escalated",
                domainEvent.ItemId,
                domainEvent.PlanId,
                properties: new Dictionary<string, string?>
                {
                    ["recipient_name"] = recipient.Name,
                    ["action_title"] = domainEvent.Title,
                    ["plan_title"] = planTitle,
                    ["assignee_name"] = domainEvent.AssigneeUserName ?? "نامشخص",
                    ["due_date"] = FormatDate(domainEvent.DueDate),
                    ["escalation_level"] = EscalationLabel(domainEvent.EscalationLevel)
                },
                ct: ct);
        }
    }

    // --- کمک‌کننده‌ها ---------------------------------------------------------------

    private async Task SendAsync(
        Guid recipientUserId,
        string templateCode,
        Guid itemId,
        Guid planId,
        Dictionary<string, string?> properties,
        CancellationToken ct)
    {
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                TemplateCode = templateCode,
                Channel = NotificationChannel.InApp,
                Category = NotificationCategory.Action,
                Language = Language.Fa,
                SourceType = ActionSourceType,
                SourceId = itemId,
                Url = $"/actions/plans/{planId}",
                Recipient = new NotificationRecipientDto
                {
                    UserId = recipientUserId,
                    Name = properties.TryGetValue("recipient_name", out var name) ? name : null
                },
                Properties = properties
            }, ct);
        }
        catch (Exception ex)
        {
            ActionFlowFailed(_logger, itemId, ex);
        }
    }

    /// <summary>ارسال به یک گیرنده‌ی ازقبل‌حل‌شده.</summary>
    private async Task SendAsync(
        NotificationRecipient recipient,
        string templateCode,
        Guid itemId,
        Guid planId,
        Dictionary<string, string?> properties,
        CancellationToken ct)
    {
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                TemplateCode = templateCode,
                Channel = NotificationChannel.InApp,
                Category = NotificationCategory.Action,
                Language = Language.Fa,
                SourceType = ActionSourceType,
                SourceId = itemId,
                Url = $"/actions/plans/{planId}",
                Recipient = new NotificationRecipientDto
                {
                    UserId = recipient.UserId,
                    Name = recipient.Name
                },
                Properties = properties
            }, ct);
        }
        catch (Exception ex)
        {
            ActionFlowFailed(_logger, itemId, ex);
        }
    }

    private async Task<string> GetPlanTitleAsync(Guid planId, CancellationToken ct)
    {
        var plan = await _planRepository.GetByIdAsync(planId, ct);

        return plan?.Title ?? string.Empty;
    }

    private static string FormatDate(DateTime? date) =>
        date?.ToUniversalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    private static string PriorityLabel(ActionPriority priority) => priority switch
    {
        ActionPriority.Critical => "بحرانی",
        ActionPriority.High => "بالا",
        ActionPriority.Low => "پایین",
        _ => "متوسط"
    };

    private static string EscalationLabel(EscalationLevel level) => level switch
    {
        EscalationLevel.Reminder => "یادآوری",
        EscalationLevel.EscalatedToOwner => "تشدید به مالک برنامه",
        EscalationLevel.EscalatedToManagement => "تشدید به مدیریت",
        _ => "نامشخص"
    };
}
