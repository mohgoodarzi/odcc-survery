namespace ODCC.Application.Modules.Organization.Dtos;

/// <summary>
/// موقعیت شغلی.
/// </summary>
public sealed record PositionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public Guid OrgUnitId { get; init; }
    public string? OrgUnitName { get; init; }
    public Guid? ReportsToPositionId { get; init; }
    public string? ReportsToTitle { get; init; }
    public int? Grade { get; init; }
    public bool IsActive { get; init; }
    public int? Headcount { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// درخواست ایجاد یا ویرایش موقعیت شغلی.
/// </summary>
public sealed record SavePositionRequest
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public Guid OrgUnitId { get; init; }
    public Guid? ReportsToPositionId { get; init; }
    public int? Grade { get; init; }
    public bool IsActive { get; init; } = true;
    public int? Headcount { get; init; }
    public string? Description { get; init; }
}
