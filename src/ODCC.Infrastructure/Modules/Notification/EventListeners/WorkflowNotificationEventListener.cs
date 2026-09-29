using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;
using ODCC.Domain.Modules.Workflow.Events;

namespace ODCC.Infrastructure.Modules.Notification.EventListeners;

/// <summary>
/// پل بین ماژول گردش کار و ماژول اعلان‌ها: تبدیل درخواست‌های تأیید به پیام
/// برای کاربرانی که مجوز تأیید را دارند.
///
/// <b>چرا شنونده در ماژول اعلان‌ها است:</b> موجودیت‌های ساخته‌شده
/// (<c>Notification</c>) متعلق به این ماژول‌اند، پس مالکیت طرح حفظ می‌شود
/// (الگوی مجاز: ماژول الف رویدادی منتشر می‌کند و ماژول ب به آن گوش می‌دهد).
/// ماژول گردش کار فقط رویداد منتشر می‌کند و از وجود اعلان‌ها بی‌خبر است.
///
/// <b>خطا:</b> شکست این شنونده نباید عملیات اصلی را لغو کند؛ فقط لاگ می‌شود.
/// </summary>
public sealed class WorkflowNotificationEventListener(
    INotificationService notificationService,
    INotificationRecipientResolver recipientResolver,
    ILogger<WorkflowNotificationEventListener> logger) :
    IDomainEventListener<WorkflowApprovalRequestedEvent>
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly INotificationRecipientResolver _recipientResolver = recipientResolver;
    private readonly ILogger<WorkflowNotificationEventListener> _logger = logger;

    private const string WorkflowSourceType = "workflow_approval";

    private static readonly Action<ILogger, Guid, Exception?> NotificationFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(31, "WorkflowNotificationFailed"),
        "ارسال اعلان درخواست تأیید {ApprovalRequestId} ناموفق بود.");

    /// <summary>
    /// درخواست تأیید: اعلان به همه‌ی کاربرانی که مجوز تأیید را دارند.
    /// </summary>
    public async Task HandleAsync(WorkflowApprovalRequestedEvent domainEvent, CancellationToken ct = default)
    {
        // مجوز لازم برای تأیید این گذار. اگر روی گذار مشخص نشده، مجوز مدیریت
        // گردش کار لازم است.
        var permission = string.IsNullOrWhiteSpace(domainEvent.ApproverPermission)
            ? ODCC.Application.Authorization.Permissions.Workflows.Approve
            : domainEvent.ApproverPermission;

        try
        {
            var recipients = await _recipientResolver.ResolveByPermissionAsync(permission, ct);

            if (recipients.Count == 0)
            {
                // هیچ کاربری مجوز تأیید ندارد — این یک مشکل پیکربندی است، ولی
                // عملیات نباید شکست بخورد. در ممیزی ثبت شده است.
                NotificationFailed(_logger, domainEvent.ApprovalRequestId,
                    new InvalidOperationException($"هیچ کاربری مجوز «{permission}» برای تأیید این گذار ندارد."));
                return;
            }

            foreach (var recipient in recipients.Where(r => r.UserId is not null))
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    TemplateCode = "workflow_approval_requested",
                    Channel = NotificationChannel.InApp,
                    Category = NotificationCategory.Workflow,
                    Language = Language.Fa,
                    SourceType = WorkflowSourceType,
                    SourceId = domainEvent.ApprovalRequestId,
                    Url = $"/workflow/approvals",
                    Recipient = new NotificationRecipientDto
                    {
                        UserId = recipient.UserId,
                        Name = recipient.Name
                    },
                    Properties = new Dictionary<string, string?>
                    {
                        ["recipient_name"] = recipient.Name,
                        ["transition_code"] = domainEvent.TransitionCode,
                        ["from_state"] = domainEvent.FromStateCode,
                        ["to_state"] = domainEvent.ToStateCode,
                        ["expiry"] = domainEvent.ExpiresAt?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                    }
                }, ct);
            }
        }
        catch (Exception ex)
        {
            NotificationFailed(_logger, domainEvent.ApprovalRequestId, ex);
        }
    }
}
