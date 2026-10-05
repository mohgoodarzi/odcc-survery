import { render as renderScreen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { SurveyDialog } from './SurveyDialog';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { QuestionnaireStatus } from '@/api/questionnaires';
import { SurveyStatus } from '@/api/surveys';

// پرسشنامه‌ی «فعال» که در فهرست گزینه‌ها هست و پرسشنامه‌ی «بایگانی‌شده» که
// نظرسنجی به آن وصل است اما دیگر در فهرست پرسشنامه‌های فعال نیست.
const ACTIVE_QUESTIONNAIRE = {
  id: 'q-active',
  code: 'QS-ACTIVE',
  status: QuestionnaireStatus.Active,
  version: 1,
  title: 'پرسشنامه فعال',
  sectionCount: 1,
  itemCount: 2,
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: null
};

const ARCHIVED_QUESTIONNAIRE = {
  id: 'q-archived',
  code: 'QS-ARCHIVED',
  status: QuestionnaireStatus.Archived,
  version: 1,
  title: 'پرسشنامه بایگانی‌شده',
  sectionCount: 1,
  itemCount: 2,
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: null
};

const SURVEY = {
  id: 'sv-1',
  code: 'SV-01',
  status: SurveyStatus.Draft,
  questionnaireId: ARCHIVED_QUESTIONNAIRE.id,
  questionnaireVersion: 1,
  questionnaireCode: ARCHIVED_QUESTIONNAIRE.code,
  templateId: null,
  isAnonymous: false,
  allowEditResponse: true,
  showProgressBar: true,
  singleResponsePerUser: true,
  startDate: null,
  endDate: null,
  estimatedMinutes: 5,
  title: 'نظرسنجی نمونه',
  description: null,
  welcomeMessage: null,
  thankYouMessage: null,
  localizations: [{ language: 1, title: 'نظرسنجی نمونه' }],
  isPublishable: true,
  acceptsResponses: false,
  publishedAt: null,
  activatedAt: null,
  closedAt: null,
  archivedAt: null,
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: null
};

vi.mock('@/api/hooks', () => ({
  queryKeys: { surveys: ['surveys'], survey: (id: string | null) => ['surveys', 'detail', id] },
  // فهرست گزینه‌ها فقط پرسشنامه‌های «فعال» را برمی‌گرداند (رفتار اصلی).
  useQuestionnaires: () => ({ data: { items: [ACTIVE_QUESTIONNAIRE], totalCount: 1, page: 1, pageSize: 100 } }),
  useSurvey: () => ({ data: SURVEY }),
  // پرسشنامه‌ی فعلیِ نظرسنجی، حتی اگر بایگانی شده باشد، قابل بارگذاری است.
  useQuestionnaire: (id: string | null) => ({
    data: id === ARCHIVED_QUESTIONNAIRE.id ? ARCHIVED_QUESTIONNAIRE : undefined
  }),
  useCreateSurvey: () => ({ mutate: vi.fn(), isPending: false, error: null }),
  useUpdateSurvey: () => ({ mutate: vi.fn(), isPending: false, error: null })
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

describe('SurveyDialog — questionnaire options', () => {
  it('includes the survey questionnaire even when it is no longer active', async () => {
    render(<SurveyDialog open={true} onClose={() => undefined} surveyId={SURVEY.id} />);

    const select = await waitFor(() => {
      const element = document.getElementById('surveyQuestionnaire');
      if (!element) throw new Error('questionnaire select not rendered');
      return element;
    });

    const optionValues = Array.from(select.querySelectorAll('option')).map((option) => option.value);

    expect(optionValues).toContain(ARCHIVED_QUESTIONNAIRE.id);
    expect(select).toHaveValue(ARCHIVED_QUESTIONNAIRE.id);
  });
});
