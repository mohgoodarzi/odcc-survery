using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Api.Middleware;
using ODCC.Api.Routing;
using ODCC.Application;
using ODCC.Application.Languages;
using ODCC.Infrastructure;
using ODCC.Infrastructure.Persistence.Audit;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");

// ---- داده و سرویس‌های کاربرد -----------------------------------------------
// رشته‌ی اتصال می‌تواند خالی باشد (پیکربندی اختیاری) تا برنامه در فاز صفر بدون
// پایگاه داده هم بوت شود. به‌محض فعال‌سازی Database:AutoMigrate بررسی می‌شود.
builder.Services.AddOdccInfrastructure(connectionString ?? string.Empty);
builder.Services.AddOdccApplication();
builder.Services.AddOdccDatabaseInitializer(builder.Configuration);

// ---- ارائه -----------------------------------------------------------------
// AddControllersWithViews (نه AddControllers): فیلتر ValidateAntiforgeryTokenAuthorizationFilter
// از طریق DI حل می‌شود و این فیلتر فقط توسط مجموعه‌ی کامل سرویس‌های MVC ثبت می‌شود.
builder.Services.AddControllersWithViews();

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

// محدودیت نرخ سراسری برای جلوگیری از سوءاستفاده.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Default", window =>
    {
        window.Window = TimeSpan.FromMinutes(1);
        window.PermitLimit = 60;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// بررسی سلامت: مسیر واقعی داده را اجرا می‌کند؛ «سالم» یعنی برنامه واقعاً
// می‌تواند با SQL Server صحبت کند.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuditDbContext>("sql-server-audit");

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

// بعد از routing عمداً: RouteDataRequestCultureProvider به مقدار مسیر {culture} نیاز دارد.
app.UseRequestLocalization();

if (allowedOrigins.Length > 0)
{
    app.UseCors();
}

app.UseRateLimiter();
app.UseAntiforgery();

app.MapControllers();
app.MapHealthChecks("/health");

// بازگشت به SPA تا پیوندهای عمیق به index.html منجر به 404 نشوند.
app.MapFallbackToFile("index.html");

app.Run();
