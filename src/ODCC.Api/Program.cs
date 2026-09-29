using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using ODCC.Api;
using ODCC.Api.Authorization;
using ODCC.Api.Filters;
using ODCC.Api.Middleware;
using ODCC.Api.Routing;
using ODCC.Application;
using ODCC.Application.Languages;
using ODCC.Infrastructure;
using ODCC.Infrastructure.Modules.Identity;
using ODCC.Infrastructure.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;
using ODCC.Infrastructure.Modules.Organization.Persistence;
using ODCC.Infrastructure.Modules.QuestionBank.Persistence;
using ODCC.Infrastructure.Modules.Questionnaire.Persistence;
using ODCC.Infrastructure.Modules.Survey.Persistence;
using ODCC.Infrastructure.Modules.Campaign.Persistence;
using ODCC.Infrastructure.Modules.Response.Persistence;
using ODCC.Infrastructure.Modules.Analytics.Persistence;
using ODCC.Infrastructure.Modules.Reporting.Persistence;
using ODCC.Infrastructure.Modules.Notification.Persistence;
using ODCC.Infrastructure.Modules.ActionManagement.Persistence;
using ODCC.Infrastructure.Modules.Workflow.Persistence;
using ODCC.Infrastructure.Modules.Integration.Persistence;
using ODCC.Infrastructure.Modules.SystemConfiguration.Persistence;
using ODCC.Infrastructure.Persistence.Audit;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");

// ---- داده و سرویس‌های کاربرد -----------------------------------------------
// رشته‌ی اتصال می‌تواند خالی باشد (پیکربندی اختیاری) تا برنامه در فاز صفر بدون
// پایگاه داده هم بوت شود. به‌محض فعال‌سازی Database:AutoMigrate بررسی می‌شود.
builder.Services.AddOdccInfrastructure(connectionString ?? string.Empty);
builder.Services.AddOdccApplication();
builder.Services.AddOdccReporting(builder.Configuration);
builder.Services.AddOdccNotifications(builder.Configuration);
builder.Services.AddOdccCampaignReminders(builder.Configuration);
builder.Services.AddOdccActions(builder.Configuration);
builder.Services.AddOdccWorkflow(builder.Configuration);
builder.Services.AddOdccIntegrations(builder.Configuration);
builder.Services.AddOdccSystemConfiguration(builder.Configuration);
builder.Services.AddOdccFileStorage(builder.Configuration);
builder.Services.AddOdccBackgroundJobs(builder.Configuration);
builder.Services.AddOdccDatabaseInitializer(builder.Configuration);

// ---- احراز هویت و مجوزدهی -----------------------------------------------
// تنظیمات JWT از پیکربندی (بخش Jwt). کلید باید حداقل ۳۲ کاراکتر باشد
// و از user secrets یا متغیر محیطی تامین شود — هرگز در مخزن کد.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// اتصال بخش Jwt به IOptions<JwtOptions>: JwtTokenService و سایر سرویس‌ها
// تنظیمات را از IOptions می‌گیرند، نه از IConfiguration. بدون این اتصال،
// IOptions یک نمونه‌ی پیش‌فرض (با Secret خالی) برمی‌گرداند و صدور توکن
// همواره شکست می‌خورد.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        // در محیط توسعه فقط هشدار می‌دهیم تا بوت بدون کلید هم ممکن باشد.
        Console.WriteLine("هشدار: کلید امضای JWT تنظیم نشده یا کوتاه است. احراز هویت کار نخواهد کرد.");
    }
    else
    {
        throw new InvalidOperationException(
            "کلید امضای JWT یافت نشد یا کوتاه است. آن را با متغیر محیطی Jwt__Secret " +
            "یا user secret تامین کنید. هرگز کلید واقعی را در مخزن کد قرار ندهید.");
    }
}
else
{
    builder.Services.AddOdccJwtAuthentication(jwtOptions);
}

// مجوزدهی مبتنی بر مجوزهای پویا: PolicyProvider نیازی به ثبت تک‌تک مجوزها ندارد.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization();

// ---- ارائه -----------------------------------------------------------------
// AddControllersWithViews (نه AddControllers): فیلتر ValidateAntiforgeryTokenAuthorizationFilter
// از طریق DI حل می‌شود و این فیلتر فقط توسط مجموعه‌ی کامل سرویس‌های MVC ثبت می‌شود.
// ModelValidationFilter: اعتبارسنج‌های FluentValidation لایه‌ی کاربرد را روی
// پارامترهای [FromBody] اجرا می‌کند تا درخواست نامعتبر هرگز به سرویس نرسد.
builder.Services.AddControllersWithViews(options => options.Filters.Add<ModelValidationFilter>());

// OpenAPI در .NET 10 داخلی است؛ نیازی به وابستگی Swashbuckle نیست.
builder.Services.AddOpenApi();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// بخش مسیر /api/{culture}/... را به فرهنگ‌های پشتیبانی‌شده محدود می‌کند.
builder.Services.Configure<RouteOptions>(options =>
{
    options.ConstraintMap.Add("language", typeof(LanguageRouteConstraint));
});

// محلی‌سازی درخواست: fa-IR / en-US را اول از مقدار مسیر، سپس کوکی و سپس
// هدر Accept-Language حل می‌کند و قالب‌بندی سمت سرور را هدایت می‌کند.
var supportedCultures = new[] { new CultureInfo("fa-IR"), new CultureInfo("en-US") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("fa-IR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider());
});

// CORS فقط هنگام اجرای سرور توسعه‌ی React روی مبدأ جداگانه فعال می‌شود.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
    });
}

