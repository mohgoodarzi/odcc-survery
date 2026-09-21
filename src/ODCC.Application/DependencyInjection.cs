using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Services;

namespace ODCC.Application;

public static class DependencyInjection
{
    /// <summary>
    /// ثبت سرویس‌های لایه‌ی کاربرد.
    /// </summary>
    public static IServiceCollection AddOdccApplication(this IServiceCollection services)
    {
        // ماژول ممیزی: به‌عنوان ماژول مرجع پیاده‌سازی شده است.
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
