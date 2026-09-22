using ODCC.Domain.Modules.Organization.Enums;

namespace ODCC.Application.Modules.Organization.Dtos;

/// <summary>
/// واحد سازمانی.
/// </summary>
public sealed record OrgUnitDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public OrgUnitType Type { get; init; }
    public Guid? ParentId { get; init; }
    public string? ParentName { get; init; }
    public string Path { get; init; } = string.Empty;
    public int Level { get; init; }
    public bool IsActive { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
    public string? ManagerFullName { get; init; }

    /// <summary>تعداد کارمندان مستقیم این واحد (در صورت موجود بودن).</summary>
    public int EmployeeCount { get; init; }
}

/// <summary>
/// گره درخت سازمانی (برای نمایش درختی).
/// </summary>
public sealed record OrgUnitTreeDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public OrgUnitType Type { get; init; }
    public int Level { get; init; }
    public bool IsActive { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
    public string? ManagerFullName { get; init; }
    public IReadOnlyList<OrgUnitTreeDto> Children { get; init; } = [];
}

/// <summary>
/// درخواست ایجاد یا ویرایش واحد سازمانی.
/// </summary>
public sealed record SaveOrgUnitRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public OrgUnitType Type { get; init; }
    public Guid? ParentId { get; init; }
    public bool IsActive { get; init; } = true;
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
}
