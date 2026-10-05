import { render as tlRender, screen, waitFor, fireEvent } from '@testing-library/react';
import { afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ProfilePage } from './ProfilePage';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AuthProvider } from '@/auth/AuthProvider';
import { writeSession, clearSession } from '@/auth/session';
import { authApi } from '@/api/auth';
import { serializeBodyIfNeeded } from '@/test-support/multipart-upload';

/**
 * تست یکپارچه‌ی زنده برای جریان کامل تصویر پروفایل:
 *
 *   بارگذاری تصویر در UI واقعی ← ذخیره در انبار فایل‌ها ← ذخیره مسیر در
 *   پایگاه داده ← پاسخ API ← نگاشت در frontend ← نمایش تصویر کنار نام کاربر
 *
 * صفحه‌ی پروفایل واقعی (همراه با AppLayout که آواتار نوار بالاست) با
 * ارائه‌دهنده‌های واقعی رندر می‌شود و درخواست‌ها از طریق پروکسی سرور توسعه
 * (پورت ۵۱۷۴) به API واقعی می‌رسند. تصویر نمونه یک PNG واقعی است تا
 * اعتبارسنجی magic bytes سرور هم اجرا شود.
 */
const DEV_ORIGIN = 'http://localhost:5174';
const ADMIN_USER = 'admin';
const ADMIN_PASS = 'DevAdmin2026!StrongPass';

/** PNG نمونه‌ی اول (پس‌زمینه‌ی آبی با دایره‌ی زرد). */
const SAMPLE_PNG_1 =
  'iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAF2SURBVHhe7dDLbcQwDAXArSR1psR0tYEOAYKBvJb1sSWIh7nwQD6+19f3z3tnLwe7iQIc7CYKcLCbKMDBbqIAB6O836/L3DHC0AJ8qIW7exlSgOF78larrgUYdiRv1+pWgAHvYIYaXQow2J3MclVzAQZ6gpmuaCrAIE8yW6nqAgwwAzOWqCrAwzMx65nLBXhwRmb+JApw8ImHZmb2I1GAgyMeWIE/5EQBDo64fAX+kFNUgItX4i+KAhzkuHQl/qIowEGOS1fiL4oCHMiFq/EfnRaQuHQl/qIowEGOS1fiL4oCHOS4dCX+oijAwREXr8AfcqIAB0dcvgJ/yCkuIPHAzMx+JApwcMZDMzLzJ1GAgxIenIlZz1QVkHh4BmYsUV1AYoAnma1UUwGJQZ5gpiuaC0gMdCezXNWlgMRgdzBDjW4FJAYcydu1uhbwx7A9eavVkAL+GL6Fu3sZWsB/PlTCHSPcVsCsogAHu4kCHOwmCnCwm+0L+AWD1TE3jyIsmAAAAABJRU5ErkJggg==';

/** PNG نمونه‌ی دوم (پس‌زمینه‌ی قرمز با مربع سفید) — برای تست تعویض تصویر. */
const SAMPLE_PNG_2 =
  'iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAADQSURBVHhe7ZCxDQMBEIN+nN9/imyV9HQoxRXGEg2VxfN53+8yD8UaBaBYowAUaxSAYo0CUKxRAIo1CkCxRgEo1vg7wPX4x1IACsv1+MdSAArL9fjHUgAKy/X4x1IACsv1+MdSAArL9fjHUgAKy/X4x1IACsv1+MdSAArL9fjHUgAKy/X4x1IACsv1+MdSAArL9fjHUgAKy/X4x1IACsv1+MdSAArL9fjHUgAKy/X4x1IAijUKQLFGASjWKADFGgWgWKMAFGsUgGKNAlCsMR/gBxr2BmgigtK/AAAAAElFTkSuQmCC';

let realFetch: typeof globalThis.fetch;
let backendAvailable = false;

beforeAll(async () => {
  realFetch = globalThis.fetch;

  // ارسال درخواست‌های نسبی به سرور توسعه (و از آنجا به API واقعی).
  // بدنه‌های FormData (آپلود آواتار) به multipart واقعی تبدیل می‌شوند، چون
  // File/FormData خود jsdom برای fetchِ Node قابل سریال‌سازی نیست.
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input.toString();
    const absolute = url.startsWith('/') ? `${DEV_ORIGIN}${url}` : url;
    return realFetch(absolute, await serializeBodyIfNeeded(init));
  }) as typeof globalThis.fetch;

  try {
    const probe = await realFetch(`${DEV_ORIGIN}/api/fa/auth/login`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ userName: ADMIN_USER, password: ADMIN_PASS })
    });
    backendAvailable = probe.ok;
  } catch {
    backendAvailable = false;
  }
});

function render(node: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false, staleTime: 0 } }
  });

  return tlRender(node, {
    wrapper: ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={['/fa/profile']}>
        <ThemeProvider>
          <LanguageProvider>
            <QueryClientProvider client={queryClient}>
              <AuthProvider>{children}</AuthProvider>
            </QueryClientProvider>
          </LanguageProvider>
        </ThemeProvider>
      </MemoryRouter>
    )
  });
}

/** ساخت یک فایل PNG واقعی از رشته‌ی base64 (تا magic bytes سرور قبول شود). */
function pngFile(base64: string, name: string): File {
  const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
  return new File([bytes], name, { type: 'image/png' });
}

/** همه‌ی تصاویر آواتارِ نمایش‌داده‌شده در صفحه. */
function avatarImages(container: HTMLElement): HTMLImageElement[] {
  return Array.from(container.querySelectorAll<HTMLImageElement>('img.rounded-full'));
}

