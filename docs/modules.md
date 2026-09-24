# مرزهای ماژول‌ها

هر ماژول مسئولیت مشخصی دارد و با ماژول‌های دیگر **بدون وابستگی چرخه‌ای** صحبت می‌کند.

| # | ماژول | مسئولیت | موجودیت‌های کلیدی |
|---|---|---|---|
| 1 | **Identity** | کاربران، نقش‌ها، کلیم‌ها، JWT، توکن‌های تازه‌سازی، MFA | `User`, `Role`, `RefreshToken` |
| 2 | **Organization** | سلسله‌مراتب سازمانی، پست‌ها، کارکنان، جمعیت هدف | `OrgUnit`, `Position`, `Employee` |
| 3 | **QuestionBank** | کتابخانه‌ی سؤالات قابل‌استفاده‌ی مجدد، تگ‌ها، نسخه‌ها | `Question`, `QuestionTag`, `QuestionVersion` |
| 4 | **Questionnaire** | ساختار: بخش‌ها، صفحات، سؤالات، انشعاب، اعتبارسنجی | `Questionnaire`, `Section`, `QuestionnaireItem`, `BranchingRule` |
| 5 | **Survey** | چرخه‌ی عمر نظرسنجی، قالب‌ها، تنظیمات، پرچم ناشناس | `Survey`, `SurveyTemplate` |
| 6 | **Campaign** | زمان‌بندی، هدف‌گیری مخاطب، توزیع، یادآوری‌ها، پیگیری‌ها | `Campaign`, `Distribution`, `Reminder` |
| 7 | **Response** | ثبت پاسخ، ذخیره‌ی جزئی، ارسال، پاسخ‌های ناشناس | `ResponseSession`, `ResponseAnswer` |
| 8 | **Analytics** | تجمیع، NPS/CSAT/CES، بنچمارک، داشبورد | `SurveyMetric`, `Benchmark` |
| 9 | **Reporting** | تعاریف گزارش، خروجی PDF/Excel، گزارش‌های زمان‌بندی‌شده | `ReportDefinition`, `ReportExecution` |
| 10 | **Notification** | ایمیل/پیامک/درون‌برنامه‌ای، قالب‌ها، ردیابی ارسال | `Notification`, `NotificationTemplate` |
| 11 | **ActionManagement** | برنامه‌های اقدام از نتایج، مالکان، مهلت‌ها، پیگیری | `ActionPlan`, `ActionItem` |
| 12 | **Workflow** | جریان‌های تأیید، ماشین وضعیت چرخه‌ی عمر نظرسنجی | `Workflow`, `WorkflowInstance` |
| 13 | **Audit** | گزارش ممیزی غیرقابل‌تغییر، ردیابی تغییرات | `AuditEntry` |
| 14 | **Integration** | همگام‌سازی HR، SSO، وب‌هوک، ارائه‌دهنده‌های هوش مصنوعی | `IntegrationEndpoint` |
| 15 | **SystemConfiguration** | تنظیمات، پرچم‌های ویژگی، پیکربندی محلی‌سازی | `Setting`, `FeatureFlag` |

## وضعیت فعلی (پایان فاز ۳)

**Audit** به‌عنوان ماژول مرجع پیاده‌سازی شده است. در فاز ۱ ماژول‌های
**Identity** (کاربران، نقش‌ها، کلیم‌ها، JWT، توکن‌های تازه‌سازی) و
**Organization** (ساختار درختی واحدها، موقعیت‌های شغلی، کارمندان) به‌طور کامل
پیاده‌سازی شدند. در فاز ۲ ماژول‌های **QuestionBank** (کتابخانه‌ی سؤالات، نسخه‌گذاری)
و **Questionnaire** (بخش‌ها، آیتم‌ها، انشعاب) اضافه شدند. در فاز ۳ ماژول‌های
**Survey** (چرخه‌ی عمر نظرسنجی، قالب‌ها، پرچم ناشناس) و **Campaign** (زمان‌بندی،
جمعیت هدف، توزیع، یادآوری) پیاده‌سازی شدند. بقیه‌ی ماژول‌ها در فازهای بعدی ساخته می‌شوند.

## ساختار یک ماژول کامل (الگوی ماژول مرجع)

