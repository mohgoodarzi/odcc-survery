using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;

namespace ODCC.Domain.Modules.QuestionBank.Events;

/// <summary>
/// رویداد دامنه‌ی ایجاد یک سؤال جدید در کتابخانه‌ی سؤالات.
/// توسط ماژول کتابخانه‌ی سؤالات منتشر می‌شود تا ماژول‌های دیگر (مثلاً ممیزی)
/// بدون ارجاع مستقیم از وقوع آن مطلع شوند.
/// </summary>
public sealed class QuestionCreatedEvent : DomainEvent
{
    public Guid QuestionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public QuestionType Type { get; init; }
    public Guid? ActorUserId { get; init; }

    public QuestionCreatedEvent(Guid questionId, string code, QuestionType type, Guid? actorUserId)
    {
        QuestionId = questionId;
        Code = code;
        Type = type;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی ویرایش یک سؤال.</summary>
public sealed class QuestionUpdatedEvent : DomainEvent
{
    public Guid QuestionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public int VersionNumber { get; init; }
    public Guid? ActorUserId { get; init; }

    public QuestionUpdatedEvent(Guid questionId, string code, int versionNumber, Guid? actorUserId)
    {
        QuestionId = questionId;
        Code = code;
        VersionNumber = versionNumber;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی بایگانی یک سؤال.</summary>
public sealed class QuestionArchivedEvent : DomainEvent
{
    public Guid QuestionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public QuestionArchivedEvent(Guid questionId, string code, Guid? actorUserId)
    {
        QuestionId = questionId;
        Code = code;
        ActorUserId = actorUserId;
    }
}
