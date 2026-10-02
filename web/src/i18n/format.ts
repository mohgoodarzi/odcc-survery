import { CULTURE_TO_LOCALE, type Culture } from './types';

/**
 * قالب‌بندی آگاه از فرهنگ با APIهای داخلی پلتفرم (Intl).
 * فرهنگ فارسی به‌صورت خودکار ارقام فارسی و تقویم جلالی را ارائه می‌دهد،
 * بنابراین هیچ کتابخانه‌ی تاریخ یا عددی لازم نیست.
 *
 * همه‌ی قالب‌ها `timeZone: 'UTC'` ندارند تا زمان‌ها در منطقه‌ی زمانی
 * کاربر نمایش داده شوند؛ اما برای برچسب‌هایی که فقط یک روز/ماه تقویمی
 * را از سمت سرور می‌رسند (بدون زمان)، از UTC استفاده می‌شود تا روز
 * تقویمی در هیچ منطقه‌ای جابه‌جا نشود.
 */

const periodMonthFormatter = new Intl.DateTimeFormat(CULTURE_TO_LOCALE.fa, {
  year: 'numeric',
  month: 'long',
  timeZone: 'UTC'
});

const periodDayFormatter = new Intl.DateTimeFormat(CULTURE_TO_LOCALE.fa, {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  timeZone: 'UTC'
});

const periodMonthTickFormatter = new Intl.DateTimeFormat(CULTURE_TO_LOCALE.fa, {
  month: 'short',
  timeZone: 'UTC'
});

const periodDayTickFormatter = new Intl.DateTimeFormat(CULTURE_TO_LOCALE.fa, {
  month: '2-digit',
  day: '2-digit',
  timeZone: 'UTC'
});

export function formatNumber(value: number, culture: Culture): string {
  return new Intl.NumberFormat(CULTURE_TO_LOCALE[culture], {
    notation: 'compact',
    maximumFractionDigits: 1
  }).format(value);
}

/**
 * اعداد صحیح با جداکننده‌ی هزارگان و ارقام محلی (مثلاً ۱۲۸۰ → ۱٬۲۸۰).
 * برای شمارنده‌ها و آمارها؛ برخلاف formatNumber که خلاصه‌سازی می‌کند.
 */
export function formatCount(value: number, culture: Culture): string {
  return new Intl.NumberFormat(CULTURE_TO_LOCALE[culture]).format(value);
}

/**
 * تاریخ را به‌صورت محلی نمایش می‌دهد. ورودی همواره ISO 8601 میلادی/UTC است؛
 * تقویم جلالی فقط در لایه‌ی نمایش و توسط مرورگر تبدیل می‌شود.
 */
export function formatDate(isoDate: string, culture: Culture): string {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) return '';

  return new Intl.DateTimeFormat(CULTURE_TO_LOCALE[culture], {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  }).format(date);
}

export function formatDateTime(isoDate: string, culture: Culture): string {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) return '';

  return new Intl.DateTimeFormat(CULTURE_TO_LOCALE[culture], {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

export function formatYear(year: number, culture: Culture): string {
  return new Intl.NumberFormat(CULTURE_TO_LOCALE[culture], { useGrouping: false }).format(year);
}

/**
 * برچسب دوره‌ی روند را — که سمت سرور به‌صورت میلادی ساخته شده — برای
 * نمایش شمسی درمی‌آورد:
 * - «2026-09» (ماه) → «شهریور ۱۴۰۵»
 * - «2026-09-30» (روز/هفته) → «۱۴۰۵/۰۶/۳۱»
 *
 * در فرهنگ انگلیسی همان مقدار میلادی برگردانده می‌شود.
 */
export function formatPeriodLabel(label: string, culture: Culture): string {
  if (!label) return '';
  if (culture === 'en') return label;

  const formatter = /^\d{4}-\d{2}$/.test(label) ? periodMonthFormatter : periodDayFormatter;
  const date = toUtcDate(label);

  return date === null ? label : formatter.format(date);
}

/**
 * شکل فشرده‌ی برچسب دوره برای محور نمودار:
 * - «2026-09» → «شهریور»
 * - «2026-09-30» → «۰۶/۳۱»
 */
export function formatPeriodTick(label: string, culture: Culture): string {
  if (!label) return '';
  if (culture === 'en') return label.slice(5);

  const formatter = /^\d{4}-\d{2}$/.test(label) ? periodMonthTickFormatter : periodDayTickFormatter;
  const date = toUtcDate(label);

  return date === null ? label.slice(5) : formatter.format(date);
}

/**
 * رشته‌ی تاریخ میلادی سرور را به یک `Date` در UTC تبدیل می‌کند؛
 * `null` اگر تجزیه نشود.
 */
function toUtcDate(label: string): Date | null {
  const day = /^\d{4}-\d{2}$/.test(label) ? `${label}-01` : label;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) return null;

  const date = new Date(`${day}T00:00:00Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}
