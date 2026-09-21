using Microsoft.AspNetCore.Http;

namespace ODCC.Api.Middleware;

/// <summary>
/// سخت‌افزاری‌سازی پایه‌ی مرورگر.
/// سیاست Content-Security-Policy فقط فونت CDN و استایل‌های درون‌خطی (الزامی برای تزریق
/// استایل توسط SPA) را مجاز می‌کند و هیچ چیز دیگری برای اسکریپت یا اتصال.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' data: https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'";

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "SAMEORIGIN";
        // از کلیدهای خام استفاده می‌شود: ویژگی‌های تایپ‌قوی برای این سه هدر
        // در این نسخه‌ی فریم‌ورک موجود نیستند.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permitted-Cross-Domain-Policies"] = "none";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
        }

        await next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
