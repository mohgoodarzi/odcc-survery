namespace ODCC.Application.Modules.Audit.Dtos;

/// <summary>
/// خروجی استاندارد یک رخداد ممیزی برای رابط کاربری و گزارش‌ها.
/// </summary>
public sealed record AuditEntryDto
{
    public Guid Id { get; init; }

    /// <summary>زمان رخداد بر حسب UTC.</summary>
    public DateTime OccurredAt { get; init; }

    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string? ClientIp { get; init; }
    public string? Description { get; init; }
}

/// <summary>فیلتر صفحه‌بندی‌شده‌ی گزارش ممیزی.</summary>
public sealed record AuditSearchRequest(
    string? EntityType,
    string? Action,
    Guid? UserId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 20);
