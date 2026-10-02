import { render as tlRender, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { OrgUnitsPage } from './OrgUnitsPage';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { configureAuth, clearAuth } from '@/api/client';

const TREE_JSON = JSON.stringify([
  {
    id: '01a0d412-c716-7c18-8d85-68187b170e3d',
    code: 'HQ',
    name: 'سازمان مرکزی',
    type: 1,
    level: 0,
    isActive: true,
    managerEmployeeId: null,
    managerFullName: null,
    children: [
      { id: '11111111-0000-0000-0000-000000000002', code: 'HR', name: 'منابع انسانی', type: 3, level: 1, isActive: true, managerEmployeeId: null, managerFullName: null, children: [] },
      { id: '11111111-0000-0000-0000-000000000001', code: 'IT', name: 'فناوری اطلاعات', type: 3, level: 1, isActive: true, managerEmployeeId: null, managerFullName: null, children: [] },
      { id: '11111111-0000-0000-0000-000000000004', code: 'OPS', name: 'عملیات و پشتیبانی', type: 3, level: 1, isActive: true, managerEmployeeId: null, managerFullName: null, children: [] },
      { id: '11111111-0000-0000-0000-000000000003', code: 'SAL', name: 'فروش و بازاریابی', type: 3, level: 1, isActive: true, managerEmployeeId: null, managerFullName: null, children: [] }
    ]
  }
]);

vi.mock('@/auth/AuthProvider', () => ({
  useAuth: () => ({ hasPermission: () => true, hasAnyPermission: () => true })
}));

let requestedUrls: string[] = [];

function render(node: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false, staleTime: 0 } }
  });

  return {
    ...tlRender(node, {
      wrapper: ({ children }: { children: ReactNode }) => (
        <MemoryRouter initialEntries={['/fa']}>
          <ThemeProvider>
            <LanguageProvider>
              <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
            </LanguageProvider>
          </ThemeProvider>
        </MemoryRouter>
      )
    })
  };
}

describe('OrgUnitsPage (real hooks + client, mocked network)', () => {
  beforeEach(() => {
    configureAuth(() => 'fake-access-token', async () => 'fake-refresh-token');
    requestedUrls = [];
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        const url = typeof input === 'string' ? input : input.toString();
        requestedUrls.push(url);
        return {
          ok: true,
          status: 200,
          headers: new Headers({ 'content-type': 'application/json' }),
          json: async () => JSON.parse(TREE_JSON)
        } as Response;
      })
    );
  });

  afterEach(() => {
    clearAuth();
    vi.unstubAllGlobals();
  });

  it('requests the tree endpoint and displays every unit', async () => {
    render(<OrgUnitsPage />);

    expect(requestedUrls.some((u) => u.includes('/organization/units/tree'))).toBe(true);

    // درخت به‌صورت پیش‌فرض باز است، پس همه‌ی واحدها دیده می‌شوند.
    await waitFor(() => expect(screen.getByText('HQ')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('HR')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('IT')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('OPS')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('SAL')).toBeInTheDocument());
  });
});
