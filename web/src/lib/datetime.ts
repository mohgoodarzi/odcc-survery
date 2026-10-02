/**
 * تبدیل بین مقادیر ورودی تاریخ و ISO ۸۶۰۱ سمت سرور.
 *
 * قرارداد: تاریخ‌ها همواره به‌صورت میلادی/UTC در پایگاه داده ذخیره می‌شوند
 * و فقط در لایه‌ی نمایش به شمسی تبدیل می‌شوند. این ماژول مرز بین
 * مقدار ورودی فرم و رشته‌ی ارسالی به API است.
 *
 * «مقدار ورودی محلی» یعنی همان شکلی که ورودی‌های بومی تاریخ دارند:
 * - تاریخ تنها: `YYYY-MM-DD`
 * - تاریخ و زمان: `YYYY-MM-DDTHH:mm` (زمان بدون منطقه‌ی زمانی،
 *   معادل زمان روی دیوار کاربر تفسیر می‌شود)
 */

const pad = (value: number) => String(value).padStart(2, '0');

/**
 * روز جاری را بر اساس منطقه‌ی زمانی کاربر در قالب `YYYY-MM-DD` برمی‌گرداند.
 * (نه UTC، تا برای کاربران غرب/شرق گرینویچ یک روز جابه‌جا نشود.)
 */
export function todayLocalDate(): string {
  const now = new Date();
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/**
 * مقدار ورودی تاریخ‌دار محلی را به ISO ۸۶۰۱ با منطقه‌ی UTC تبدیل می‌کند.
 * کاربر زمان را به زمان محلی خود انتخاب می‌کند و سرور در UTC کار می‌کند.
 */
export function localDateTimeToIso(localDateTime: string | null | undefined): string | null {
  if (!localDateTime) return null;

  const parsed = new Date(localDateTime);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

/**
 * ISO ۸۶۰۱ سمت سرور را به مقدار ورودی datetime-local محلی تبدیل می‌کند.
 */
export function isoToLocalDateTime(iso: string | null | undefined): string {
  if (!iso) return '';

  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';

  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/**
 * ISO ۸۶۰۱ سمت سرور را به تاریخ تنها (`YYYY-MM-DD`) تبدیل می‌کند.
 *
 * فیلد تاریخ در پایگاه داده نیمه‌شب روز را نگه می‌دارد و سرور آن را بدون
 * منطقه‌ی زمانی ارسال می‌کند، پس بخش تاریخ رشته مستقیماً استفاده می‌شود
 * تا روز تقویمی در هر منطقه‌ی زمانی ثابت بماند.
 */
export function isoToLocalDate(iso: string | null | undefined): string {
  if (!iso) return '';

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso.trim());
  if (!match) return '';

  return `${match[1]}-${match[2]}-${match[3]}`;
}

export interface ParsedLocalInput {
  /** نیمه‌شب (UTC) روز تقویمی روی دیوار کاربر. */
  utcMs: number;
  hour: number;
  minute: number;
}

/**
 * مقدار ورودی محلی را به روز تقویمی و زمان جداگانه تجزیه می‌کند.
 *
 * نکته‌ی ظریف: `new Date('YYYY-MM-DD')` تاریخ را به‌صورت UTC تفسیر می‌کند،
 * پس در مناطق زمانی شرقی یا غربی یک روز جابه‌به می‌شود. به همین دلیل
 * «T00:00:00» اضافه می‌شود تا تاریخ به‌صورت زمان محلی خوانده شود.
 */
export function parseLocalInput(value: string): ParsedLocalInput | null {
  if (!value) return null;

  const date = new Date(value.length === 10 ? `${value}T00:00:00` : value);
  if (Number.isNaN(date.getTime())) return null;

  return {
    utcMs: Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()),
    hour: date.getHours(),
    minute: date.getMinutes()
  };
}

/**
 * روز تقویمی و زمان را دوباره به مقدار ورودی محلی تبدیل می‌کند.
 */
export function formatLocalInput(
  utcMs: number,
  hour: number,
  minute: number,
  withTime: boolean
): string {
  const date = new Date(utcMs);
  const datePart = `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;

  return withTime ? `${datePart}T${pad(hour)}:${pad(minute)}` : datePart;
}
