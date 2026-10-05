import { render as tlRender, screen, waitFor, fireEvent } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { UserDialog } from './UserDialog';
import { AppLayout } from '@/layouts/AppLayout';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AuthProvider } from '@/auth/AuthProvider';
import { clearSession, writeSession } from '@/auth/session';
import { configureAuth, clearAuth } from '@/api/client';

/**
 * تستِ رفتاری برای جریان تصویر پروفایل در «مدیریت کاربران».
 *
 * شبکه‌ی واقعی شبیه‌سازی می‌شود تا دقیقاً مشخص شود وقتی مدیر تصویر کاربری
 * را از دیالوگ مدیریت کاربران تغییر می‌دهد، چه درخواست‌هایی صادر می‌شود.
 *
 * نکته‌ی کلیدی: آواتار نوار بالا از پروفایل «کاربر جاری» خوانده می‌شود
 * (<c>useAuth().user.avatarUrl</c>) که فقط با <c>refreshUser()</c> به‌روز
 * می‌شود. وقتی مدیر تصویر «خودش» را تغییر می‌دهد، این پروفایل باید مثل
 * صفحه‌ی پروفایل، دوباره بارگذاری شود؛ در غیر این صورت آواتار نوار بالا
 * قدیمی می‌ماند.
 */

const OWN_USER_ID = '11111111-0000-0000-0000-000000000001';
const OTHER_USER_ID = '11111111-0000-0000-0000-000000000002';
const NEW_AVATAR = '/api/identity/users/OWN/avatar/updated.png';
const OTHER_NEW_AVATAR = '/api/identity/users/OTHER/avatar/updated.png';

const PNG_BYTES = new Uint8Array([
  0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
]);

function profileJson(avatarUrl: string | null) {
  return {
    id: OWN_USER_ID,
    userName: 'admin',
    email: 'admin@odcc.local',
    phoneNumber: null,
    firstName: 'مدیر',
    lastName: 'سامانه',
    displayName: 'مدیر سامانه',
    avatarUrl,
    isActive: true,
    emailConfirmed: true,
    twoFactorEnabled: false,
    orgUnitId: null,
    orgUnitName: null,
    dataScope: 4,
    roles: ['Admin'],
    permissions: ['identity.users.view', 'identity.users.edit']
  };
}

function summaryJson(id: string, avatarUrl: string | null) {
  return {
    id,
    userName: id === OWN_USER_ID ? 'admin' : 'other',
    email: 'admin@odcc.local',
    firstName: 'مدیر',
    lastName: 'سامانه',
    displayName: 'مدیر سامانه',
    avatarUrl,
    isActive: true,
    emailConfirmed: true,
    orgUnitId: null,
    orgUnitName: null,
    dataScope: 4,
    createdAt: '2026-01-01T00:00:00Z',
    roles: ['Admin']
  };
}

let requestedUrls: string[] = [];
let meResponseAvatar: string | null;

