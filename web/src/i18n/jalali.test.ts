import { describe, expect, it } from 'vitest';

import {
  compareJalali,
  daysInJalaliMonth,
  firstCellOfJalaliMonthGrid,
  formatJalaliDate,
  formatJalaliLongDate,
  gregorianToJalali,
  isValidJalaliDate,
  jalaliMonthName,
  jalaliToGregorianMs,
  jalaliWeekdayNames,
  normalizeDigits,
  parseJalaliDate
} from './jalali';

describe('gregorianToJalali', () => {
  it('converts known dates', () => {
    expect(gregorianToJalali(Date.UTC(2026, 8, 30))).toEqual({ year: 1405, month: 7, day: 8 });
    expect(gregorianToJalali(Date.UTC(2024, 2, 20))).toEqual({ year: 1403, month: 1, day: 1 });
    expect(gregorianToJalali(Date.UTC(2020, 7, 21))).toEqual({ year: 1399, month: 5, day: 31 });
  });

  it('is stable across the whole proleptic range', () => {
    // رفت و برگشت دوباره روی همه‌ی روزهای ۲۰۰ سال
    for (let day = Date.UTC(1900, 0, 1); day <= Date.UTC(2100, 11, 31); day += 86_400_000) {
      expect(jalaliToGregorianMs(gregorianToJalali(day))).toBe(day);
    }
  });

  it('is not affected by the host time zone', () => {
    // ۲۰۲۶-۰۹-۳۰ ساعت ۱۰ شب UTC در مناطق شرقی فردای آنجاست؛ تقویم شمسی
    // باید روی UTC ثابت بماند.
    expect(gregorianToJalali(Date.UTC(2026, 8, 30, 22))).toEqual({ year: 1405, month: 7, day: 8 });
    expect(gregorianToJalali(Date.UTC(2026, 8, 30, 2))).toEqual({ year: 1405, month: 7, day: 8 });
  });
});

describe('jalaliToGregorianMs', () => {
  it('converts back to the same calendar day', () => {
    const ms = jalaliToGregorianMs({ year: 1405, month: 7, day: 8 });
    expect(new Date(ms!).toISOString()).toBe('2026-09-30T00:00:00.000Z');
  });

  it('returns null for non-existent dates', () => {
    expect(jalaliToGregorianMs({ year: 1404, month: 12, day: 30 })).toBeNull();
    expect(jalaliToGregorianMs({ year: 1405, month: 7, day: 32 })).toBeNull();
    expect(jalaliToGregorianMs({ year: 1405, month: 13, day: 1 })).toBeNull();
  });
});

describe('isValidJalaliDate', () => {
  it('accepts real dates', () => {
    expect(isValidJalaliDate({ year: 1405, month: 7, day: 8 })).toBe(true);
    expect(isValidJalaliDate({ year: 1403, month: 12, day: 30 })).toBe(true);
  });

  it('rejects Esfand 30 of non-leap years', () => {
    expect(isValidJalaliDate({ year: 1404, month: 12, day: 30 })).toBe(false);
    expect(isValidJalaliDate({ year: 1403, month: 12, day: 30 })).toBe(true);
  });
});

describe('daysInJalaliMonth', () => {
  it('knows the lengths of Persian months', () => {
    expect(daysInJalaliMonth(1405, 1)).toBe(31);
    expect(daysInJalaliMonth(1405, 6)).toBe(31);
    expect(daysInJalaliMonth(1405, 7)).toBe(30);
    expect(daysInJalaliMonth(1405, 12)).toBe(29);
  });

  it('gives Esfand 30 in leap years', () => {
    expect(daysInJalaliMonth(1403, 12)).toBe(30);
  });
});

describe('compareJalali', () => {
  it('orders dates correctly', () => {
    expect(compareJalali({ year: 1405, month: 1, day: 1 }, { year: 1405, month: 1, day: 1 })).toBe(0);
    expect(compareJalali({ year: 1404, month: 12, day: 30 }, { year: 1405, month: 1, day: 1 })).toBeLessThan(0);
    expect(compareJalali({ year: 1405, month: 2, day: 1 }, { year: 1405, month: 1, day: 30 })).toBeGreaterThan(0);
  });
});

describe('firstCellOfJalaliMonthGrid', () => {
  it('starts the grid on Saturday', () => {
    // ۱۴۰۵/۰۷/۰۱ چهارشنبه است، پس اولین سلول شبکه شنبه‌ی همان هفته است.
    const firstCell = firstCellOfJalaliMonthGrid(1405, 7)!;
    expect(new Date(firstCell).getUTCDay()).toBe(6);
    expect(gregorianToJalali(firstCell)).toEqual({ year: 1405, month: 6, day: 28 });
  });

  it('keeps exactly 42 days aligned', () => {
    const firstCell = firstCellOfJalaliMonthGrid(1405, 7)!;
    const lastCell = firstCell + 41 * 86_400_000;
    expect(gregorianToJalali(lastCell).month).toBe(8);
  });
});

describe('formatJalaliDate / parseJalaliDate', () => {
  it('formats with Persian digits in Persian culture', () => {
    expect(formatJalaliDate({ year: 1405, month: 7, day: 8 }, 'fa')).toBe('۱۴۰۵/۰۷/۰۸');
  });

  it('formats with Latin digits in English culture', () => {
    expect(formatJalaliDate({ year: 1405, month: 7, day: 8 }, 'en')).toBe('1405/07/08');
  });

  it('parses Persian and Latin digits and different separators', () => {
    expect(parseJalaliDate('۱۴۰۵/۰۷/۰۸')).toEqual({ year: 1405, month: 7, day: 8 });
    expect(parseJalaliDate('1405-07-08')).toEqual({ year: 1405, month: 7, day: 8 });
    expect(parseJalaliDate('1405.07.08')).toEqual({ year: 1405, month: 7, day: 8 });
  });

  it('round-trips through the string form', () => {
    for (let year = 1390; year <= 1420; year++) {
      const formatted = formatJalaliDate({ year, month: 7, day: 8 }, 'fa');
      expect(parseJalaliDate(formatted)).toEqual({ year, month: 7, day: 8 });
    }
  });

  it('rejects garbage', () => {
    expect(parseJalaliDate('')).toBeNull();
    expect(parseJalaliDate('today')).toBeNull();
    expect(parseJalaliDate('1405/07/08/01')).toBeNull();
    expect(parseJalaliDate('۱۴۰۴/۱۲/۳۰')).toBeNull();
  });
});

describe('normalizeDigits', () => {
  it('maps Persian and Arabic-Indic digits to Latin', () => {
    expect(normalizeDigits('۱۴۰۵۰۶')).toBe('140506');
    expect(normalizeDigits('١٤٠٥')).toBe('1405');
  });
});

describe('localized names', () => {
  it('returns Persian month names', () => {
    expect(jalaliMonthName(1, 'fa')).toBe('فروردین');
    expect(jalaliMonthName(7, 'fa')).toBe('مهر');
    expect(jalaliMonthName(12, 'fa')).toBe('اسفند');
  });

  it('returns seven weekday names starting on Saturday', () => {
    expect(jalaliWeekdayNames('fa')).toEqual([
      'شنبه', 'یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه'
    ]);
  });

  it('formats long dates', () => {
    expect(formatJalaliLongDate({ year: 1405, month: 7, day: 8 }, 'fa')).toBe('۸ مهر ۱۴۰۵');
  });
});
