using Microsoft.AspNetCore.Http;
using ODCC.Application.Languages;

namespace ODCC.Api.Routing;

/// <summary>
/// بخش مسیر {culture} را به زبان‌های پشتیبانی‌شده محدود می‌کند.
/// افزودن زبان یک خط در <see cref="LanguageExtensions"/> تغییر است.
/// </summary>
public sealed class LanguageRouteConstraint : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        return values.TryGetValue(routeKey, out var value)
            && value is string segment
            && LanguageExtensions.SupportedCultureSegments.Contains(segment, StringComparer.OrdinalIgnoreCase);
    }
}
