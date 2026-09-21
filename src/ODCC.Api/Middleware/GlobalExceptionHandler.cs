using System.Net;
using Microsoft.AspNetCore.Diagnostics;

namespace ODCC.Api.Middleware;

/// <summary>
/// تبدیل استثناهای مدیریت‌نشده به یک پاسخ JSON پایدار و بدون نشت اطلاعات.
/// ردپای پشته و جزئیات پایگاه داده هرگز سرور را ترک نمی‌کنند؛ به‌جای آن لاگ می‌شوند.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    // برای کارایی و رعایت CA1848 از LoggerMessage.Define استفاده می‌شود.
    private static readonly Action<ILogger, Exception> UnhandledException = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(2, "UnhandledException"),
        "استثنای مدیریت‌نشده در پردازش درخواست.");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        UnhandledException(logger, exception);

        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.io/500",
            title = "Internal Server Error",
            status = 500,
            // کد مستقل از زبان؛ کلاینت آن را به پیام فارسی تبدیل می‌کند.
            code = "internal_error",
            detail = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید."
        }, cancellationToken);

        return true;
    }
}
