# استقرار و تولید

این سند استقرار سامانه‌ی ODCC Survey را در محیط تولید پوشش می‌دهد: IIS،
KESTREL مستقل، Docker، مدیریت اسرار، پایگاه داده‌ی SQL Server و سخت‌گیری‌های
امنیتی.

> **سیاست پروژه:** ایجاد/تغییر پایگاه داده و سرویس‌های پس‌زمینه‌ی دارای اثر
> جانبی نیازمند **تأیید صریح** است. هیچ‌کدام به‌صورت پیش‌فرض در تولید فعال
> نیستند.

## معماری استقرار

```
مرورگر ──HTTPS──▶ IIS (پروکسی معکوس) ──▶ Kestrel (ASP.NET Core)
                              ├── API  (api/{culture}/...)
                              ├── SPA  (wwwroot، استاتیک + MapFallbackToFile)
                              ├── وب‌هوک ورودی عمومی (api/webhooks/{code})
                              └── SQL Server (یک پایگاه داده، چند DbContext ماژولی)
```

API و SPA در **یک واحد استقراری** سرو می‌شوند؛ نیازی به هاست جداگانه‌ی
frontend نیست.

## پیش‌نیازها

- **.NET 10 Runtime / Hosting Bundle** (شامل ماژول ASP.NET Core برای IIS)
- **SQL Server 2019+** (یا Azure SQL)، قابل دسترس از سرور برنامه
- **IIS 10+** روی Windows Server 2019+ با نقش `Web-Server` و ویژگی‌های:
  - `IIS-WebServerRole`
  - `IIS-WebSockets` (برای وب‌هوک‌ها و SignalR آینده — اختیاری اما توصیه می‌شود)
  - `IIS-ApplicationInit` (برای warm-up)
- کاربر سرویس با کمینه‌ترین دسترسی (Application Pool Identity یا حساب گواهی)

## ۱) ساخت و انتشار

```powershell
# Frontend
cd web
npm ci
npm run build          # type-check + تولید web/dist

# کپی خروجی SPA به wwwroot پروژه‌ی API
Copy-Item -Recurse -Force web\dist\* src\ODCC.Api\wwwroot\

# Backend
dotnet publish src/ODCC.Api -c Release -o .\publish
```

خروجیِ `.\publish` یک پوشه‌ی قابل‌حمل است که شامل `appsettings.Production.json`
نیست — مقادیر تولید از **متغیرهای محیطی** تامین می‌شوند (پایین‌تر).

### مسیر فونت فارسی گزارش‌ها

رندر PDF نیازمند فونتی با گلیف‌های فارسی است. مسیر فونت را از
`Reports:PersianFontPath` تنظیم کنید (یک فایل TTF در سرور):

```powershell
setx Reports__PersianFontPath "C:\Fonts\Vazirmatn-Regular.ttf"
```

در نبودن فونت، PDF تولید می‌شود ولی متن فارسی درست نمایش داده نمی‌شود.

## ۲) اسرار و پیکربندی

**هرگز** اعتبارات واقعی را در `appsettings.json` یا مخزن کد قرار ندهید. پروژه
با `appsettings.Production.json` همراه می‌شود که فقط **نکات** دارد، نه مقدار.
همه‌ی مقادیر حساس از متغیرهای محیطی خوانده می‌شوند:

| متغیر محیطی | توضیح |
|---|---|
| `ConnectionStrings__Default` | رشته‌ی اتصال SQL Server (اجباری — در نبودش fail-fast). |
| `Jwt__Secret` | کلید امضای توکن (حداقل ۳۲ کاراکتر). |
| `Seed__AdminPassword` | رمز مدیر کل برای داده‌ی اولیه (اختیاری). |
| `Integrations__Secrets__{SecretRef}` | راز هر اندپوینت یکپارچه‌سازی (کلید = نام منطقی `SecretRef`). |
| `Reports__PersianFontPath` | مسیر فونت فارسی برای PDF. |

```powershell
setx ConnectionStrings__Default "Server=sql.prod;Database=OdccSurvey;User Id=odcc_app;Password=...;Encrypt=True;TrustServerCertificate=False"
setx Jwt__Secret "your-minimum-32-character-signing-key-here"
setx Seed__AdminPassword "AStrongPassword123!"
```

> **نکته:** متغیرهای `setx` سطح کاربر هستند. برای Application Pool Identity،
> از `setx /M` (نیازمند مدیر) یا یک قالب ARM/Group Policy استفاده کنید.

### اسرار یکپارچه‌سازی

