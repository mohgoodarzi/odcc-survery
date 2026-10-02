import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { useEffect } from 'react';

import { ThemeProvider, useTheme, type Theme } from './ThemeProvider';
import { render } from '../components/ui/test-utils';

/**
 * مصرف‌کننده‌ی آزمایشی که تم انتخاب‌شده را روی سند اعمال می‌کند.
 * ThemeProvider تم را در یک effect اعمال می‌کند، بنابراین کافی است
 * یک بار رندر شود و سپس مقادیر <html> بررسی شوند.
 */
function ThemeConsumer({ theme }: { theme: Theme }) {
  const { setTheme } = useTheme();

  // اعمال در effect تا هشدار «setState حین رندر» نگیرد.
  useEffect(() => {
    setTheme(theme);
  }, [theme, setTheme]);

  return <div data-testid="consumer">{theme}</div>;
}

describe('ThemeProvider', () => {
  afterEach(() => {
    document.documentElement.classList.remove('dark', 'green');
    document.documentElement.style.colorScheme = '';
    localStorage.removeItem('odcc.survey.theme');
  });

  it('applies no theme class and a light color-scheme for the light theme', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer theme="light" />
      </ThemeProvider>
    );

    const html = document.documentElement;
    expect(html).not.toHaveClass('dark');
    expect(html).not.toHaveClass('green');
    expect(html.style.colorScheme).toBe('light');
  });

  it('applies the .dark class for the dark theme', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer theme="dark" />
      </ThemeProvider>
    );

    const html = document.documentElement;
    expect(html).toHaveClass('dark');
    expect(html).not.toHaveClass('green');
    expect(html.style.colorScheme).toBe('dark');
  });

  it('applies the .green class and a dark color-scheme for the ODCC Green theme', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer theme="green" />
      </ThemeProvider>
    );

    const html = document.documentElement;
    expect(html).toHaveClass('green');
    expect(html).not.toHaveClass('dark');
    // تم سبز یک تم تیره است: کنترل‌های بومی (select، اسکرول‌بار) باید تیره باشند.
    expect(html.style.colorScheme).toBe('dark');
  });

  it('persists the selected theme to localStorage', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer theme="green" />
      </ThemeProvider>
    );

    expect(localStorage.getItem('odcc.survey.theme')).toBe('green');
    expect(screen.getByTestId('consumer')).toHaveTextContent('green');
  });
});
