using ODCC.Domain.Common;
using ODCC.Domain.Modules.Survey.Enums;

namespace ODCC.Domain.Modules.Survey.Events;

/// <summary>رویداد دامنه‌ی ایجاد یک نظرسنجی جدید.</summary>
public sealed class SurveyCreatedEvent : DomainEvent
{
    public Guid SurveyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public SurveyCreatedEvent(Guid surveyId, string code, Guid? actorUserId)
    {
        SurveyId = surveyId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی ویرایش یک نظرسنجی.</summary>
public sealed class SurveyUpdatedEvent : DomainEvent
{
    public Guid SurveyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public SurveyUpdatedEvent(Guid surveyId, string code, Guid? actorUserId)
    {
        SurveyId = surveyId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی انتشار یک نظرسنجی (گذار به Scheduled یا Active).</summary>
public sealed class SurveyPublishedEvent : DomainEvent
{
    public Guid SurveyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public SurveyStatus Status { get; init; }
    public Guid? ActorUserId { get; init; }

    public SurveyPublishedEvent(Guid surveyId, string code, SurveyStatus status, Guid? actorUserId)
    {
        SurveyId = surveyId;
        Code = code;
        Status = status;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی شروع پنجره‌ی پاسخ‌گویی یک نظرسنجی.</summary>
public sealed class SurveyStartedEvent : DomainEvent
{
    public Guid SurveyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public SurveyStartedEvent(Guid surveyId, string code, Guid? actorUserId)
    {
        SurveyId = surveyId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی بسته شدن یک نظرسنجی (پایان پاسخ‌گویی).</summary>
public sealed class SurveyClosedEvent : DomainEvent
{
    public Guid SurveyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public SurveyClosedEvent(Guid surveyId, string code, Guid? actorUserId)
    {
        SurveyId = surveyId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی بایگانی یک نظرسنجی.</summary>
public sealed class SurveyArchivedEvent : DomainEvent
{
    public Guid SurveyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public SurveyArchivedEvent(Guid surveyId, string code, Guid? actorUserId)
    {
        SurveyId = surveyId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی ایجاد یک قالب نظرسنجی.</summary>
public sealed class SurveyTemplateCreatedEvent : DomainEvent
{
    public Guid TemplateId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public SurveyTemplateCreatedEvent(Guid templateId, string code, Guid? actorUserId)
    {
        TemplateId = templateId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی بایگانی یک قالب نظرسنجی.</summary>
public sealed class SurveyTemplateArchivedEvent : DomainEvent
{
    public Guid TemplateId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public SurveyTemplateArchivedEvent(Guid templateId, string code, Guid? actorUserId)
    {
        TemplateId = templateId;
        Code = code;
        ActorUserId = actorUserId;
    }
}
