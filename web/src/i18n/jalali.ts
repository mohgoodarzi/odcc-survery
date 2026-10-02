import type { Culture } from './types';

/**
 * تبدیل تاریخ میلادی (UTC) به هجری شمسی (جلالی) و برعکس.
 *
 * طبق سیاست پروژه (docs/localization.md)، الگوریتم تقویم شمسی دست‌ساز
 * پیاده‌سازی نمی‌شود. مرجع تبدیل، تقویم «persian» خود موتور Intl مرورگر
 * (ICU) است — همان تقویمی که در نمایش تاریخ‌ها به کار می‌رود:
 *
 *   new Intl.DateTimeFormat('fa-IR-u-ca-persian', ...)
 *
 * همه‌ی قالب‌ها با `timeZone: 'UTC'` کار می‌کنند تا روز تقویمی به هیچ
 * وجهی به منطقه‌ی زمانی کاربر یا تغییر ساعت تابستانه وابسته نباشد.
 * مسیر برگشت (شمسی → میلادی) با جستجوی دودویی روی روزها انجام می‌شود،
 * چون تقویم نسبت به روز میلادی کاملاً یکنوا است.
 */

export interface JalaliDate {
  year: number;
  /** ۱..۱۲ */
  month: number;
  /** ۱..۳۱ */
  day: number;
}

const MS_PER_DAY = 86_400_000;
const PERSIAN_CALENDAR_LOCALES: Record<Culture, string> = {
  fa: 'fa-IR-u-ca-persian',
  en: 'en-US-u-ca-persian'
};

const persianParts = new Intl.DateTimeFormat('fa-IR-u-ca-persian-nu-latn', {
  year: 'numeric',
  month: 'numeric',
  day: 'numeric',
  timeZone: 'UTC'
});

/**
 * روز میلادی (میلی‌ثانیه از مبدأ UTC، ترجیحاً نیمه‌شب) را به تاریخ شمسی تبدیل می‌کند.
 */
export function gregorianToJalali(utcMs: number): JalaliDate {
  const parts = persianParts.formatToParts(new Date(utcMs));
  const result: JalaliDate = { year: 0, month: 0, day: 0 };

  for (const part of parts) {
    if (part.type === 'year' || part.type === 'month' || part.type === 'day') {
      const value = Number(part.value.replace(/[^\d]/g, ''));
      result[part.type] = Number.isNaN(value) ? 0 : value;
    }
  }

  return result;
}

export function compareJalali(left: JalaliDate, right: JalaliDate): number {
  return left.year - right.year || left.month - right.month || left.day - right.day;
}

export function isEqualJalali(left: JalaliDate, right: JalaliDate): boolean {
  return compareJalali(left, right) === 0;
}

/**
 * تاریخ شمسی را به نیمه‌شب (UTC) همان روز میلادی تبدیل می‌کند.
 * برای تاریخ نامعتبر (مثل ۳۰ اسفند سال کبیسه‌نیس) `null` برمی‌گرداند.
 */
export function jalaliToGregorianMs(jalali: JalaliDate): number | null {
  // نوروز سال شمسی J همواره در مارس میلادی سال J+۶۲۱ است، پس کل سال شمسی
  // داخل این بازه قرار می‌گیرد و جستجوی دودویی کفایت می‌کند.
  let low = Date.UTC(jalali.year + 621, 2, 1);
  let high = Date.UTC(jalali.year + 622, 2, 31);

  while (low <= high) {
    const middle = low + Math.floor((high - low) / MS_PER_DAY / 2) * MS_PER_DAY;
    const comparison = compareJalali(gregorianToJalali(middle), jalali);

    if (comparison < 0) {
      low = middle + MS_PER_DAY;
    } else if (comparison > 0) {
      high = middle - MS_PER_DAY;
    } else {
      return middle;
    }
  }

  return null;
}

export function isValidJalaliDate(jalali: JalaliDate): boolean {
  return (
    jalali.year >= 1 &&
    jalali.year <= 9999 &&
    jalali.month >= 1 &&
    jalali.month <= 12 &&
    jalali.day >= 1 &&
    jalali.day <= daysInJalaliMonth(jalali.year, jalali.month)
  );
}

export function daysInJalaliMonth(year: number, month: number): number {
  if (month < 1 || month > 12 || year < 1 || year > 9999) return 0;

  const start = jalaliToGregorianMs({ year, month, day: 1 });
  const next = month === 12
    ? jalaliToGregorianMs({ year: year + 1, month: 1, day: 1 })
    : jalaliToGregorianMs({ year, month: month + 1, day: 1 });

  if (start === null || next === null) return 0;

  return Math.round((next - start) / MS_PER_DAY);
}

/**
 * امروز بر اساس روز تقویمی منطقه‌ی زمانی کاربر (نه UTC).
 */
