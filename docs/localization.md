# محلی‌سازی و تقویم جلالی

سامانه فارسی-اول است: زبان پیش‌فرض فارسی، جهت RTL، و ارقام/تقویم فارسی.

## قانون اساسی تاریخ‌ها

**تاریخ شمسی هرگز به‌صورت رشته در پایگاه داده ذخیره نمی‌شود.**

- تمام تاریخ‌ها به‌صورت میلادی/UTC در ستون‌های `datetime2` / `date` ذخیره می‌شوند.
- تبدیل به شمسی **فقط در لایه‌ی ارائه** انجام می‌شود (رابط کاربری، گزارش‌های PDF، پیامک).
- در backend، تبدیل توسط `ICalendarService` که از `PersianCalendar` خود دات‌نت استفاده می‌کند
  (الگوریتم رسمی و قابل‌اتکا؛ هرگز الگوریتم دست‌ساز پیاده‌سازی نمی‌شود).

```csharp
public interface ICalendarService
{
    string ToJalaliShortDate(DateTime utcValue, Language language = Language.Fa);
    string ToJalaliLongDate(DateTime utcValue, Language language = Language.Fa);
    string ToJalaliDateTime(DateTime utcValue, Language language = Language.Fa);
    DateTime? FromJalaliToUtc(string jalaliDate);
}
```

در frontend، تبدیل توسط `Intl` API مرورگر با فرهنگ `fa-IR` انجام می‌شود که تقویم جلالی و
ارقام فارسی را **به‌صورت خودکار** ارائه می‌دهد — نیازی به کتابخانه‌ی تاریخ نیست:

```ts
new Intl.DateTimeFormat('fa-IR', { year: 'numeric', month: 'long', day: 'numeric' })
```

## Backend

### شمارش زبان

```csharp
public enum Language { Fa = 1, En = 2 }
```

به‌صورت `int` ذخیره می‌شود تا افزودن زبان، ردیف‌های موجود را نامعتبر نکند.

### محتوای قابل‌ترجمه

هر موجودیت قابل‌ترجمه یک جدول فرزند `*Localization` دارد که با `Language` کلید می‌خورد.
ستون‌های ترجمه هرگز روی جدول اصلی قرار نمی‌گیرند، پس افزودن زبان فقط افزودن ردیف است و
نیازی به مهاجرت طرح پایگاه داده ندارد. یک اندیس یکتا روی `(parent, language)` تضمین می‌کند
که هر زبان برای هر والد فقط یک ردیف داشته باشد.

### نگاشت فرهنگ — منبع واحد حقیقت

`ODCC.Application.Languages.LanguageExtensions` تنها جایی است که backend از بخش‌های مسیر
مطلع است. `ToLanguage()` برای بخش ناشناخته به فارسی تنزل می‌کند.

### مسیریابی

`LanguageRouteConstraint` بخش `{culture}` را به `fa`/`en` محدود می‌کند.
`RequestLocalizationOptions` گزینه‌های `fa-IR`/`en-US` را با پیش‌فرض `fa-IR` تعریف می‌کند و
`RouteDataRequestCultureProvider` را اولویت اول قرار می‌دهد تا مقدار مسیر بر کوکی و
`Accept-Language` پیروز شود.

## Frontend

### رشته‌های رابط کاربری

`web/src/i18n/types.ts` اینترفیس `Dictionary` را تعریف می‌کند — قراردادی که هر زبان باید
ارضا کند. هر زبان یک فایل: `web/src/i18n/dictionaries/{fa,en}.ts`.

`LanguageProvider` از طریق هوک `useLanguage()` مقادیر `culture`, `direction`, `locale`, `t`,
`setCulture`, `toggleCulture` را در اختیار می‌گذارد.

### رزولوشن فرهنگ و ماندگاری

تقدم: **بخش مسیر → تنظیم ذخیره‌شده → پیش‌فرض سامانه (فارسی)**. تغییر زبان فقط بخش فرهنگ را
بازنویسی می‌کند و مسیر فعلی را حفظ می‌کند. تنظیمات در `localStorage` ذخیره می‌شود.

یک اسکریپت درون‌خطی در `web/index.html` قبل از اولین رنگ‌پردازی `lang` و `dir` را تنظیم
می‌کند تا چشمک جهت اشتباه رخ ندهد.

### رفتار آگاه از جهت

- لایه‌بندی از خصوصیات منطقی CSS استفاده می‌کند، بنابراین یک استایل‌شیت با `dir` برمی‌گردد.
- مقادیر فقط-LTR (ایمیل، شماره) با `dir="ltr"` نشانه‌گذاری می‌شوند.

## افزودن زبان سوم

به پایگاه داده نیاز نیست — طرح از نظر طراحی زبان‌خنثی است.

**Backend**
1. عضو enum را در `Language.cs` اضافه کنید.
2. بخش(ها) را در `LanguageExtensions` اضافه کنید.
3. فرهنگ را در `Program.cs` اضافه کنید.
4. ردیف‌های `*Localization` را در Seeding اضافه کنید.

**Frontend**
1. نوع `Culture` و `CULTURE_TO_LOCALE` را گسترش دهید.
2. فایل `dictionaries/<code>.ts` را بسازید.
3. آن را در `DICTIONARIES` ثبت کنید.

## تنزل ایمن

- نبودن ردیف ترجمه: پیاده‌سازی `LocalizationPicker.Pick` به هر ترجمه‌ی موجود تنزل می‌کند.
- بخش فرهنگ ناشناخته در API: `ToLanguage()` به فارسی.
- بخش ناشناخته در URL: frontend به تنظیم ذخیره‌شده و سپس فارسی تنزل می‌کند.
