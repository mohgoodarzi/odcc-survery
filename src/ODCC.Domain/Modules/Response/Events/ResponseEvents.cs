using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Response.Events;

/// <summary>
/// رویداد دامنه‌ی شروع یک نشست پاسخ‌گویی.
/// </summary>
public sealed class ResponseStartedEvent : DomainEvent
{
    public Guid SessionId { get; init; }
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public Guid? CampaignId { get; init; }
    public Guid? DistributionId { get; init; }
    public bool IsAnonymous { get; init; }
    public Guid? ActorUserId { get; init; }

    public ResponseStartedEvent(
        Guid sessionId,
        Guid surveyId,
        string surveyCode,
        Guid? campaignId,
        Guid? distributionId,
        bool isAnonymous,
        Guid? actorUserId)
    {
        SessionId = sessionId;
        SurveyId = surveyId;
        SurveyCode = surveyCode;
        CampaignId = campaignId;
        DistributionId = distributionId;
        IsAnonymous = isAnonymous;
        ActorUserId = actorUserId;
    }
}

/// <summary>
/// رویداد دامنه‌ی ارسال نهایی یک پاسخ.
///
/// **چرا DistributionId فقط اینجاست:** شناسه‌ی توزیع (دعوت‌نامه‌ی شخصی) برای
/// نظرسنجی‌های ناشناس در پایگاه داده‌ی پاسخ‌ها ذخیره نمی‌شود تا پیوند میان
/// پاسخ و پاسخ‌دهنده قطع بماند. ولی ماژول کمپین باید بداند دعوت‌نامه پاسخ
/// داده شده تا آن را «پاسخ‌داده» علامت بزند؛ این شناسه فقط به‌صورت گذرا در این
/// رویداد منتقل می‌شود و شنونده‌ی کمپین آن را پردازش می‌کند.
/// </summary>
public sealed class ResponseSubmittedEvent : DomainEvent
{
    public Guid SessionId { get; init; }
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public Guid? CampaignId { get; init; }
    public Guid? DistributionId { get; init; }
    public bool IsAnonymous { get; init; }
    public int AnswerCount { get; init; }
    public Guid? ActorUserId { get; init; }

    public ResponseSubmittedEvent(
        Guid sessionId,
        Guid surveyId,
        string surveyCode,
        Guid? campaignId,
        Guid? distributionId,
        bool isAnonymous,
        int answerCount,
        Guid? actorUserId)
    {
        SessionId = sessionId;
        SurveyId = surveyId;
        SurveyCode = surveyCode;
        CampaignId = campaignId;
        DistributionId = distributionId;
        IsAnonymous = isAnonymous;
        AnswerCount = answerCount;
        ActorUserId = actorUserId;
    }
}