هر اندپوینت یکپارچه‌سازی فقط **نام منطقی** راز (`SecretRef`) را در پایگاه
داده نگه می‌دارد. مقدار واقعی باید به‌صورت
`Integrations__Secrets__{SecretRef}` در محیط سرور موجود باشد؛ در غیر این صورت
اندپوینت «راز پیکربندی‌نشده» گزارش می‌شود و ارسال/دریافت شکست می‌خورد.

## ۳) پایگاه داده

هر ماژول **DbContext و مهاجرت مستقل** خود را دارد که فقط جداول همان ماژول را
ایجاد می‌کند (مالکیت طرح بین ماژول‌ها حفظ می‌شود). همه‌ی مهاجرت‌ها
**افزودنی** هستند و هیچ‌گاه داده‌ی موجود را حذف یا بازنویسی نمی‌کنند.

```powershell
# اعمال همه‌ی مهاجرت‌ها (یکبار، با تأیید صریح)
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c AuditDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c IdentityDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c OrganizationDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c QuestionBankDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c QuestionnaireDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c SurveyDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c CampaignDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c ResponseDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c AnalyticsDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c NotificationDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c ActionManagementDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c WorkflowDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c IntegrationDbContext --connection "<رشته‌ی اتصال>"
dotnet ef database update -p src/ODCC.Infrastructure -s src/ODCC.Api -c SystemConfigurationDbContext --connection "<رشته‌ی اتصال>"
```

### ایمنی مهاجرت‌ها

- **افزودنی بودن:** مهاجرت‌های فاز ۹ فقط جدول جدید می‌سازند یا ستون/اندیس
  افزودنی اضافه می‌کنند (`AddColumn` با `nullable: true`، `CreateIndex`،
  `RenameIndex`). **هیچ‌کدام ستون یا جدولی را حذف یا بازنویسی نمی‌کنند.**
- **بدون دستکاری داده:** هیچ مهاجرتی داده‌ی موجود را تغییر نمی‌دهد.
- **قابل بازگشت:** هر مهاجرت `Down` کامل دارد.
- **توصیه:** مهاجرت‌ها را در یک محیط staging دقیقاً مشابه تولید اعمال و
  تست کنید، سپس با همان ترتیب در تولید.

### داده‌ی اولیه (Bootstrap)

با `Database:AutoMigrate=true` و `Seed:Enabled=true` برنامه به‌صورت خودتوان
واحد سازمانی ریشه، نقش «مدیر سامانه» و کاربر مدیر کل را ایجاد می‌کند. در
تولید `Seed:Enabled=false` پیش‌فرض است (به `appsettings.Production.json`
مراجعه کنید) و کاشت باید با تأیید صریح انجام شود. در نبودن
`Seed__AdminPassword` ایجاد مدیر کل **امنانه رد** می‌شود و برنامه همچنان
بوت می‌شود.

## ۴) استقرار در IIS

### ۴.۱) نصب Hosting Bundle

[.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)
را روی سرور نصب کنید تا ماژول `AspNetCoreModuleV2` در IIS ثبت شود.

### ۴.۲) ساخت سایت

1. محتوای `publish` را در `C:\inetpub\wwwroot\odcc` کپی کنید.
2. در IIS Manager یک **Application Pool** جدید بسازید:
   - نام: `ODCCSurvey`
   - .NET CLR version: **No Managed Code** (ماژول ASP.NET Core این را مدیریت می‌کند)
   - Identity: `ApplicationPoolIdentity` (یا یک حساب گواهی با دسترسی به پوشه‌ها)
3. یک **Website** جدید بسازید:
   - نام: `ODCC Survey`
   - Physical path: `C:\inetpub\wwwroot\odcc`
   - Binding: `https://*:443` با گواهی معتبر + (اختیاری) `http://*:80` برای redirect
   - Application Pool: `ODCCSurvey`

### ۴.۳) web.config

فایل `web.config` استاندارد در زمان publish تولید می‌شود. برای بهینه‌سازی:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet"
                arguments=".\ODCC.Api.dll"
                stdoutLogEnabled="true"
                stdoutLogFile=".\logs\stdout"
                hostingModel="inprocess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      </environmentVariables>
    </aspNetCore>
    <security>
      <requestFiltering>
        <!-- سقف اندازه‌ی بدنه برای آپلود پیوست (پارامتر MaxFileSizeMb باید با این هماهنگ باشد) -->
        <requestLimits maxAllowedContentLength="31457280" />
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

> `maxAllowedContentLength` را با `FileStorage:MaxFileSizeMb` هماهنگ کنید
> (پیش‌فرض ۲۵ مگابایت + سربار multipart).

