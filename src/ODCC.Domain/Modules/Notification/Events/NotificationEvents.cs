using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Domain.Modules.Notification.Events;

/// <summary>
/// اعلانی در صف تحویل قرار گرفت (یا تحویل آن بلافاصله انجام شد).
/// شنونده‌ی ممیزی این رویداد را ثبت می‌کند.
/// </summary>
public sealed class NotificationCreatedEvent : DomainEvent
{
    public Guid NotificationId { get; init; }
    public Guid? RecipientUserId { get; init; }
    public string? RecipientName { get; init; }
    public NotificationChannel Channel { get; init; }
    public NotificationCategory Category { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string? SourceType { get; init; }
    public Guid? SourceId { get; init; }

    public NotificationCreatedEvent(
        Guid notificationId,
        Guid? recipientUserId,
        string? recipientName,
        NotificationChannel channel,
        NotificationCategory category,
        string templateCode,
        string subject,
        string? sourceType,
        Guid? sourceId)
    {
        NotificationId = notificationId;
        RecipientUserId = recipientUserId;
        RecipientName = recipientName;
        Channel = channel;
        Category = category;
        TemplateCode = templateCode;
        Subject = subject;
        SourceType = sourceType;
        SourceId = sourceId;
    }
}

/// <summary>
/// تحویل یک اعلان با موفقیت انجام شد. ماژول‌های آینده می‌توانند به آن گوش دهند
/// (مثلاً برای ثبت نرخ باز شدن).
/// </summary>
public sealed class NotificationDeliveredEvent : DomainEvent
{
    public Guid NotificationId { get; init; }
    public Guid? RecipientUserId { get; init; }
    public NotificationChannel Channel { get; init; }
    public string? ProviderMessageId { get; init; }

    public NotificationDeliveredEvent(
        Guid notificationId,
        Guid? recipientUserId,
        NotificationChannel channel,
        string? providerMessageId)
    {
        NotificationId = notificationId;
        RecipientUserId = recipientUserId;
        Channel = channel;
        ProviderMessageId = providerMessageId;
    }
}

/// <summary>
/// تحویل یک اعلان پس از اتمام تمام امتحان‌ها شکست خورد.
/// </summary>
public sealed class NotificationFailedEvent : DomainEvent
{
    public Guid NotificationId { get; init; }
    public Guid? RecipientUserId { get; init; }
    public NotificationChannel Channel { get; init; }
    public string LastError { get; init; } = string.Empty;

    public NotificationFailedEvent(
        Guid notificationId,
        Guid? recipientUserId,
        NotificationChannel channel,
        string lastError)
    {
        NotificationId = notificationId;
        RecipientUserId = recipientUserId;
        Channel = channel;
        LastError = lastError;
    }
}

/// <summary>
/// گیرنده یک اعلان درون‌برنامه‌ای را خواند.
/// </summary>
public sealed class NotificationReadEvent : DomainEvent
{
    public Guid NotificationId { get; init; }
    public Guid RecipientUserId { get; init; }

    public NotificationReadEvent(Guid notificationId, Guid recipientUserId)
    {
        NotificationId = notificationId;
        RecipientUserId = recipientUserId;
    }
}
