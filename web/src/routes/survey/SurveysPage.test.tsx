import { render as renderScreen, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { SurveysPage } from './SurveysPage';
import { SurveyStatus } from '@/api/surveys';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';

const SURVEYS = [
  { id: 'id-draft', code: 'SV-01', status: SurveyStatus.Draft, title: 'پیش‌نویس', questionnaireCode: 'QS-01', isAnonymous: false, startDate: null, endDate: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null },
  { id: 'id-scheduled', code: 'SV-02', status: SurveyStatus.Scheduled, title: 'زمان‌بندی‌شده', questionnaireCode: 'QS-01', isAnonymous: false, startDate: '2026-10-09T00:00:00Z', endDate: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null },
  { id: 'id-active', code: 'SV-03', status: SurveyStatus.Active, title: 'فعال', questionnaireCode: 'QS-01', isAnonymous: false, startDate: null, endDate: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null },
  { id: 'id-paused', code: 'SV-04', status: SurveyStatus.Paused, title: 'متوقف', questionnaireCode: 'QS-01', isAnonymous: false, startDate: null, endDate: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null },
  { id: 'id-closed', code: 'SV-05', status: SurveyStatus.Closed, title: 'بسته‌شده', questionnaireCode: 'QS-01', isAnonymous: false, startDate: null, endDate: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null },
  { id: 'id-archived', code: 'SV-06', status: SurveyStatus.Archived, title: 'بایگانی‌شده', questionnaireCode: 'QS-01', isAnonymous: false, startDate: null, endDate: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null }
];

vi.mock('@/api/hooks', () => ({
  queryKeys: { surveys: ['surveys'] },
  useSurveysSearch: () => ({
    data: { items: SURVEYS, totalCount: SURVEYS.length, page: 1, pageSize: 20 },
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn()
  }),
  useDeleteSurvey: () => ({ mutate: vi.fn(), isPending: false, error: null }),
  useSurveyLifecycle: () => ({ mutate: vi.fn(), isPending: false, error: null })
}));

vi.mock('@/auth/AuthProvider', () => ({
  useAuth: () => ({
    hasPermission: () => true,
    hasAnyPermission: () => true,
    user: null,
    logout: vi.fn()
  })
}));

function render(node: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false, staleTime: 0 } }
  });

  return renderScreen(node, {
    wrapper: ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={['/fa/surveys']}>
        <ThemeProvider>
          <LanguageProvider>
            <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
          </LanguageProvider>
        </ThemeProvider>
      </MemoryRouter>
    )
  });
}

describe('SurveysPage — edit action', () => {
  it('shows the edit button for draft, scheduled, active and paused surveys', () => {
    render(<SurveysPage />);

    const editButtons = screen.getAllByRole('button', { name: 'ویرایش' });
    expect(editButtons).toHaveLength(4);
  });

  it('hides the edit button for closed and archived surveys', () => {
    render(<SurveysPage />);

    const rows = screen.getAllByRole('row');
    const closedRow = rows.find((row) => row.textContent?.includes('SV-05'));
    const archivedRow = rows.find((row) => row.textContent?.includes('SV-06'));

    expect(closedRow?.querySelector('[aria-label="ویرایش"]')).toBeNull();
    expect(archivedRow?.querySelector('[aria-label="ویرایش"]')).toBeNull();
  });

  it('still renders the lifecycle menu (stop/close) next to the edit button', () => {
    render(<SurveysPage />);

    const rows = screen.getAllByRole('row');
    const activeRow = rows.find((row) => row.textContent?.includes('SV-03'));

    // دکمه‌ی ویرایش و منوی کرکره‌ایِ چرخه‌ی عمر هر دو باید کنار هم باشند.
    expect(activeRow?.querySelector('[aria-label="ویرایش"]')).not.toBeNull();
    expect(activeRow?.querySelector('[aria-label="عملیات"]')).not.toBeNull();
  });
});
