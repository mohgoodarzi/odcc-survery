using ODCC.Domain.Common;
using ODCC.Domain.Modules.Questionnaire.Enums;

namespace ODCC.Domain.Modules.Questionnaire.Events;

/// <summary>
/// رویداد دامنه‌ی ایجاد یک پرسشنامه جدید.
/// </summary>
public sealed class QuestionnaireCreatedEvent : DomainEvent
{
    public Guid QuestionnaireId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public QuestionnaireCreatedEvent(Guid questionnaireId, string code, Guid? actorUserId)
    {
        QuestionnaireId = questionnaireId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی ویرایش یک پرسشنامه.</summary>
public sealed class QuestionnaireUpdatedEvent : DomainEvent
{
    public Guid QuestionnaireId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public QuestionnaireUpdatedEvent(Guid questionnaireId, string code, Guid? actorUserId)
    {
        QuestionnaireId = questionnaireId;
        Code = code;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی انتشار یک پرسشنامه (فعال‌سازی برای استفاده در نظرسنجی‌ها).</summary>
public sealed class QuestionnairePublishedEvent : DomainEvent
{
    public Guid QuestionnaireId { get; init; }
    public string Code { get; init; } = string.Empty;
    public int Version { get; init; }
    public Guid? ActorUserId { get; init; }

    public QuestionnairePublishedEvent(Guid questionnaireId, string code, int version, Guid? actorUserId)
    {
        QuestionnaireId = questionnaireId;
        Code = code;
        Version = version;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی بایگانی یک پرسشنامه.</summary>
public sealed class QuestionnaireArchivedEvent : DomainEvent
{
    public Guid QuestionnaireId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public QuestionnaireArchivedEvent(Guid questionnaireId, string code, Guid? actorUserId)
    {
        QuestionnaireId = questionnaireId;
        Code = code;
        ActorUserId = actorUserId;
    }
}
