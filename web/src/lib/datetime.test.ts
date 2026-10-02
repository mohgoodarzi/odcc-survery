import { describe, expect, it, vi } from 'vitest';

import {
  formatLocalInput,
  isoToLocalDate,
  isoToLocalDateTime,
  localDateTimeToIso,
  parseLocalInput,
  todayLocalDate
} from './datetime';

const pad = (value: number) => String(value).padStart(2, '0');

describe('localDateTimeToIso', () => {
  it('interprets the input as the local wall clock and emits UTC', () => {
    // معادل `new Date(value).toISOString()` — یعنی زمان محلی کاربر مبنای تبدیل است.
    expect(localDateTimeToIso('2026-09-30T14:30')).toBe(new Date('2026-09-30T14:30').toISOString());
  });

  it('returns null for empty and invalid input', () => {
    expect(localDateTimeToIso('')).toBeNull();
    expect(localDateTimeToIso(null)).toBeNull();
    expect(localDateTimeToIso('not-a-date')).toBeNull();
  });
});

describe('isoToLocalDateTime', () => {
  it('round-trips against localDateTimeToIso', () => {
    const value = '2026-09-30T14:30';
    expect(isoToLocalDateTime(localDateTimeToIso(value)!)).toBe(value);
  });

  it('handles naive server strings as local time', () => {
    expect(isoToLocalDateTime('2026-09-30T14:00:00')).toBe('2026-09-30T14:00');
  });

  it('rebuilds the wall clock from a UTC instant', () => {
    const iso = localDateTimeToIso('2026-09-30T14:30')!;
    const expected = new Date(iso);
    const expectedString =
      `${expected.getFullYear()}-${pad(expected.getMonth() + 1)}-${pad(expected.getDate())}` +
      `T${pad(expected.getHours())}:${pad(expected.getMinutes())}`;

    expect(isoToLocalDateTime(iso)).toBe(expectedString);
  });

  it('returns an empty string for invalid input', () => {
    expect(isoToLocalDateTime('')).toBe('');
    expect(isoToLocalDateTime(null)).toBe('');
    expect(isoToLocalDateTime('nope')).toBe('');
  });
});

describe('isoToLocalDate', () => {
  it('takes the calendar date as written by the server', () => {
    expect(isoToLocalDate('2026-09-30T00:00:00')).toBe('2026-09-30');
    expect(isoToLocalDate('2026-09-30')).toBe('2026-09-30');
    expect(isoToLocalDate('2026-09-30T23:00:00Z')).toBe('2026-09-30');
  });

  it('returns an empty string for invalid input', () => {
    expect(isoToLocalDate('')).toBe('');
    expect(isoToLocalDate(null)).toBe('');
    expect(isoToLocalDate('nope')).toBe('');
  });
});

describe('parseLocalInput / formatLocalInput', () => {
  it('reads a date-only value as the local calendar day', () => {
    // نکته: new Date('YYYY-MM-DD') به‌صورت UTC تفسیر می‌شود و در مناطق
    // زمانی شرقی/غربی یک روز جابه‌به می‌کند؛ این تابع نباید جابه‌به شود.
    expect(parseLocalInput('2026-09-30')).toEqual({ utcMs: Date.UTC(2026, 8, 30), hour: 0, minute: 0 });
  });

  it('reads the wall-clock time of a datetime value', () => {
    const parsed = parseLocalInput('2026-09-30T14:30')!;
    expect(parsed.utcMs).toBe(Date.UTC(2026, 8, 30));
    expect(parsed.hour).toBe(14);
    expect(parsed.minute).toBe(30);
  });

  it('formats back to the same string', () => {
    expect(formatLocalInput(Date.UTC(2026, 8, 30), 0, 0, false)).toBe('2026-09-30');
    expect(formatLocalInput(Date.UTC(2026, 8, 30), 14, 30, true)).toBe('2026-09-30T14:30');
  });

  it('round-trips through the string form', () => {
    const value = '2026-09-30T14:30';
    const parsed = parseLocalInput(value)!;
    expect(formatLocalInput(parsed.utcMs, parsed.hour, parsed.minute, true)).toBe(value);
  });

  it('rejects invalid input', () => {
    expect(parseLocalInput('')).toBeNull();
    expect(parseLocalInput('nope')).toBeNull();
  });
});

describe('todayLocalDate', () => {
  it('matches the local calendar day, not UTC', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-29T20:30:00.000Z'));

    const now = new Date();
    const expected = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;

    expect(todayLocalDate()).toBe(expected);

    vi.useRealTimers();
  });
});