### ۴.۴) مجوزهای پوشه

Application Pool Identity نیاز به **دسترسی نوشتن** در این مسیرها دارد:

- `App_Data\files` — انبار فایل‌ها (پیوست‌های اقدام)
- `App_Data\reports` — خروجی‌های گزارش
- `logs` — گزارش‌های stdout

```powershell
icacls "C:\inetpub\wwwroot\odcc\App_Data" /grant "IIS AppPool\ODCCSurvey:(OI)(CI)M" /T
icacls "C:\inetpub\wwwroot\odcc\logs" /grant "IIS AppPool\ODCCSurvey:(OI)(CI)M" /T
```

> **هشدار:** هرگز به Application Pool Identity دسترسی نوشتن به کل درایو یا
> به پوشه‌ی برنامه‌ی دیگر ندهید. انبار فایل از **path traversal** محافظت
> می‌شود، ولی کمینه‌کردن دسترسی لایه‌ی دفاعی بعدی است.

### ۴.۵) HTTPS و گواهی

- یک گواهی معتبر (داخلی یا عمومی) نصب کنید؛ HTTP را به HTTPS **redirect** کنید.
- HSTS توسط برنامه (در محیط Production) اعمال می‌شود — نیازی به پیکربندی IIS
  نیست.
- Cipher suites و TLS را در سطح OS/IIS سخت‌گیرانه پیکربندی کنید.

## ۵) سرویس‌های پس‌زمینه (اثر جانبی)

طبق سیاست پروژه، **هیچ سرویس پس‌زمینه‌ای به‌صورت پیش‌فرض فعال نیست**. هر کدام
با یک پرچم صریح فعال می‌شوند:

| سرویس | پرچم | اثر |
|---|---|---|
| زمان‌بند گزارش‌گیری | `Reports:EnableScheduler` | اجرای خودکار گزارش‌های فعال |
| زمان‌بند تحویل اعلان | `Notifications:Scheduler:EnableScheduler` | تحویل به‌تعویق‌افتاده و امتحان مجدد |
| زمان‌بند یادآور کمپین | `Campaigns:EnableReminderScheduler` | علامت‌گذاری یادآورهای سررسیده |
| زمان‌بند پیگیری اقدام | `Actions:FollowUp:EnableFollowUpScheduler` | یادآور و تشدید خودکار آیتم‌ها |
| زمان‌بند انقضای تأیید | `Workflows:EnableApprovalExpiryScheduler` | انقضای خودکار درخواست‌های تأیید |
| زمان‌بند تحویل وب‌هوک | `Integrations:Delivery:EnableDeliveryScheduler` | ارسال مجدد وب‌هوک‌های ناموفق |
| پردازشگر کارهای پس‌زمینه | `BackgroundJobs:EnableProcessor` | اجرای کارهای صف‌شده |

این‌ها را **یکی‌یکی** و پس از بررسی بار تولید فعال کنید. تحویل بلافاصله
اعلان‌ها همیشه فعال است.

## ۶) وب‌هوک ورودی (مسیر عمومی)

مسیر `POST /api/webhooks/{endpointCode}` از احراز هویت JWT معاف است چون توسط
سامانه‌های خارجی صدا زده می‌شود. امنیت آن:

- **امضای HMAC-SHA256** روی بدنه‌ی خام (هدر `X-ODCC-Signature` قابل پیکربندی).
- **محافظت در برابر بازپخش** با هدر برچسب زمانی و
  `Integrations:InboundTimestampToleranceSeconds` (پیش‌فرض ۳۰۰ ثانیه).
- **سقف اندازه‌ی بدنه ۱ مگابایت** (در سطح Kestrel و اعتبارسنج).
- امضای نامعتبر → ۴۰۱؛ اندپوینت ناموجود → ۴۰۴ (افشای وجود نمی‌شود).
- مسیر از فیلتر antiforgery سراسری معاف است (صریحاً و مستند).

در IIS، مطمئن شوید این مسیر توسط ruleهای URL Rewrite به صورت استاتیک تفسیر
نمی‌شود (در حالت inprocess نیازی نیست).

## ۷) بررسی سلامت

سه نقطه‌ی پایانی وجود دارد:

| مسیر | برچسب‌ها | معنی |
|---|---|---|
| `/health` | همه | خلاصه‌ی کامل: برنامه + همه‌ی DbContextها. |
| `/health/live` | `live` | فقط اجرای خود برنامه — **بدون وابستگی به پایگاه داده**. |
| `/health/ready` | `ready` | وابستگی‌های واقعی (۱۴ بررسی DbContext). |

