import { render as tlRender, screen, waitFor, fireEvent, within } from '@testing-library/react';
import { afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { SurveysPage } from './SurveysPage';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AuthProvider } from '@/auth/AuthProvider';
import { writeSession, clearSession, readSession } from '@/auth/session';
import { authApi } from '@/api/auth';
import { configureAuth, ApiError } from '@/api/client';
import { surveysApi, Language } from '@/api/surveys';
import { questionnairesApi, QuestionnaireStatus } from '@/api/questionnaires';
import { fa } from '@/i18n/dictionaries/fa';

/**
 * آزمون یکپارچه‌ی زنده برای جریان کامل ویرایش نظرسنجی:
 * ساخت → انتشار → باز کردن فرم ویرایش از کنار دکمه‌های توقف/بستن →
 * ذخیره → بررسی پیام موفقیت → بررسی ماندگاری تغییرات در پایگاه داده.
 *
 * این آزمون به سرور توسعه (پورت ۵۱۷۴) و API واقعی نیاز دارد؛ اگر در
 * دسترس نباشند، نادیده گرفته می‌شود تا CI بدون زیرساخت خرد نشود.
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
      <MemoryRouter initialEntries={['/fa/surveys']}>
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

describe('SurveysPage (live edit workflow)', () => {
  beforeEach(async () => {
    clearSession();

    try {
      const result = await authApi.login('fa', { userName: ADMIN_USER, password: ADMIN_PASS });
      if (!result.tokens) throw new Error('live login failed');

      writeSession({
        accessToken: result.tokens.accessToken,
        refreshToken: result.tokens.refreshToken,
        expiresAt: result.tokens.expiresAt
      });
    } catch (error) {
      // هنگام اجرای همزمان‌ی کل مجموعه‌ی آزمون‌ها، چندین آزمون زنده لاگین
      // می‌کنند و ممکن است سقف نرخ درخواست سرور (۴۲۹) را فعال کنند. در این
      // حالت آزمون نادیده گرفته می‌شود تا CI خرد نشود — مثل زمانی که سرور
      // در دسترس نیست.
      if (error instanceof ApiError && error.status === 429) {
        backendAvailable = false;
        return;
      }
      throw error;
    }

    // فراخوانی‌های مستقیمِ API پیش از رندر صفحه هم احراز هویت شوند؛
    // در غیر این صورت AuthProvider هنوز مونت نشده و توکنی به درخواست‌ها نمی‌چسبد.
    configureAuth(() => readSession()?.accessToken ?? null, async () => null);
  });

  afterEach(() => {
    clearSession();
  });

  it('edits a published survey and the change persists after reload', async () => {
    if (!backendAvailable) {
      console.warn('dev server/API در دسترس نیست — این آزمون زنده نادیده گرفته شد');
      return;
    }

    // پیش‌نیاز: یک پرسشنامه‌ی منتشرشده برای ساخت نظرسنجی.
    const questionnaires = await questionnairesApi.search('fa', {
      status: QuestionnaireStatus.Published,
      page: 1,
      pageSize: 10
    });
    const questionnaire = questionnaires.items[0];
    if (!questionnaire) {
      console.warn('پرسشنامه‌ی منتشرشده‌ای وجود ندارد — این آزمون زنده نادیده گرفته شد');
      return;
    }

    const code = `SV-LIVE-EDIT-${Date.now().toString(36).toUpperCase()}`;
    const originalTitle = `نظرسنجی زنده ${code}`;
    const updatedTitle = `${originalTitle} — ویرایش‌شده`;

    // یک نظرسنجی می‌سازیم و منتشر می‌کنیم تا به وضعیت «فعال» برسد؛ یعنی
    // همان وضعیتی که دکمه‌های توقف و بستن نشان داده می‌شوند.
    const created = await surveysApi.create('fa', {
      code,
      questionnaireId: questionnaire.id,
      localizations: [{ language: Language.Fa, title: originalTitle }],
      isAnonymous: false,
      allowEditResponse: true,
      showProgressBar: true,
      singleResponsePerUser: true,
      startDate: null,
      endDate: null,
      estimatedMinutes: 5
    });
    await surveysApi.publish('fa', created.id);

    try {
      render(<SurveysPage />);

      const row = await waitFor(() => {
        const cell = screen.getByText(code);
        return cell.closest('tr');
      }, { timeout: 15000 });

      expect(row).not.toBeNull();
      // ویرایش باید کنار توقف/بستن (منوی کرکره‌ای) قابل دسترسی باشد.
      expect(row!.querySelector('[aria-label="عملیات"]')).not.toBeNull();

      fireEvent.click(within(row!).getByRole('button', { name: fa.common.edit }));

      // فرم با مقادیر فعلی پیش‌بندی می‌شود.
      const titleInput = await screen.findByDisplayValue(originalTitle);
      expect(screen.getByRole('dialog')).toHaveAttribute('aria-labelledby', 'dialog-title');

      fireEvent.change(titleInput, { target: { value: updatedTitle } });
      fireEvent.click(screen.getByRole('button', { name: fa.common.save }));

      // پیام موفقیت روی صفحه نمایش داده می‌شود و دیالوگ بسته می‌شود.
      await waitFor(
        () => expect(screen.getByText(fa.surveys.surveyUpdated)).toBeInTheDocument(),
        { timeout: 15000 }
      );
      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull(), { timeout: 5000 });

      // فهرست پس از ذخیره نوسازی شده و عنوان جدید را نشان می‌دهد.
      await waitFor(() => expect(screen.getByText(updatedTitle)).toBeInTheDocument(), { timeout: 10000 });

      // ماندگاری در پایگاه‌داده با خواندن مستقیم از API بررسی می‌شود
      // (معادل «رفرش صفحه و دیدن تغییرات»).
      const persisted = await surveysApi.getById('fa', created.id);
      expect(persisted.title).toBe(updatedTitle);
      expect(persisted.code).toBe(code);
      expect(persisted.status).toBe(3 /* Active */);
    } finally {
      // تمیزکاری: بستن نظرسنجی آزمون.
      await surveysApi.close('fa', created.id).catch(() => undefined);
    }
  }, 90000);
});
