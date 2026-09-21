import { CULTURE_TO_LOCALE, type Culture } from './types';

/**
 * قالب‌بندی آگاه از فرهنگ با APIهای داخلی پلتفرم (Intl).
 * فرهنگ فارسی به‌صورت خودکار ارقام فارسی و تقویم جلالی را ارائه می‌دهد،
 * بنابراین هیچ کتابخانه‌ی تاریخ یا عددی لازم نیست.
 */

export function formatNumber(value: number, culture: Culture): string {
  return new Intl.NumberFormat(CULTURE_TO_LOCALE[culture], {
    notation: 'compact',
    maximumFractionDigits: 1
  }).format(value);
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
