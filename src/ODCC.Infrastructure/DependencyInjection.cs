using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Infrastructure.Persistence;
using ODCC.Infrastructure.Persistence.Audit;
using ODCC.Infrastructure.Repositories.Audit;
using ODCC.Infrastructure.Services;

namespace ODCC.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// ثبت لایه‌ی زیرساخت: DbContext ماژول‌ها، مخازن و سرویس‌های مشترک.
    /// </summary>
    public static IServiceCollection AddOdccInfrastructure(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "رشته‌ی اتصال 'Default' یافت نشد. آن را با متغیر محیطی ConnectionStrings__Default، " +
                "user secret یا appsettings.json تامین کنید. هرگز اعتبارات واقعی را در مخزن کد قرار ندهید.");
        }

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddSingleton<IAnalyticsAiService, NoOpAnalyticsAiService>();

        // --- ماژول ممیزی -------------------------------------------------
        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
            configure?.Invoke(options);
        });

        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork<AuditDbContext>>();

        return services;
    }

    /// <summary>
    /// راه‌اندازی پایگاه داده در زمان بوت.
    ///
    /// توجه: طبق سیاست امنیتی پروژه، اجرای خودکار مهاجرت‌ها به‌صورت پیش‌فرض
    /// <b>غیرفعال</b> است (<c>Database:AutoMigrate</c>). فعال‌سازی آن نیازمند
    /// تأیید صریح شما پیش از ایجاد/تغییر پایگاه داده است.
    /// </summary>
    public static IServiceCollection AddOdccDatabaseInitializer(this IServiceCollection services, IConfiguration configuration)
    {
        var autoMigrate = configuration.GetValue<bool?>("Database:AutoMigrate") is true;

        if (autoMigrate)
        {
            services.AddHostedService<OdccDbInitializerHostedService>();
        }

        return services;
    }
}
