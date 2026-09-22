using Microsoft.AspNetCore.Authorization;

namespace ODCC.Api.Authorization;

/// <summary>
/// نیازمندی یک مجوز مشخص برای اجرای عملیات.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Permission = permission;
    }
}
