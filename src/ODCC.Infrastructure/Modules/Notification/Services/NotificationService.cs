using NotificationEntity = ODCC.Domain.Modules.Notification.Entities.Notification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Entities;
using ODCC.Domain.Modules.Notification.Enums;
using ODCC.Domain.Modules.Notification.Events;

namespace ODCC.Infrastructure.Modules.Notification.Services;

/// <summary>
/// سرویس اعلان‌ها: ساخت، تحویل و مدیریت صندوق ورودی کاربر جاری.
///
/// <b>مرز حریم خصوصی:</b> کاربر فقط اعلان‌های <i>خودش</i> را می‌بیند و فقط
/// می‌تواند اعلان‌های خودش را خوانده‌شده علامت بزند. این اجبار در این سرویس
/// است (نه در کنترلر) تا هیچ مسیری دور نزند. مدیران با مجوز
/// <c>notifications.manage</c> می‌توانند اعلان‌های همه را ببینند (فقط خواندن).
/// </summary>
public sealed class NotificationService(
    INotificationRepository notificationRepository,
    INotificationTemplateRenderer templateRenderer,
    INotificationDispatcher dispatcher,
    ICurrentUserService currentUserService,
    INotificationUnitOfWork unitOfWork,
    IOptions<NotificationDeliveryOptions> deliveryOptions,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly INotificationTemplateRenderer _templateRenderer = templateRenderer;
    private readonly INotificationDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly INotificationUnitOfWork _unitOfWork = unitOfWork;
    private readonly NotificationDeliveryOptions _deliveryOptions = deliveryOptions.Value;
    private readonly ILogger<NotificationService> _logger = logger;

    /// <inheritdoc/>
    public async Task<Result<NotificationDto>> SendAsync(SendNotificationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rendered = await _templateRenderer.RenderAsync(request.TemplateCode, request.Language, request.Properties, ct);

        var notification = BuildNotification(request, request.Recipient, rendered);

        notification.RaiseDomainEvent(new NotificationCreatedEvent(
            notification.Id,
            notification.RecipientUserId,
            notification.RecipientName,
            notification.Channel,
            notification.Category,
            notification.TemplateCode,
            notification.Subject,
            notification.SourceType,
            notification.SourceId));

        await _notificationRepository.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // تحویل بلافاصله: اعلان در صف قرار گرفته و همان‌جا پردازش می‌شود.
        // اگر تحویل ناموفق باشد، وضعیت برای امتحان مجدد زمان‌بندی می‌شود و
        // زمان‌بند پس‌زمینه بعداً دوباره تلاش می‌کند.
        await _dispatcher.ProcessPendingAsync(maxBatch: 1, ct);

        return Result.Success(ToDto(notification));
    }

    /// <inheritdoc/>
    public async Task<Result<int>> SendToManyAsync(SendNotificationsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rendered = await _templateRenderer.RenderAsync(request.TemplateCode, request.Language, request.Properties, ct);

        var notifications = new List<NotificationEntity>(request.Recipients.Count);

        foreach (var recipient in request.Recipients)
        {
            // متغیرهای اختصاصی گیرنده با متغیرهای مشترک ادغام می‌شوند (دومی اولویت ندارد).
            var properties = MergeProperties(request.Properties, recipient.Properties);
            var recipientRendered = await _templateRenderer.RenderAsync(request.TemplateCode, request.Language, properties, ct);

            notifications.Add(BuildNotification(request, recipient, recipientRendered));
        }

        // رویداد برای هر اعلان به‌صورت جداگانه منتشر می‌شود تا ممیزی دقیق بماند،
        // ولی همه در یک تراکنش ذخیره می‌شوند.
        foreach (var notification in notifications)
        {
            notification.RaiseDomainEvent(new NotificationCreatedEvent(
                notification.Id,
                notification.RecipientUserId,
                notification.RecipientName,
                notification.Channel,
                notification.Category,
                notification.TemplateCode,
                notification.Subject,
                notification.SourceType,
                notification.SourceId));
        }

        await _notificationRepository.AddRangeAsync(notifications, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // تحویل بلافاصله‌ی دسته‌ای.
        await _dispatcher.ProcessPendingAsync(maxBatch: notifications.Count, ct);

        return Result.Success(notifications.Count);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<NotificationDto>> SearchAsync(NotificationSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // کاربر جاری فقط اعلان‌های خودش را می‌بیند مگر اینکه مجوز مدیریت داشته باشد.
        var effectiveRequest = ApplyOwnershipFilter(request);

        var totalCount = await _notificationRepository.CountAsync(effectiveRequest, ct);
        var notifications = await _notificationRepository.SearchAsync(effectiveRequest, ct);

        return new PagedResult<NotificationDto>
        {
            Items = notifications.Select(ToDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(id, ct);

        if (notification is null)
        {
            return Result.Failure<NotificationDto>("notification_not_found", "اعلان یافت نشد.");
        }

        if (!IsOwnerOrManager(notification))
        {
            return Result.Failure<NotificationDto>("notification_not_found", "اعلان یافت نشد.");
        }

        return Result.Success(ToDto(notification));
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationDto>> MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(id, ct);

        if (notification is null)
        {
            return Result.Failure<NotificationDto>("notification_not_found", "اعلان یافت نشد.");
        }

        // فقط مالک می‌تواند اعلان خودش را خوانده‌شده علامت بزند.
        if (notification.RecipientUserId != _currentUserService.UserId)
        {
            return Result.Failure<NotificationDto>("notification_not_owner", "شما فقط می‌توانید اعلان‌های خودتان را علامت بزنید.");
        }

        notification.MarkRead();

        notification.RaiseDomainEvent(new NotificationReadEvent(notification.Id, notification.RecipientUserId!.Value));

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(notification));
    }

    /// <inheritdoc/>
    public async Task<Result> MarkAllReadAsync(NotificationChannel? channel, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;

        if (userId is null)
        {
            return Result.Failure("not_authenticated", "کاربر احراز هویت نشده است.");
        }

        var unreadIds = await _notificationRepository.ListUnreadIdsAsync(userId.Value, channel, ct);

        foreach (var id in unreadIds)
        {
            var notification = await _notificationRepository.GetForDeliveryAsync(id, ct);

            if (notification is null || notification.RecipientUserId != userId)
            {
                continue;
            }

            notification.MarkRead();

            notification.RaiseDomainEvent(new NotificationReadEvent(notification.Id, userId.Value));
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;

        if (userId is null)
        {
            return 0;
        }

        return await _notificationRepository.CountUnreadAsync(userId.Value, ct);
    }

    // --- کمکی‌ها --------------------------------------------------------------

    private NotificationEntity BuildNotification(
        SendNotificationRequest request,
        NotificationRecipientDto recipient,
        RenderedTemplate rendered)
    {
        return BuildNotification(
            request.TemplateCode, request.Channel, request.Category, request.Language,
            request.SourceType, request.Url, recipient, rendered);
    }

    private NotificationEntity BuildNotification(
        SendNotificationsRequest request,
        NotificationRecipientDto recipient,
        RenderedTemplate rendered)
    {
        return BuildNotification(
            request.TemplateCode, request.Channel, request.Category, request.Language,
            request.SourceType, request.Url, recipient, rendered);
    }

    private NotificationEntity BuildNotification(
        string templateCode,
        NotificationChannel channel,
        NotificationCategory category,
        Language language,
        string? sourceType,
        string? url,
        NotificationRecipientDto recipient,
        RenderedTemplate rendered)
    {
        return new NotificationEntity
        {
            RecipientUserId = recipient.UserId,
            RecipientName = recipient.Name,
            RecipientEmail = recipient.Email,
            RecipientPhone = recipient.Phone,
            Channel = channel,
            Category = category,
            Status = NotificationStatus.Pending,
            TemplateCode = templateCode,
            Subject = rendered.Subject,
            Body = rendered.Body,
            Language = language,
            MaxRetries = _deliveryOptions.MaxRetries,
            SourceType = sourceType,
            SourceId = recipient.SourceId,
            Url = url
        };
    }

    private NotificationSearchRequest ApplyOwnershipFilter(NotificationSearchRequest request)
    {
        if (_currentUserService.HasPermission(Permissions.Notifications.Manage))
        {
            return request;
        }

        // کاربر عادی: همیشه به اعلان‌های خودش محدود می‌شود، حتی اگر درخواست
        // شناسه‌ی کاربر دیگری فرستاده باشد.
        return request with { RecipientUserId = _currentUserService.UserId };
    }

    private bool IsOwnerOrManager(NotificationEntity notification)
    {
        if (_currentUserService.HasPermission(Permissions.Notifications.Manage))
        {
            return true;
        }

        return notification.RecipientUserId == _currentUserService.UserId;
    }

    private static IReadOnlyDictionary<string, string?> MergeProperties(
        IReadOnlyDictionary<string, string?> shared,
        IReadOnlyDictionary<string, string?> perRecipient)
    {
        if (perRecipient.Count == 0)
        {
            return shared;
        }

        var merged = new Dictionary<string, string?>(shared.Count + perRecipient.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in shared)
        {
            merged[pair.Key] = pair.Value;
        }

        foreach (var pair in perRecipient)
        {
            // متغیرهای اختصاصی گیرنده اولویت دارند.
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static NotificationDto ToDto(NotificationEntity n) => new()
    {
        Id = n.Id,
        RecipientUserId = n.RecipientUserId,
        RecipientName = n.RecipientName,
        Channel = n.Channel,
        Category = n.Category,
        Status = n.Status,
        TemplateCode = n.TemplateCode,
        Subject = n.Subject,
        Body = n.Body,
        Language = n.Language,
        NextTryAt = n.NextTryAt,
        SentAt = n.SentAt,
        DeliveredAt = n.DeliveredAt,
        ReadAt = n.ReadAt,
        RetryCount = n.RetryCount,
        LastError = n.LastError,
        SourceType = n.SourceType,
        SourceId = n.SourceId,
        Url = n.Url,
        CreatedAt = n.CreatedAt,
        IsUnread = n.IsUnread
    };
}
