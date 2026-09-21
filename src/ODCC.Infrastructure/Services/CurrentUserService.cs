using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ODCC.Application.Abstractions;

namespace ODCC.Infrastructure.Services;

/// <summary>
/// دسترسی به کاربر جاری از <see cref="HttpContext"/>.
/// با تکمیل احراز هویت JWT در فاز ۱، مقادیر از کلیم‌های توکن پر می‌شوند.
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly HttpContext? _httpContext = httpContextAccessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var id = _httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(id, out var parsed) ? parsed : null;
        }
    }

    public string? UserName =>
        _httpContext?.User?.FindFirstValue(ClaimTypes.Name)
        ?? _httpContext?.User?.FindFirstValue("name");

    public bool IsAuthenticated =>
        _httpContext?.User?.Identity?.IsAuthenticated is true;

    public IReadOnlyCollection<string> Roles =>
        _httpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() is { } roles
            ? roles
            : [];

    public bool IsInRole(params string[] roles) =>
        roles.Length > 0 && roles.Any(role => _httpContext?.User?.IsInRole(role) is true);

    public string? ClientIpAddress =>
        _httpContext?.Connection?.RemoteIpAddress?.ToString();
}