export function jalaliToday(): JalaliDate {
  const now = new Date();
  return gregorianToJalali(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
}

/**
 * نام ماه شمسی در فرهنگ جاری. نام‌ها از همان تقویم Intl می‌آیند تا
 * نگهداری و هم‌خوانی با بقیه‌ی برنامه تضمین شود.
 */
export function jalaliMonthName(month: number, culture: Culture): string {
  if (month < 1 || month > 12) return '';

  const formatter = new Intl.DateTimeFormat(PERSIAN_CALENDAR_LOCALES[culture], {
    month: 'long',
    timeZone: 'UTC'
  });

  const probe = jalaliToGregorianMs({ year: 1404, month, day: 15 });
  return probe === null ? '' : formatter.format(new Date(probe));
}

/**
 * نام‌های هفته از شنبه تا جمعه — آغاز هفته‌ی تقویم شمسی.
 */
export function jalaliWeekdayNames(culture: Culture): string[] {
  const formatter = new Intl.DateTimeFormat(PERSIAN_CALENDAR_LOCALES[culture], {
    weekday: 'short',
    timeZone: 'UTC'
  });

  const now = Date.now();
  const weekday = new Date(now).getUTCDay(); // ۰ = یکشنبه … ۶ = شنبه
  const saturday = now - ((weekday + 1) % 7) * MS_PER_DAY;

  return Array.from(
    { length: 7 },
    (_, index) => formatter.format(new Date(saturday + index * MS_PER_DAY))
  );
}

const persianDigits = new Intl.NumberFormat('fa-IR', { useGrouping: false });
const latinDigits = new Intl.NumberFormat('en-US', { useGrouping: false });

/**
 * تاریخ شمسی را به شکل عددی قابل‌خواندن در فرهنگ جاری درمی‌آورد،
 * مثلاً «۱۴۰۵/۰۷/۰۸» یا «1405/07/08». این شکل برای ورود دستی هم parses می‌شود.
 */
export function formatJalaliDate(jalali: JalaliDate, culture: Culture): string {
  const digits = culture === 'fa' ? persianDigits : latinDigits;
  const zero = digits.format(0);
  const part = (value: number) => digits.format(value).padStart(2, zero);

  return `${digits.format(jalali.year)}/${part(jalali.month)}/${part(jalali.day)}`;
}

const FA_DIGITS = '۰۱۲۳۴۵۶۷۸۹';
const AR_DIGITS = '٠١٢٣٤٥٦٧٨٩';

/**
 * ارقام فارسی/عربی رشته‌ی ورودی را به ارقام لاتین تبدیل می‌کند تا
 * قابل تجزیه باشند (همان رویکرد `CalendarService.FromJalaliToUtc` سمت سرور).
 */
export function normalizeDigits(text: string): string {
  let result = text;

  for (let index = 0; index < FA_DIGITS.length; index++) {
    result = result.replaceAll(FA_DIGITS[index], String(index)).replaceAll(AR_DIGITS[index], String(index));
  }

  return result;
}

/**
 * یک رشته‌ی تاریخ شمسی (۱۴۰۵/۰۷/۰۸، 1405-07-08 یا ۱۴۰۵.۰۷.۰۸) را تجزیه می‌کند.
 * فقط شکل کامل سال/ماه/روز پذیرفته می‌شود. در صورت نامعتبر بودن `null`.
 */
export function parseJalaliDate(text: string): JalaliDate | null {
  const match = /^(\d{1,4})\s*[/\-.]\s*(\d{1,2})\s*[/\-.]\s*(\d{1,2})$/.exec(
    normalizeDigits(text).trim()
  );

  if (!match) return null;

  const jalali: JalaliDate = {
    year: Number(match[1]),
    month: Number(match[2]),
    day: Number(match[3])
  };

  return isValidJalaliDate(jalali) ? jalali : null;
}

/**
 * اولین سلول (شنبه) شبکه‌ی ماه شمسی را برمی‌گرداند: ممکن است روزهایی از
 * ماه قبل را هم شامل شود تا شبکه با شنبه شروع شود.
 */
export function firstCellOfJalaliMonthGrid(year: number, month: number): number | null {
  const start = jalaliToGregorianMs({ year, month, day: 1 });
  if (start === null) return null;

  const weekday = new Date(start).getUTCDay(); // ۰ = یکشنبه … ۶ = شنبه
  return start - ((weekday + 1) % 7) * MS_PER_DAY;
}

const persianLongDateFormatters: Record<Culture, Intl.DateTimeFormat> = {
  fa: new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    timeZone: 'UTC'
  }),
  en: new Intl.DateTimeFormat('en-US-u-ca-persian', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    timeZone: 'UTC'
  })
};

/**
 * تاریخ شمسی را به شکل بلند و خوانا درمی‌آورد، مثلاً «۸ مهر ۱۴۰۵».
 * برای هم‌خوانی با بقیه‌ی برنامه از همان موتور Intl استفاده می‌کند.
 */
export function formatJalaliLongDate(jalali: JalaliDate, culture: Culture): string {
  const ms = jalaliToGregorianMs(jalali);
  return ms === null ? '' : persianLongDateFormatters[culture].format(new Date(ms));
}

export { MS_PER_DAY };
