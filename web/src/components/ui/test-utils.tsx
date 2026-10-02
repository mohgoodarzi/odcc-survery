import type { ReactElement, ReactNode } from 'react';
import { render as renderScreen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

import { LanguageProvider } from '@/i18n/LanguageProvider';

/**
 * رندر آزمایشی با پروایدرهای ضروری: زبان (فارسی/RTL) و مسیریابی.
 * تقویم جلالی برای حالت فارسی ساخته شده و این همان حالتی است که در
 * تولید استفاده می‌شود.
 */
export function render(node: ReactElement) {
  document.documentElement.setAttribute('lang', 'fa');
  document.documentElement.setAttribute('dir', 'rtl');

  return renderScreen(node, {
    wrapper: ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={['/fa']}>
        <LanguageProvider>{children}</LanguageProvider>
      </MemoryRouter>
    )
  });
}
