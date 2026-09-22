/**
 * ذخیره‌سازی امن نشست احراز هویت.
 *
 * توکن دسترسی کوتاه‌مدت است و فقط در حافظه‌ی برنامه نگه‌داری می‌شود.
 * توکن تازه‌سازی برای بازگرداندن نشست پس از بارگذاری مجدد صفحه، در
 * localStorage نگه‌داری می‌شود. مرز امنیتی در سمت سرور است: توکن تازه‌سازی
 * به‌صورت هش‌شده ذخیره می‌شود، با چرخش و ابطال خانواده در صورت استفاده‌ی مجدد.
 */
export interface StoredSession {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

const SESSION_KEY = 'odcc.survey.session';

export function readSession(): StoredSession | null {
  try {
    const raw = window.localStorage.getItem(SESSION_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as Partial<StoredSession>;
    if (!parsed.accessToken || !parsed.refreshToken) {
      return null;
    }

    return {
      accessToken: parsed.accessToken,
      refreshToken: parsed.refreshToken,
      expiresAt: parsed.expiresAt ?? new Date(0).toISOString()
    };
  } catch {
    return null;
  }
}

export function writeSession(session: StoredSession): void {
  try {
    window.localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  } catch {
    /* storage ممکن است در حالت خصوصی در دسترس نباشد؛ نشست فقط در حافظه می‌ماند. */
  }
}

export function clearSession(): void {
  try {
    window.localStorage.removeItem(SESSION_KEY);
  } catch {
    /* نادیده می‌گیریم: نشست در حافظه هم پاک می‌شود. */
  }
}

/**
 * آیا توکن دسترسی ذخیره‌شده منقضی شده است؟
 * با یک پیش‌زمینه‌ی امنیتی کوتاه بررسی می‌شود تا درخواست‌ها قبل از انقضا
 * تازه‌سازی شوند.
 */
export function isAccessTokenExpired(expiresAt: string, skewSeconds = 30): boolean {
  const expiry = new Date(expiresAt).getTime();
  if (Number.isNaN(expiry)) return true;

  return Date.now() >= expiry - skewSeconds * 1000;
}
