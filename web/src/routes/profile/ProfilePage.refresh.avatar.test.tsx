import { render as tlRender, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ProfilePage } from './ProfilePage';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AuthProvider } from '@/auth/AuthProvider';
import { writeSession, clearSession } from '@/auth/session';
import { authApi } from '@/api/auth';
import { serializeMultipartFormData } from '@/test-support/multipart-upload';

/**
 * Live test for the "page refresh" avatar flow:
 *
 *   avatar already stored on the account (upload done beforehand through the API)
 *   <- fresh page load (session in localStorage)
 *   <- AuthProvider restores the session (GET /identity/users/me)
 *   <- header avatar must render the image
 */
const DEV_ORIGIN = 'http://localhost:5174';
const ADMIN_USER = 'admin';
const ADMIN_PASS = 'DevAdmin2026!StrongPass';

// minimal valid 1x1 PNG
const PNG_BYTES = new Uint8Array([
  0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
  0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1f, 0x15, 0xc4,
  0x89, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9c, 0x62, 0x00, 0x01, 0x00, 0x00,
  0x05, 0x00, 0x01, 0x0d, 0x0a, 0x2d, 0xb4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae,
  0x42, 0x60, 0x82
]);

let realFetch: typeof globalThis.fetch;
let backendAvailable = false;
let accessToken: string | null = null;

beforeAll(async () => {
  realFetch = globalThis.fetch;

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

afterAll(async () => {
  // cleanup: remove the test avatar so the admin account stays pristine
  if (backendAvailable && accessToken) {
    try {
      await realFetch(`${DEV_ORIGIN}/api/fa/identity/users/me/avatar`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${accessToken}` }
      });
    } catch {
      /* best effort */
    }
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

function avatarImages(container: HTMLElement): HTMLImageElement[] {
  return Array.from(container.querySelectorAll<HTMLImageElement>('img.rounded-full'));
}

async function login(): Promise<string> {
  const result = await authApi.login('fa', { userName: ADMIN_USER, password: ADMIN_PASS });
  if (!result.tokens) throw new Error('live login failed');

  writeSession({
    accessToken: result.tokens.accessToken,
    refreshToken: result.tokens.refreshToken,
    expiresAt: result.tokens.expiresAt
  });

  accessToken = result.tokens.accessToken;
  return accessToken;
}

describe('avatar after page refresh (live API)', () => {
  afterEach(() => {
    clearSession();
  });

  it('renders the stored avatar on a fresh page load', async () => {
    if (!backendAvailable) {
      console.warn('dev server / API unavailable — live test skipped');
      return;
    }

    // 1) store an avatar on the account beforehand (via API)
    const token = await login();

    const formData = new FormData();
    formData.append('File', new File([PNG_BYTES], 'avatar.png', { type: 'image/png' }));

    const serialized = await serializeMultipartFormData(formData);
    const upload = await realFetch(`${DEV_ORIGIN}/api/fa/identity/users/me/avatar`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'content-type': serialized.contentType
      },
      body: serialized.body
    });
    expect(upload.status).toBe(200);
    const uploaded = (await upload.json()) as { avatarUrl: string | null };
    expect(uploaded.avatarUrl).toMatch(/\.png$/);

    // 2) simulate a page reload: a session is present, the app boots fresh
    clearSession();
    await login();

    // 3) rendering the page runs the session-restore path (GET /identity/users/me)
    const { container } = render(<ProfilePage />);

    await waitFor(
      () => expect(screen.getAllByText('مدیر سامانه').length).toBeGreaterThan(0),
      { timeout: 20000 }
    );

    // 4) the header avatar must be the downloaded image, not the initials fallback
    await waitFor(
      () => {
        const headerImages = avatarImages(container).filter((img) =>
          img.className.includes('size-8')
        );
        const shown = headerImages.filter((img) => img.src.startsWith('data:image/'));
        expect(shown.length).toBeGreaterThan(0);
      },
      { timeout: 30000 }
    );
  }, 120000);
});