این تفکیک عمدی است: کندی موقت پایگاه داده نباید باعث ری‌استارت برنامه
شود. پروب **liveness** متعادل‌کننده‌ی بار/IIS را به `/health/live` و پروب
**readiness** را به `/health/ready` وصل کنید. همه‌ی این مسیرها از محدودیت نرخ
معافند تا پروب‌های مکرر مسدود نشوند.

در IIS می‌توانید `/health/live` را به عنوان **App Initialization warm-up**
پیکربندی کنید تا برنامه پس از recycle آماده باشد:

```xml
<applicationInitialization>
  <add initializationPage="/health/live" />
</applicationInitialization>
```

## ۸) Docker (جایگزین IIS)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-nanoserver-1809 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-nanoserver-1809 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish src/ODCC.Api -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ODCC.Api.dll"]
```

```yaml
services:
  odcc:
    image: odcc-survey:latest
    ports: ["443:8080"]
    environment:
      ConnectionStrings__Default: "Server=sql;Database=OdccSurvey;..."
      Jwt__Secret: "..."
      ASPNETCORE_ENVIRONMENT: "Production"
    volumes:
      - ./appdata:/app/App_Data
    depends_on: [sql]
```

## ۹) سخت‌گیری‌های امنیتی تولید

| موضوع | اقدام |
|---|---|
| پایگاه داده | حساب اختصاصی برنامه با دسترسی فقط به یک پایگاه داده (db_datareader/db_datawriter + اجازه‌ی `CREATE TABLE` فقط برای مهاجرت). |
| شبکه | API و SQL در VLAN جداگانه؛ فقط پورت‌های ۸۰/۴۴۳ روی لبه. |
| فایروال | محدودیت نرخ روی مسیر عمومی وب‌هوک (`Public` policy) از قبل پیاده شده است. |
| لاگ‌ها | `stdout` را در IIS فعال کنید؛ **هرگز** لاگ را در مسیر عمومی سرو نکنید. |
| CORS | در تولید فقط مبدأهای واقعی frontend را در `Cors:AllowedOrigins` فهرست کنید. |
| گواهی‌ها | انقضا را مانیتور کنید؛ HSTS را فعال نگه دارید. |
| رمز مدیر کل | از متغیر محیطی؛ هرگز در مخزن کد. |
| نظارت | `/health/live` و `/health/ready` را در مانیتورینگ وصل کنید؛ خطاهای ۵xx را هشدار دهید. |
| پشتیبان | پایگاه داده و `App_Data` (پیوست‌ها و خروجی‌ها) را به‌صورت روزانه پشتیبان کنید. |

## ۱۰) عیب‌یابی استقرار

| مشکل | علت |
|---|---|
| `502.5 Process Failure` | Runtime نصب نیست یا `web.config` نادرست. `stdout` را بررسی کنید. |
| `Database initialization failed` | رشته‌ی اتصال اشتباه یا پایگاه داده در دسترس نیست. |
| کاربر مدیر کل ساخته نشد | `Seed__AdminPassword` تنظیم نشده — این یک رد ایمن است، نه خطا. |
| وب‌هوک ورودی ۴۰۱ می‌دهد | راز `Integrations__Secrets__{SecretRef}` موجود نیست یا امزا نادرست است. |
| متن فارسی در PDF خراب است | `Reports:PersianFontPath` تنظیم نیست یا فونت گلیف فارسی ندارد. |
| آپلود پیوست شکست می‌خورد | `maxAllowedContentLength` در IIS کمتر از `FileStorage:MaxFileSizeMb` است. |
| گزارش‌ها اجرا نمی‌شوند | `Reports:EnableScheduler` در تولید false است — این پیش‌فرض است. |

## ۱۱) ارتقا و rollback

- **ارتقا:** پوشه‌ی `publish` جدید را کنار قدیمی استقرار دهید (blue/green یا
  با توقف کوتاه)، مهاجرت‌های جدید را با ترتیب اعمال کنید، سپس Application Pool
  را recycle کنید.
- **Rollback:** نسخه‌ی قبلی را مستقر کنید. مهاجرت‌ها قابل بازگشت‌اند ولی
  **هرگز** داده‌ای را بازنویسی نمی‌کنند؛ rollback کد، ردیف‌های افزوده‌شده را
  سالم نگه می‌دارد.
- **هیچ‌گاه** پایگاه داده را drop/recreate نکنید — داده‌های پاسخ‌گوی ناشناس
  و تاریخچه‌ی ممیزی غیرقابل‌جبران هستند.
