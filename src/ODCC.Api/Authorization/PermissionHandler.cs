using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ODCC.Application.Authorization;

namespace ODCC.Api.Authorization;

/// <summary>
/// بررسی‌کننده‌ی <see cref="PermissionRequirement"/>: کاربر باید کلیم مجوز را در توکن خود داشته باشد.
/// کلیم‌ها هنگام ورود از نقش‌های کاربر استخراج و داخل JWT قرار داده می‌شوند.
/// </summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;

        if (user?.Identity?.IsAuthenticated is not true)
        {
            return Task.CompletedTask;
        }

        if (user.HasClaim(Permissions.ClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
