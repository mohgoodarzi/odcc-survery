using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Campaign.Events;

/// <summary>رویداد دامنه‌ی ایجاد یک کمپین جدید.</summary>
public sealed class CampaignCreatedEvent : DomainEvent
{
    public Guid CampaignId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid SurveyId { get; init; }
    public Guid? ActorUserId { get; init; }

    public CampaignCreatedEvent(Guid campaignId, string code, Guid surveyId, Guid? actorUserId)
    {
        CampaignId = campaignId;
        Code = code;
        SurveyId = surveyId;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی ویرایش یک کمپین.</summary>
public sealed class CampaignUpdatedEvent : DomainEvent
{
    public Guid CampaignId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public CampaignUpdatedEvent(Guid campaignId, string code, Guid? actorUserId)
    {
        CampaignId = campaignId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی زمان‌بندی یک کمپین.</summary>
public sealed class CampaignScheduledEvent : DomainEvent
{
    public Guid CampaignId { get; init; }
    public string Code { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
    public Guid? ActorUserId { get; init; }

    public CampaignScheduledEvent(Guid campaignId, string code, DateTime scheduledAt, Guid? actorUserId)
    {
        CampaignId = campaignId;
        Code = code;
        ScheduledAt = scheduledAt;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی اجرای یک کمپین و ساخت ردیف‌های توزیع.</summary>
public sealed class CampaignLaunchedEvent : DomainEvent
{
    public Guid CampaignId { get; init; }
    public string Code { get; init; } = string.Empty;
    public int RecipientCount { get; init; }
    public Guid? ActorUserId { get; init; }

    public CampaignLaunchedEvent(Guid campaignId, string code, int recipientCount, Guid? actorUserId)
    {
        CampaignId = campaignId;
        Code = code;
        RecipientCount = recipientCount;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی تکمیل یک کمپین.</summary>
public sealed class CampaignCompletedEvent : DomainEvent
{
    public Guid CampaignId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public CampaignCompletedEvent(Guid campaignId, string code, Guid? actorUserId)
    {
        CampaignId = campaignId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی بایگانی یک کمپین.</summary>
public sealed class CampaignArchivedEvent : DomainEvent
{
    public Guid CampaignId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public CampaignArchivedEvent(Guid campaignId, string code, Guid? actorUserId)
    {
        CampaignId = campaignId;
        Code = code;
        ActorUserId = actorUserId;
    }
}
