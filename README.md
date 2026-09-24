# ODCC Survey — سامانه مدیریت نظرسنجی سازمانی

پلتفرم داخلی نظرسنجی و بازخورد سازمانی. فارسی-اول، RTL، با تقویم جلالی.
ساخته‌شده با ASP.NET Core 10 (Clean Architecture + Modular Monolith)، SQL Server،
Entity Framework Core، React 19 + TypeScript + Vite.

مستندات: [`docs/architecture.md`](docs/architecture.md) ·
[`docs/modules.md`](docs/modules.md) ·
[`docs/localization.md`](docs/localization.md)

## پیش‌نیازها

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) و npm
- SQL Server (LocalDB / Express / کامل) — احراز هویت Windows به‌صورت پیش‌فرض

## ساختار

```
ODCC.Survey.slnx
src/   ODCC.Domain, ODCC.Application, ODCC.Infrastructure, ODCC.Api
web/   React + TypeScript + Vite
docs/  مستندات توسعه‌دهنده
tests/ آزمون‌ها (در فازهای بعدی)
```

## راه‌اندازی Backend

### رشته‌ی اتصال

رشته‌ی اتصال پیش‌فرض در `src/ODCC.Api/appsettings.Development.json` (LocalDB، بدون رمز)
قرار دارد و از طریق متغیرهای محیطی یا user secrets قابل بازنویسی است.
**هرگز اعتبارات واقعی را در مخزن کد قرار ندهید.**

```powershell
# محیط تولید (متغیر محیطی)
setx ConnectionStrings__Default "Server=...;Database=...;User Id=...;Password=...;Encrypt=True"
```

### پایگاه داده (یکبار)

> **توجه:** طبق سیاست پروژه، ایجاد/تغییر پایگاه داده نیازمند تأیید صریح است.
> اجرای خودکار مهاجرت‌ها با `Database:AutoMigrate=false` به‌صورت پیش‌فرض غیرفعال است.

هر ماژول DbContext و مهاجرت‌های مستقل خود را دارد که فقط جداول همان ماژول را
ایجاد می‌کنند (مالکیت طرح بین ماژول‌ها حفظ می‌شود):

```powershell
# ماژول ممیزی (ماژول مرجع)
dotnet ef migrations add InitialCreate -p src/ODCC.Infrastructure -s src/ODCC.Api -c AuditDbContext

# ماژول هویت (کاربران، نقش‌ها، توکن‌های تازه‌سازی)
dotnet ef migrations add InitialCreate -p src/ODCC.Infrastructure -s src/ODCC.Api -c IdentityDbContext -o Modules/Identity/Persistence/Migrations

# ماژول سازمان (واحدها، موقعیت‌ها، کارمندان)
dotnet ef migrations add InitialCreate -p src/ODCC.Infrastructure -s src/ODCC.Api -c OrganizationDbContext -o Modules/Organization/Persistence/Migrations

# ماژول کتابخانه‌ی سؤالات
dotnet ef migrations add InitialCreate -p src/ODCC.Infrastructure -s src/ODCC.Api -c QuestionBankDbContext -o Modules/QuestionBank/Persistence/Migrations

# ماژول پرسشنامه‌ها (بخش‌ها، آیتم‌ها، انشعاب)
dotnet ef migrations add InitialCreate -p src/ODCC.Infrastructure -s src/ODCC.Api -c QuestionnaireDbContext -o Modules/Questionnaire/Persistence/Migrations

# ماژول نظرسنجی‌ها (چرخه‌ی عمر، قالب‌ها)
dotnet ef migrations add InitialCreate -p src/ODCC.Infrastructure -s src/ODCC.Api -c SurveyDbContext -o Modules/Survey/Persistence/Migrations

# ماژول کمپین‌ها (زمان‌بندی، توزیع، یادآورها)
dotnet ef migrations add InitialCreate -p src/ODCC.Infrastructure -s src/ODCC.Api -c CampaignDbContext -o Modules/Campaign/Persistence/Migrations

# اعمال همه‌ی مهاجرت‌ها (پس از تأیید)
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c AuditDbContext
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c IdentityDbContext
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c OrganizationDbContext
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c QuestionBankDbContext
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c QuestionnaireDbContext
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c SurveyDbContext
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c CampaignDbContext
```

> **هیچ‌کدام از این دستورات را تا دریافت تأیید اجرا نکنید.**

### داده‌ی اولیه (Bootstrap)