async function login() {
  clearSession();

  const result = await authApi.login('fa', { userName: ADMIN_USER, password: ADMIN_PASS });
  if (!result.tokens) throw new Error('live login failed');

  writeSession({
    accessToken: result.tokens.accessToken,
    refreshToken: result.tokens.refreshToken,
    expiresAt: result.tokens.expiresAt
  });

  return { profile: result.tokens.profile, accessToken: result.tokens.accessToken };
}

describe('ProfilePage avatar (live API through dev-server proxy)', () => {
  let currentAccessToken: string | null = null;

  beforeEach(async () => {
    const { accessToken } = await login();
    currentAccessToken = accessToken;
  });

  afterEach(async () => {
    clearSession();

    // برگرداندن وضعیت اولیه: تصویر آواتار آزمایشی حذف می‌شود تا آزمون
    // قابل تکرار باشد و داده‌ی نمونه روی حساب مدیر باقی نماند.
    if (backendAvailable && currentAccessToken) {
      try {
        await realFetch(`${DEV_ORIGIN}/api/fa/identity/users/me/avatar`, {
          method: 'DELETE',
          headers: { Authorization: `Bearer ${currentAccessToken}` }
        });
      } catch {
        /* بهترین‌حالت؛ وضعیت نشست مهم‌تر است. */
      }
    }

    currentAccessToken = null;
  });

  it('uploads a sample image in the real UI and displays it next to the user name', async () => {
    if (!backendAvailable) {
      console.warn('dev server/API در دسترس نیست — این آزمون زنده نادیده گرفته شد');
      return;
    }

    const { container } = render(<ProfilePage />);

    // انتظار: پروفایل بارگذاری شده و نام کاربر نمایش داده می‌شود (هم در نوار
    // بالا، هم در سرصفحه‌ی کارت پروفایل).
    await waitFor(
      () => expect(screen.getAllByText('مدیر سامانه').length).toBeGreaterThan(0),
      { timeout: 15000 }
    );

    // ورودی فایلِ واقعیِ کامپوننت AvatarUploader.
    const fileInput = container.querySelector('input[type="file"]');
    expect(fileInput).not.toBeNull();

    // بارگذاری تصویر نمونه از طریق UI واقعی (دقیقاً مثل انتخاب فایل توسط کاربر).
    fireEvent.change(fileInput!, {
      target: { files: [pngFile(SAMPLE_PNG_1, 'sample-avatar-1.png')] }
    });

    // انتظار: تصویر از سرور دانلود شده و به data URL تبدیل می‌شود و در
    // کنار نام کاربر (چه در صفحه‌ی پروفایل، چه در نوار بالا) نمایش داده می‌شود.
    await waitFor(
      () => {
        const images = avatarImages(container);
        const shown = images.filter((img) => img.src.startsWith('data:image/'));
        expect(shown.length).toBeGreaterThan(0);
      },
      { timeout: 20000 }
    );

    // آواتار نوار بالا (کنار نام کاربر) باید تصویر باشد، نه حروف اول.
    const headerImages = avatarImages(container).filter((img) =>
      img.className.includes('size-8')
    );
    expect(headerImages.length).toBe(1);
    expect(headerImages[0].src).toMatch(/^data:image\//);
  }, 90000);

  it('swaps to a second sample image and keeps it after a fresh page load', async () => {
    if (!backendAvailable) {
      console.warn('dev server/API در دسترس نیست — این آزمون زنده نادیده گرفته شد');
      return;
    }

    const { container } = render(<ProfilePage />);

    await waitFor(
      () => expect(screen.getAllByText('مدیر سامانه').length).toBeGreaterThan(0),
      { timeout: 15000 }
    );

    // تصویر اول.
    fireEvent.change(container.querySelector('input[type="file"]')!, {
      target: { files: [pngFile(SAMPLE_PNG_1, 'sample-avatar-1.png')] }
    });

    let firstDataUrl = '';

    await waitFor(
      () => {
        const shown = avatarImages(container).filter((img) =>
          img.src.startsWith('data:image/')
        );
        expect(shown.length).toBeGreaterThan(0);
        firstDataUrl = shown[0].src;
      },
      { timeout: 20000 }
    );

    // تصویر دوم: آواتار باید عوض شود.
    fireEvent.change(container.querySelector('input[type="file"]')!, {
      target: { files: [pngFile(SAMPLE_PNG_2, 'sample-avatar-2.png')] }
    });

    await waitFor(
      () => {
        const shown = avatarImages(container).filter((img) =>
          img.src.startsWith('data:image/')
        );
        expect(shown.length).toBeGreaterThan(0);
        expect(shown[0].src).not.toEqual(firstDataUrl);
      },
      { timeout: 20000 }
    );

    // شبیه‌سازی «بارگذاری مجدد صفحه»: نشست پاک می‌شود و ورود دوباره با نشست
    // تازه انجام می‌شود؛ آواتار باید از سرور دوباره بارگذاری و نمایش داده شود.
    const { profile, accessToken } = await login();
    expect(profile?.avatarUrl).toMatch(/\/avatar\/.+\.png$/);

    const avatarResponse = await realFetch(`${DEV_ORIGIN}${profile!.avatarUrl}`, {
      headers: { Authorization: `Bearer ${accessToken}` }
    });
    expect(avatarResponse.status).toBe(200);
    expect(avatarResponse.headers.get('content-type')).toContain('image/');
  }, 90000);
});