// محافظت CSRF برای فرم‌های عمومی.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "ODCC.Survey.AntiForgery";
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// محدودیت نرخ: خط‌مشی‌های متفاوت برای انواع مسیر.
// - Default: خط‌مشی سراسری (GlobalLimiter) — هر درخواستی زیر این سقف است.
// - Public: مسیرهای عمومی (لاگین، وب‌هوک ورودی) — محدودتر برای جلوگیری از
//   brute-force و اسپم.
// - Critical: عملیات پرهزینه (داشبورد، گزارش، تحلیل) — جلوگیری از فشار زیاد.
// وقتی هم خط‌مشی سراسری و هم خط‌مشی نقطه‌ای اعمال شوند، هر دو سقف فعال
// می‌شوند (دفاع در عمق).
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Default", window =>
    {
        window.Window = TimeSpan.FromMinutes(1);
        window.PermitLimit = 60;
    });
    options.AddFixedWindowLimiter("Public", window =>
    {
        window.Window = TimeSpan.FromMinutes(1);
        window.PermitLimit = 20;
    });
    options.AddFixedWindowLimiter("Critical", window =>
    {
        window.Window = TimeSpan.FromMinutes(1);
        window.PermitLimit = 30;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // خط‌مشی سراسری: همه‌ی مسیرها (حتی بدون ویژگی) زیر سقف پایه قرار می‌گیرند.
    // مسیرهای بررسی سلامت از این سقف معافند تا پروب‌های متوالیِ متعادل‌کننده‌ی
    // بار مسدود نشوند.
    options.GlobalLimiter = PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 60,
                QueueLimit = 0
            }));
});

// کش خروجی برای پاسخ‌های پرهزینه و تغییرناپذیر (داشبورد، آمار، متادیتا).
// انقضای کوتاه: همه داده‌ی تازه را می‌بینند و فشار پایگاه داده کم می‌شود.
// خط‌مشی‌ها بر اساس هدر Authorization تفکیک می‌شوند تا پاسخِ کاربری با مجوز،
// برای کاربرِ بدون مجوز (یا برعکس) لو نرود (جلوگیری از آلودگی کش).
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("Dashboard", policy => policy
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByHeader("Authorization")
        .Tag("dashboard"));

    options.AddPolicy("Metadata", policy => policy
        .Expire(TimeSpan.FromMinutes(5))
        .Tag("metadata"));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// بررسی سلامت: مسیر واقعی داده را اجرا می‌کند؛ «سالم» یعنی برنامه واقعاً
// می‌تواند با SQL Server صحبت کند. هر DbContext ماژول یک بررسی جداگانه است.
// - «self» (برچسب live): فقط در صورت اجرای خود برنامه سالم است → /health/live
//   برای پروبِ زنده‌بودن (liveness) متعادل‌کننده‌ی بار/IIS.
// - بررسی‌های پایگاه داده → /health/ready (آماده‌بودن) و /health.
// این تفکیک باعث نمی‌شود برنامه به‌خاطر کندی موقت پایگاه داده ری‌استارت شود.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: HealthCheckTags.Live)
    .AddDbContextCheck<AuditDbContext>("sql-server-audit", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<IdentityDbContext>("sql-server-identity", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<OrganizationDbContext>("sql-server-organization", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<QuestionBankDbContext>("sql-server-question-bank", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<QuestionnaireDbContext>("sql-server-questionnaire", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<SurveyDbContext>("sql-server-survey", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<CampaignDbContext>("sql-server-campaign", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<ResponseDbContext>("sql-server-response", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<AnalyticsDbContext>("sql-server-analytics", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<ReportingDbContext>("sql-server-reporting", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<NotificationDbContext>("sql-server-notification", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<ActionManagementDbContext>("sql-server-action-management", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<WorkflowDbContext>("sql-server-workflow", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<IntegrationDbContext>("sql-server-integration", tags: HealthCheckTags.Ready)
    .AddDbContextCheck<SystemConfigurationDbContext>("sql-server-system-configuration", tags: HealthCheckTags.Ready);

var app = builder.Build();

// ---- خط لوله ---------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseStatusCodePages();
app.UseSecurityHeaders();
app.UseResponseCompression();
app.UseHttpsRedirection();

// ارائه‌ی برنامه‌ی React ساخته‌شده (در صورت وجود) تا تولید یک واحد استقراری باشد.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.Append(
        "Cache-Control", "public, max-age=31536000, immutable")
});

app.UseRouting();

// کش خروجی: باید بعد از routing و قبل از authorization باشد.
app.UseOutputCache();

// بعد از routing عمداً: RouteDataRequestCultureProvider به مقدار مسیر {culture} نیاز دارد.
app.UseRequestLocalization();

// احراز هویت باید قبل از مجوزدهی و قبل از endpointها باشد.
app.UseAuthentication();
app.UseAuthorization();

if (allowedOrigins.Length > 0)
{
    app.UseCors();
}

app.UseRateLimiter();
app.UseAntiforgery();

app.MapControllers();

// نقاط پایانی بررسی سلامت. پروب زنده‌بودن (/health/live) فقط اجرای خود برنامه
// را بررسی می‌کند و پروب آماده‌بودن (/health/ready) وابستگی‌های واقعی (پایگاه
// داده) را. هر دو از محدودیت نرخ معافند تا پروب‌های مکرر متعادل‌کننده‌ی بار
// یا IIS مسدود نشوند.
app.MapHealthChecks("/health").DisableRateLimiting();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).DisableRateLimiting();

// بازگشت به SPA تا پیوندهای عمیق به index.html منجر به 404 نشوند.
app.MapFallbackToFile("index.html");

app.Run();