با فعال کردن `Database:AutoMigrate`، برنامه پس از اجرای مهاجرت‌ها به‌صورت
خودتوان داده‌ی اولیه را ایجاد می‌کند: واحد سازمانی ریشه، نقش «مدیر سامانه» با
تمام مجوزها (`IsSystem`) و کاربر مدیر کل با دامنه‌ی `Company`.

رمز عبور مدیر کل باید از user secrets یا متغیر محیطی تامین شود — هرگز در
مخزن کد:

```powershell
cd src/ODCC.Api
dotnet user-secrets set "Seed:AdminPassword" "YourStrongPassword123!"
dotnet user-secrets set "Jwt:Secret" "your-minimum-32-character-signing-key-here"
```

در صورت نبودن `Seed:AdminPassword`، ایجاد کاربر مدیر کل به‌صورت ایمن رد
می‌شود (برنامه همچنان بوت می‌شود).

### اجرا

```powershell
dotnet run --project src/ODCC.Api
```

- API: `https://localhost:7208`
- OpenAPI (فقط Development): `https://localhost:7208/openapi/v1.json`
- بررسی سلامت: `https://localhost:7208/health` (بدون پایگاه داده 503 برمی‌گرداند — رفتار درست است)

## راه‌اندازی Frontend

```powershell
cd web
npm install
npm run dev      # http://localhost:5174 (پروکسی /api به https://localhost:7208)
```

سرور توسعه‌ی Vite درخواست‌های `/api` را به API پروکسی می‌کند، پس مرورگر یک مبدأ می‌بیند.

### ساخت تولید

```powershell
npm run build    # type-check می‌کند و web/dist را تولید می‌کند
```

برای سرو کردن SPA از خود API (یک واحد استقراری):

```powershell
Copy-Item -Recurse web\dist\* src\ODCC.Api\wwwroot\
dotnet publish src/ODCC.Api -c Release -o ./publish
```

`Program.cs` با `UseStaticFiles` + `MapFallbackToFile("index.html")` پیوندهای عمیق را
به SPA هدایت می‌کند.

## امنیت

| موضوع | پیاده‌سازی |
|---|---|
| اسرار | فقط config/env/user secrets؛ در نبودن رشته‌ی اتصال، راه‌اندازی fail-fast می‌شود. |
| تزریق SQL | منحصراً پرس‌وجوهای پارامتری EF Core. |
| XSS | React تمام محتوا را escape می‌کند؛ CSP محدود (`script-src 'self'`). |
| CSRF | توکن antiforgery با هدر `X-CSRF-TOKEN`. |
| سوءاستفاده | محدودیت نرخ fixed-window سراسری. |
| حملات | HTTPS redirection + HSTS در تولید. |
| هدرها | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `COOP`, CSP. |
| خطاها | `IExceptionHandler` سراسری؛ ردپای پشته هرگز سرور را ترک نمی‌کند. |

## وضعیت پروژه

**فاز ۰** (زیرساخت و مبنا)، **فاز ۱** (هویت، مجوزدهی و سازمان)، **فاز ۲**
(کتابخانه‌ی سؤالات و پرسشنامه) و **فاز ۳** (نظرسنجی و کمپین) پیاده‌سازی شده‌اند.
ماژول‌های پیاده‌سازی‌شده تاکنون: Audit (ماژول مرجع)، Identity (کاربران، نقش‌ها،
JWT، توکن‌های تازه‌سازی)، Organization (واحدهای سازمانی، موقعیت‌ها، کارمندان)،
QuestionBank (سؤالات قابل‌استفاده‌ی مجدد، نسخه‌گذاری)، Questionnaire (بخش‌ها،
آیتم‌ها، انشعاب)، Survey (چرخه‌ی عمر نظرسنجی، قالب‌ها، پرچم ناشناس) و Campaign
(زمان‌بندی، جمعیت هدف، توزیع، یادآوری). سایر ماژول‌ها در فازهای بعدی ساخته
می‌شوند. به جدول فازها در پروژه‌ی معماری مراجعه کنید.

## عیب‌یابی

- **`Database initialization failed` در بوت** — مهاجرت ساخته/اجرا نشده یا `Database:AutoMigrate` فعال نیست.
- **`ECONNREFUSED` از سرور توسعه** — ابتدا API را اجرا کنید؛ پروکسی Vite به `https://localhost:7208` اشاره می‌کند.