```
ODCC.Domain/Modules/Audit/Entities/AuditEntry.cs
ODCC.Application/Modules/Audit/
    Abstractions/IAuditService.cs          ← قراردادی که سایر ماژول‌ها می‌بینند
    Abstractions/IAuditEntryRepository.cs  ← مخزن اختصاصی ماژول
    Dtos/AuditEntryDto.cs                  ← خروجی/ورودی API
    Services/AuditService.cs               ← منطق کاربردی
ODCC.Infrastructure/
    Persistence/Audit/AuditDbContext.cs    ← DbContext اختصاصی ماژول
    Repositories/Audit/AuditEntryRepository.cs
ODCC.Api/Controllers/AuditController.cs    ← نقطه‌ی ورود HTTP
```

## قواعد ارتباط بین ماژول‌ها

**مجاز است:**
- ماژول A از قرارداد ماژول B در `Abstractions` استفاده کند.
- ماژول A رویداد دامنه‌ای منتشر کند که ماژول B به آن گوش دهد.

**ممنوع است:**
- ماژول A از DbContext یا مخزن داخلی ماژول B استفاده کند.
- ماژول A به موجودیت دامنه‌ی ماژول B ارجاع مستقیم بدهد (فقط DTO/قرارداد).
- وابستگی چرخه‌ای در هر سطح.

این قواعد توسط `tests/ODCC.Architecture.Tests` (مبتنی بر NetArchTest) به‌صورت خودکار
بررسی می‌شوند.

## فاز ۳ — ماژول Survey

چرخه‌ی عمر کامل نظرسنجی روی یک ماشین وضعیت سمت سرور است:

```
Draft ──publish──▶ Scheduled ──start──▶ Active ──pause──▶ Paused ──resume──▶ Active
                                       Active ──close──▶ Closed ──archive──▶ Archived
```

نکات کلیدی:
- هر نظرسنجی به یک **پرسشنامه‌ی منتشرشده** وصل می‌شود و در زمان انتشار
  `QuestionnaireVersion` ثابت می‌شود تا ساختار پاسخ‌گویی در طول عمر ثابت بماند.
- **قالب‌ها** (`SurveyTemplate`) بسته‌های قابل‌استفاده‌ی مجدد از پرسشنامه + تنظیمات
  هستند و با `CreateFromTemplate` به‌سرعت نظرسنجی جدید می‌سازند.
- تمام انتقال‌ها رویداد دامنه منتشر می‌کنند که شنونده‌ی ممیزی ثبت می‌کند.
- جداول ترجمه (`survey_localizations`, `survey_template_localizations`) اندیس یکتای
  (parent, language) دارند.

## فاز ۳ — ماژول Campaign

کمپین مسئول توزیع یک نظرسنجی به جمعیت هدف است:

```
Draft ──schedule──▶ Scheduled ──launch──▶ Running ──complete──▶ Completed ──archive──▶ Archived
Draft ──launch──▶ Running                                        (هر وضعیت غیر بایگانی‌شده ──archive──▶ Archived)
```

نکات کلیدی:
- کمپین فقط به یک **نظرسنجی فعال** وصل می‌شود؛ اجرای کمپین وقتی نظرسنجی فعال نباشد رد
  می‌شود (`survey_not_active`).
- **جمعیت هدف** سه حالت دارد: تمام شرکت، واحدهای سازمانی (با پرچم `IncludeDescendants`
  برای شامل‌شدن زیردرخت) یا کارمندان صریح. حل این جمعیت در زمان اجرا از طریق
  **قراردادهای** ماژول سازمان انجام می‌شود، نه DbContext آن.
- **ردیف‌های توزیع** (`Distribution`) موجودیت‌های مجزایی هستند که هنگام اجرا به ازای هر
  گیرنده ساخته می‌شوند. نتیجه‌ی ارسال واقعی توسط ماژول اعلان‌ها از طریق
  `RecordDistributionResultAsync` ثبت می‌شود (در فاز اعلان‌ها).
- **یادآورها** دارای ترجمه هستند و `ProcessDueRemindersAsync` یادآورهای سررسیده را
  علامت می‌زند و شمارنده‌ی یادآورِ گیرندگانِ واجد شرایط را افزایش می‌دهد.
- تمام انتقال‌ها رویداد دامنه منتشر می‌کنند که شنونده‌ی ممیزی ثبت می‌کند.
- جداول ترجمه (`campaign_localizations`، `reminder_localizations`) اندیس یکتای
  (parent, language) دارند.
