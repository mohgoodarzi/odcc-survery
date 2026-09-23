using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ODCC.Api.Authorization;
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
using ODCC.Infrastructure.Persistence.Audit;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");

// ---- داده و سرویس‌های کاربرد -----------------------------------------------
// رشته‌ی اتصال می‌تواند خالی باشد (پیکربندی اختیاری) تا برنامه در فاز صفر بدون
// پایگاه داده هم بوت شود. به‌محض فعال‌سازی Database:AutoMigrate بررسی می‌شود.
builder.Services.AddOdccInfrastructure(connectionString ?? string.Empty);
builder.Services.AddOdccApplication();
builder.Services.AddOdccDatabaseInitializer(builder.Configuration);

// ---- احراز هویت و مجوزدهی -----------------------------------------------
// تنظیمات JWT از پیکربندی (بخش Jwt). کلید باید حداقل ۳۲ کاراکتر باشد
// و از user secrets یا متغیر محیطی تامین شود — هرگز در مخزن کد.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

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
// می‌تواند با SQL Server صحبت کند. هر DbContext ماژول یک بررسی جداگانه است.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuditDbContext>("sql-server-audit")
    .AddDbContextCheck<IdentityDbContext>("sql-server-identity")
    .AddDbContextCheck<OrganizationDbContext>("sql-server-organization")
    .AddDbContextCheck<QuestionBankDbContext>("sql-server-question-bank")
    .AddDbContextCheck<QuestionnaireDbContext>("sql-server-questionnaire");

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
app.MapHealthChecks("/health");

// بازگشت به SPA تا پیوندهای عمیق به index.html منجر به 404 نشوند.
app.MapFallbackToFile("index.html");

app.Run();
