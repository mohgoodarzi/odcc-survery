using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Organization.Events;

/// <summary>
/// رویداد دامنه‌ی ایجاد یک واحد سازمانی جدید.
/// این رویداد توسط ماژول سازمان منتشر می‌شود تا ماژول‌های دیگر (مثلاً ممیزی)
/// بدون ارجاع مستقیم به آن از وقوع آن مطلع شوند.
/// </summary>
public sealed class OrgUnitCreatedEvent : DomainEvent
{
    public Guid OrgUnitId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public OrgUnitCreatedEvent(Guid orgUnitId, string code, string path, Guid? actorUserId)
    {
        OrgUnitId = orgUnitId;
        Code = code;
        Path = path;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی به‌روزرسانی یک واحد سازمانی.</summary>
public sealed class OrgUnitUpdatedEvent : DomainEvent
{
    public Guid OrgUnitId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public OrgUnitUpdatedEvent(Guid orgUnitId, string code, string path, Guid? actorUserId)
    {
        OrgUnitId = orgUnitId;
        Code = code;
        Path = path;
        ActorUserId = actorUserId;
    }
}

/// <summary>رویداد دامنه‌ی حذف نرم یک واحد سازمانی.</summary>
public sealed class OrgUnitDeletedEvent : DomainEvent
{
    public Guid OrgUnitId { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }

    public OrgUnitDeletedEvent(Guid orgUnitId, string code, Guid? actorUserId)
    {
        OrgUnitId = orgUnitId;
        Code = code;
        ActorUserId = actorUserId;
    }
}
