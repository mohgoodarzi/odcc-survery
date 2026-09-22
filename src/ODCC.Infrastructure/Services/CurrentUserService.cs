using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Services;

/// <summary>
/// دسترسی به کاربر جاری از <see cref="HttpContext"/> و کلیم‌های توکن JWT.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly HttpContext? _httpContext = httpContextAccessor.HttpContext;
    private ClaimsPrincipal? User => _httpContext?.User;

    public Guid? UserId
    {
        get
        {
            var id = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User?.FindFirstValue("sub");
            return Guid.TryParse(id, out var parsed) ? parsed : null;
        }
    }

    public string? UserName =>
        User?.FindFirstValue(ClaimTypes.Name)
        ?? User?.FindFirstValue("name");

    public string? DisplayName =>
        User?.FindFirstValue(ClaimTypes.GivenName)
        ?? User?.FindFirstValue("given_name");

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated is true;

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() is { } roles
            ? roles
            : [];

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll(ODCC.Application.Authorization.Permissions.ClaimType).Select(c => c.Value).ToList() is { } perms
            ? perms
            : [];

    /// <summary>واحد سازمانی کاربر از کلیم <c>org_unit</c>.</summary>
    public Guid? OrgUnitId
    {
        get
        {
            var value = User?.FindFirstValue("org_unit");
            return Guid.TryParse(value, out var parsed) ? parsed : null;
        }
    }

    /// <summary>دامنه‌ی دسترسی کاربر از کلیم <c>data_scope</c>.</summary>
    public DataScope DataScope
    {
        get
        {
            var value = User?.FindFirstValue("data_scope");
            return int.TryParse(value, out var parsed) && Enum.IsDefined(typeof(DataScope), parsed)
                ? (DataScope)parsed
                : DataScope.Own;
        }
    }

    public bool IsInRole(params string[] roles) =>
        roles.Length > 0 && roles.Any(role => User?.IsInRole(role) is true);

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(Permissions.PermissionKey permission) => HasPermission(permission.Value);

    public string? ClientIpAddress =>
        _httpContext?.Connection?.RemoteIpAddress?.ToString();
}
