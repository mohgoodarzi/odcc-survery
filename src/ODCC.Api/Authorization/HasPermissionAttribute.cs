using Microsoft.AspNetCore.Authorization;
using ODCC.Application.Authorization;

namespace ODCC.Api.Authorization;

/// <summary>
/// ویژگی اعتبارسنجی مبتنی بر مجوز. معادل <c>[Authorize(Policy = permission)]</c> اما
/// تایپ‌امن و قابل‌خواندن. کاربرد: <c>[HasPermission(Permissions.Identity.UsersView)]</c>.
/// </summary>
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) : base(policy: permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
    }

    public HasPermissionAttribute(Permissions.PermissionKey permission) : base(permission.Value)
    {
    }
}
