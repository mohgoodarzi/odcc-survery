using ODCC.Application.Authorization;

namespace ODCC.Application.Modules.Identity.Dtos;

/// <summary>
/// خلاصه‌ی یک نقش.
/// </summary>
public sealed record RoleSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public IReadOnlyCollection<string> Permissions { get; init; } = [];
}

/// <summary>
/// درخواست ایجاد یا ویرایش نقش.
/// </summary>
public sealed record SaveRoleRequest
{
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Description { get; init; }

    /// <summary>مجوزهای این نقش (از کاتالوگ <see cref="Permissions"/>).</summary>
    public IReadOnlyCollection<string> Permissions { get; init; } = [];
}

/// <summary>
/// درخواست انتصاب مجوزها به یک نقش.
/// </summary>
public sealed record AssignRolePermissionsRequest
{
    public Guid RoleId { get; init; }
    public IReadOnlyCollection<string> Permissions { get; init; } = [];
}
