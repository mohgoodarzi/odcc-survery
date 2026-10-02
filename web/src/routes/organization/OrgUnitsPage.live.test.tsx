import { render as tlRender, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { OrgUnitsPage } from './OrgUnitsPage';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AuthProvider } from '@/auth/AuthProvider';
import { writeSession, clearSession } from '@/auth/session';
import { authApi } from '@/api/auth';

/**
 * تست یکپارچه‌ی زنده: صفحه‌ی واحدهای سازمانی با ارائه‌دهنده‌های واقعی،
 * hookهای واقعی و کلاینت API واقعی رندر می‌شود و درخواست‌ها از طریق
 * پروکسی سرور توسعه (پورت ۵۱۷۴) به API واقعی می‌رسند. فقط رندر در jsdom
 * است؛ شبکه کاملاً واقعی است.
 */
const DEV_ORIGIN = 'http://localhost:5174';
const ADMIN_USER = 'admin';
const ADMIN_PASS = 'DevAdmin2026!StrongPass';

let realFetch: typeof globalThis.fetch;

let backendAvailable = false;

beforeAll(async () => {
  realFetch = globalThis.fetch;

  // ارسال درخواست‌های نسبی به سرور توسعه (و از آنجا به API واقعی).
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input.toString();
    const absolute = url.startsWith('/') ? `${DEV_ORIGIN}${url}` : url;
    return realFetch(absolute, init);
  }) as typeof globalThis.fetch;

  // این آزمون به سرور توسعه و API واقعی نیاز دارد؛ اگر در دسترس نباشند،
  // آزمون نادیده گرفته می‌شود تا CI بدون زیرساخت خرد نشود.
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

  const base = tlRender(node, {
    wrapper: ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={['/fa/organization/units']}>
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

  return base;
}

describe('OrgUnitsPage (live API through dev-server proxy)', () => {
  beforeEach(async () => {
    clearSession();

    // ورود واقعی به سامانه برای گرفتن توکن معتبر.
    const result = await authApi.login('fa', { userName: ADMIN_USER, password: ADMIN_PASS });
    if (!result.tokens) throw new Error('live login failed');

    writeSession({
      accessToken: result.tokens.accessToken,
      refreshToken: result.tokens.refreshToken,
      expiresAt: result.tokens.expiresAt
    });
  });

  afterEach(() => {
    clearSession();
  });

  it('displays every existing org unit from the database', async () => {
    if (!backendAvailable) {
      console.warn('dev server/API در دسترس نیست — این آزمون زنده نادیده گرفته شد');
      return;
    }

    render(<OrgUnitsPage />);

    // درخت به‌صورت پیش‌فرض باز است، پس هر واحدی که در پایگاه داده وجود دارد
    // (و در dropdown فرم کارمند هم دیده می‌شود) در صفحه‌ی مدیریت هم دیده می‌شود.
    await waitFor(() => expect(screen.getByText('HQ')).toBeInTheDocument(), { timeout: 15000 });
    await waitFor(() => expect(screen.getByText('HR')).toBeInTheDocument(), { timeout: 15000 });
    await waitFor(() => expect(screen.getByText('IT')).toBeInTheDocument(), { timeout: 15000 });
    await waitFor(() => expect(screen.getByText('OPS')).toBeInTheDocument(), { timeout: 15000 });
    await waitFor(() => expect(screen.getByText('SAL')).toBeInTheDocument(), { timeout: 15000 });
  }, 60000);
});