function render(node: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false, staleTime: 0 } }
  });

  return tlRender(node, {
    wrapper: ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={['/fa/identity/users']}>
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

function avatarImages(scope: Document | HTMLElement): HTMLImageElement[] {
  return Array.from(scope.querySelectorAll<HTMLImageElement>('img.rounded-full'));
}

/** آواتار نوار بالای برنامه (کنار نام کاربر) — اندازه‌ی sm معادل size-8 است. */
function headerAvatarImages(container: HTMLElement): HTMLImageElement[] {
  return avatarImages(container).filter((img) => img.className.includes('size-8'));
}

/** آواتارِ داخل دیالوگ (اندازه‌ی lg معادل size-20)؛ دیالوگ در پورتال رندر می‌شود. */
function dialogAvatarImages(): HTMLImageElement[] {
  return avatarImages(document.body).filter((img) => img.className.includes('size-20'));
}

function jsonReponse(body: unknown): Response {
  return {
    ok: true,
    status: 200,
    headers: new Headers({ 'content-type': 'application/json' }),
    json: async () => body
  } as Response;
}

function blobResponse(): Response {
  return {
    ok: true,
    status: 200,
    headers: new Headers({ 'content-type': 'image/png' }),
    blob: async () => new Blob([PNG_BYTES], { type: 'image/png' })
  } as Response;
}

describe('UserDialog avatar (real hooks + providers, mocked network)', () => {
  beforeEach(() => {
    configureAuth(() => 'fake-access-token', async () => 'fake-refresh-token');

    // نشست معتبر تا AuthProvider در راه‌اندازی پروفایل را بارگذاری کند.
    writeSession({
      accessToken: 'fake-access-token',
      refreshToken: 'fake-refresh-token',
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
    });

    meResponseAvatar = null;
    requestedUrls = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input.toString();
        requestedUrls.push(url);
        const method = init?.method ?? 'GET';

        // پروفایل کاربر جاری — منبع آواتار نوار بالا.
        if (url.includes('/identity/users/me') && method === 'GET') {
          return jsonReponse(profileJson(meResponseAvatar));
        }

        // آپلود آواتار کاربر توسط مدیر: نشانیِ کاربر به‌روز می‌شود و اگر
        // کاربرِ ویرایش‌شده خودِ کاربر جاری باشد، پروفایل هم باید دوباره
        // خوانده شود (این رفتارِ تحتِ آزمون است).
        if (method === 'POST' && url.match(/\/identity\/users\/[^/]+\/avatar$/)) {
          const isOwn = url.includes(OWN_USER_ID);
          if (isOwn) {
            meResponseAvatar = NEW_AVATAR;
          }
          return jsonReponse(summaryJson(isOwn ? OWN_USER_ID : OTHER_USER_ID, isOwn ? NEW_AVATAR : OTHER_NEW_AVATAR));
        }

        // دانلود خودِ تصویر آواتار.
        if (url.includes('/avatar/') && method === 'GET') {
          return blobResponse();
        }

        // سایر درخواست‌ها (مثلاً لیست واحدهای سازمانی).
        return jsonReponse([]);
      })
    );
  });

  afterEach(() => {
    clearAuth();
    clearSession();
    vi.unstubAllGlobals();
  });

  it('refreshes the current user when the admin edits their own avatar so the header avatar updates', async () => {
    const { container } = render(
      <AppLayout>
        <UserDialog open={true} onClose={() => undefined} user={summaryJson(OWN_USER_ID, null)} />
      </AppLayout>
    );

    // پروفایل بارگذاری می‌شود و نام کاربر در نوار بالا نمایش داده می‌شود.
    await waitFor(
      () => expect(screen.getAllByText('مدیر سامانه').length).toBeGreaterThan(0),
      { timeout: 10000 }
    );

    const fileInput = document.body.querySelector('input[type="file"]');
    expect(fileInput).not.toBeNull();

    // قبل از بارگذاری، کاربر تصویری ندارد؛ آواتار نوار بالا حروف اول است.
    expect(headerAvatarImages(container).length).toBe(0);
    expect(requestedUrls.filter((u) => u.includes('/identity/users/me')).length).toBe(1);

    fireEvent.change(fileInput!, {
      target: { files: [new File([PNG_BYTES], 'avatar.png', { type: 'image/png' })] }
    });

    // رفتارِ مورد انتظار: پس از آپلود، پروفایل کاربر جاری دوباره بارگذاری
    // می‌شود (درخواست دوم به /me) و آواتار نوار بالا تصویر جدید را نشان می‌دهد.
    await waitFor(
      () => {
        const headerImages = headerAvatarImages(container);
        expect(headerImages.length).toBe(1);
        expect(headerImages[0].src).toMatch(/^data:image\//);
      },
      { timeout: 10000 }
    );

    const meRequests = requestedUrls.filter((u) => u.includes('/identity/users/me'));
    expect(meRequests.length).toBeGreaterThanOrEqual(2);
  });

  it('leaves the current user untouched when the admin edits another user', async () => {
    const { container } = render(
      <AppLayout>
        <UserDialog open={true} onClose={() => undefined} user={summaryJson(OTHER_USER_ID, null)} />
      </AppLayout>
    );

    await waitFor(
      () => expect(screen.getAllByText('مدیر سامانه').length).toBeGreaterThan(0),
      { timeout: 10000 }
    );

    const fileInput = document.body.querySelector('input[type="file"]');
    expect(fileInput).not.toBeNull();

    expect(headerAvatarImages(container).length).toBe(0);
    const meBefore = requestedUrls.filter((u) => u.includes('/identity/users/me')).length;

    fireEvent.change(fileInput!, {
      target: { files: [new File([PNG_BYTES], 'avatar.png', { type: 'image/png' })] }
    });

    // آواتارِ داخل دیالوگِ کاربرِ دیگر به‌روز می‌شود.
    await waitFor(
      () => {
        expect(dialogAvatarImages().length).toBe(1);
      },
      { timeout: 10000 }
    );

    // ولی پروفایل کاربر جاری دوباره بارگذاری نمی‌شود چون کاربرِ ویرایش‌شده
    // کاربر جاری نیست؛ آواتار نوار بالا دست‌نخورده می‌ماند.
    const meAfter = requestedUrls.filter((u) => u.includes('/identity/users/me')).length;
    expect(meAfter).toBe(meBefore);
    expect(headerAvatarImages(container).length).toBe(0);
  });
});
