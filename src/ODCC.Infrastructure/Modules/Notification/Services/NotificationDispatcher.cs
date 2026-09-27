using NotificationEntity = ODCC.Domain.Modules.Notification.Entities.Notification;
using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Domain.Modules.Notification.Entities;
using ODCC.Domain.Modules.Notification.Enums;
using ODCC.Domain.Modules.Notification.Events;

namespace ODCC.Infrastructure.Modules.Notification.Services;

/// <summary>
/// موتور پردازش اعلان‌های در صف: بررسی ترجیح کاربر، فراخوانی ارائه‌دهنده‌ی
/// کانال متناظر و به‌روزرسانی وضعیت (با امتحان مجدد نمایی).
///
/// این کلاس هم بعد از <c>SendAsync</c> (تحویل بلافاصله) و هم توسط زمان‌بند
/// پس‌زمینه (تحویل به‌تعویق‌افتاده و امتحان مجدد) استفاده می‌شود تا یک مسیر
/// واحد برای تبدیل «اعلان در صف» به «اعلان تحویل‌شده» وجود داشته باشد.
/// </summary>
public sealed class NotificationDispatcher(
    INotificationRepository notificationRepository,
    INotificationPreferenceRepository preferenceRepository,
    IEnumerable<INotificationDeliveryProvider> providers,
    INotificationUnitOfWork unitOfWork,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly INotificationPreferenceRepository _preferenceRepository = preferenceRepository;
    private readonly Dictionary<NotificationChannel, INotificationDeliveryProvider> _providers = providers
        .ToDictionary(p => p.Channel);
    private readonly INotificationUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<NotificationDispatcher> _logger = logger;

    private static readonly Action<ILogger, Guid, string, Exception?> DeliveryFailed = LoggerMessage.Define<Guid, string>(
        LogLevel.Warning,
        new EventId(3, "NotificationDeliveryFailed"),
        "تحویل اعلان {NotificationId} شکست خورد: {Reason}");

    /// <inheritdoc/>
    public async Task<int> ProcessPendingAsync(int maxBatch, CancellationToken ct = default)
    {
        var pending = await _notificationRepository.ListPendingAsync(maxBatch, ct);

        if (pending.Count == 0)
        {
            return 0;
        }

        var processed = 0;

        foreach (var notification in pending)
        {
            await DeliverOneAsync(notification, ct);
            processed++;
        }

        // تمام تغییرات وضعیت در یک تراکنش ذخیره می‌شوند.
        await _unitOfWork.SaveChangesAsync(ct);

        return processed;
    }

    private async Task DeliverOneAsync(NotificationEntity notification, CancellationToken ct)
    {
        // ۱. ترجیبات کاربر: اعلان‌های سیستمی (دسته‌ی General) هرگز مسدود نمی‌شوند.
        if (notification.Category != NotificationCategory.General
            && notification.RecipientUserId is { } userId
            && !await IsDeliveryAllowedAsync(userId, notification.Channel, notification.Category, ct))
        {
            notification.MarkSuppressed();
            return;
        }

        // ۲. ارائه‌دهنده‌ی کانال متناظر.
        if (!_providers.TryGetValue(notification.Channel, out var provider))
        {
            notification.MarkFailed($"ارائه‌دهنده‌ای برای کانال {notification.Channel} ثبت نشده است.");
            RaiseFailureEvent(notification);
            return;
        }

        var request = new NotificationDeliveryRequest
        {
            NotificationId = notification.Id,
            RecipientUserId = notification.RecipientUserId,
            RecipientName = notification.RecipientName,
            RecipientEmail = notification.RecipientEmail,
            RecipientPhone = notification.RecipientPhone,
            Subject = notification.Subject,
            Body = notification.Body
        };

        DeliveryAttemptResult attempt;

        try
        {
            attempt = await provider.DeliverAsync(request, ct);
        }
        catch (Exception ex)
        {
            // خطای غیرمنتظره ارائه‌دهنده: گذرا فرض می‌شود تا امتحان مجدد ممکن بماند.
            attempt = DeliveryAttemptResult.Failed($"خطای غیرمنتظره در ارائه‌دهنده: {ex.Message}", isPermanent: false);
        }

        // ۳. به‌روزرسانی وضعیت بر اساس نتیجه.
        if (attempt.Success)
        {
            if (attempt.IsConfirmed)
            {
                notification.MarkDelivered(attempt.ProviderMessageId);
            }
            else
            {
                notification.MarkSent(attempt.ProviderMessageId);
            }

            notification.RaiseDomainEvent(new NotificationDeliveredEvent(
                notification.Id,
                notification.RecipientUserId,
                notification.Channel,
                attempt.ProviderMessageId));
        }
        else if (attempt.IsPermanentFailure || !notification.CanRetry)
        {
            notification.MarkFailed(attempt.FailureReason ?? "خطای نامشخص ارسال");
            RaiseFailureEvent(notification);
        }
        else
        {
            // شکست گذرا: تلاش بعدی با تأخیر نمایی برنامه‌ریزی می‌شود.
            notification.ScheduleRetry(attempt.FailureReason ?? "خطای نامشخص ارسال");
            DeliveryFailed(_logger, notification.Id, notification.LastError!, null);
        }
    }

    private void RaiseFailureEvent(NotificationEntity notification)
    {
        notification.RaiseDomainEvent(new NotificationFailedEvent(
            notification.Id,
            notification.RecipientUserId,
            notification.Channel,
            notification.LastError ?? "خطای نامشخص ارسال"));

        DeliveryFailed(_logger, notification.Id, notification.LastError!, null);
    }

    /// <summary>
    /// آیا تحویل به این کاربر در این کانال/دسته مجاز است؟ اولویت: ترجیح خاص دسته،
    /// سپس ترجیب عمومی کانال. نبودن ردیف یعنی مجاز (پیش‌فرض روشن).
    /// </summary>
    private async Task<bool> IsDeliveryAllowedAsync(
        Guid userId, NotificationChannel channel, NotificationCategory category, CancellationToken ct)
    {
        var specific = await _preferenceRepository.FindAsync(userId, channel, category, ct);

        if (specific is not null)
        {
            return specific.IsEnabled;
        }

        var general = await _preferenceRepository.FindAsync(userId, channel, category: null, ct);

        return general is null || general.IsEnabled;
    }
}
