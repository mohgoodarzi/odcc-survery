# معماری

ODCC Survey یک «مونولیت ماژولار» مبتنی بر معماری تمیز (Clean Architecture) است.
 backend با ASP.NET Core 10 و frontend با React 19 + TypeScript + Vite ساخته می‌شود.

## ساختار راه‌حل

```
ODCC.Survey.slnx
src/
├── ODCC.Domain          موجودیت‌ها، اشیاء مقداری، رویدادهای دامنه (بدون وابستگی)
├── ODCC.Application     DTOها، قراردادهای ماژول‌ها، سرویس‌های کاربردی
├── ODCC.Infrastructure  DbContextهای ماژولی، مخازن، یکپارچه‌سازی خارجی
└── ODCC.Api             ریشه‌ی ترکیب: کنترلرها، میان‌افزارها، احراز هویت، میزبان SPA
web/                     React + TypeScript + Vite (پروژه‌ی مجزا)
tests/                   آزمون‌های واحد، یکپارچه‌سازی و معماری
docs/                    مستندات توسعه‌دهنده
```

## لایه‌بندی و وابستگی‌ها

`ODCC.Domain ← ODCC.Application ← ODCC.Infrastructure ← ODCC.Api`

| پروژه | ارجاع به | یادداشت |
|---|---|---|
| Domain | — | مدل خالص دامنه. هیچ پکیج خارجی. |
| Application | Domain | فقط `DependencyInjection.Abstractions` برای متد الحاقی DI. |
| Infrastructure | Application | EF Core 10 + SqlServer، QuestPDF، ارجاع صریح به فریم‌ورک ASP.NET Core. |
| Api | Application, Infrastructure | ریشه‌ی ترکیب؛ میزبان ابزار EF و SPAی ساخته‌شده. |

این قاعده فقط با ProjectReferenceها اعمال می‌شود و در `tests/ODCC.Architecture.Tests`
به‌صورت خودکار بررسی می‌شود.

## ریشه‌ی ترکیب

`src/ODCC.Api/Program.cs` تنها جایی است که لایه‌ها به هم وصل می‌شوند:

```csharp
builder.Services.AddOdccInfrastructure(connectionString);
builder.Services.AddOdccApplication();
builder.Services.AddOdccDatabaseInitializer(builder.Configuration);
```

- `AddOdccInfrastructure` — DbContextهای ماژولی (فعلاً `AuditDbContext`)، مخازن،
  `ICurrentUserService`، `ICalendarService` و پیاده‌سازی No-op تحلیلات هوش مصنوعی را ثبت می‌کند.
- `AddOdccApplication` — سرویس‌های کاربردی ماژول‌ها را ثبت می‌کند.
- `AddOdccDatabaseInitializer` — فقط در صورت فعال بودن `Database:AutoMigrate` ثبت می‌شود.

رشته‌ی اتصال با بررسی fail-fast خوانده می‌شود.

## خط لوله‌ی درخواست

ترتیب در `Program.cs` مهم است:

```
security headers → response compression → HTTPS redirection
→ default files / static files (immutable cache) → routing
→ request localization (بعد از routing: به مقدار مسیر {culture} نیاز دارد)
→ CORS (فقط هنگام وجود مبدأ مجاز) → rate limiter → antiforgery
→ MapControllers → MapHealthChecks → MapFallbackToFile("index.html")
```

## لایه‌ی داده

در مونولیت ماژولار، **هر ماژول DbContext اختصاصی خود را دارد** که فقط جداول همان ماژول را
می‌شناسد و مهاجرت‌های مستقل تولید می‌کند. تمام ماژول‌ها یک رشته‌ی اتصال و یک پایگاه داده‌ی
فیزیکی را به اشتراک می‌گذارند، اما مالکیت طرح (schema ownership) بین آن‌ها حفظ می‌شود.
سایر ماژول‌ها هرگز از DbContext یک ماژول استفاده مستقیم نمی‌کنند؛ تنها از طریق قراردادهای
لایه‌ی Application (مثلاً `IAuditService`).

قراردادهای مشترک در `Infrastructure/Persistence/Common/`:
- `EntityConfiguration.ConfigureBase` — کلید GUID، نام snake_case، حذف نرم (QueryFilter)،
  کنترل همزمانی خوش‌بینانه (`IsRowVersion`).
- `LocalizationConfiguration.ConfigureLocalization` — شکل مشترک تمام جدول‌های ترجمه با
  اندیس یکتا روی (parent, language).

## ماژول مرجع

ماژول **Audit** به‌عنوان «ماژول مرجع» پیاده‌سازی شده است تا الگوی کامل (موجیت → قرارداد →
DTO → سرویس → مخزن → DbContext → کنترلر) اثبات‌شده باشد و فازهای بعدی مکانیکی شوند.
ساختار آن را در `docs/modules.md` ببینید.

## جلوگیری از وابستگی چرخه‌ای

ارتباط بین ماژول‌ها فقط از طریق دو مسیر مجاز است:
1. **قراردادها** در `ODCC.Application.Abstractions` (اینترفیس‌های سرویس).
2. **رویدادهای دامنه** از طریق `IDomainEventDispatcher` / `IDomainEventListener<T>`.

مثال: ماژول Response رویداد `ResponseSubmitted` را منتشر می‌کند؛ ماژول‌های Analytics و
Notification آن را پردازش می‌کنند بدون اینکه Response از وجود آن‌ها بداند.
