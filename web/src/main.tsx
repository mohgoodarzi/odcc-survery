import { StrictMode, useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LanguageProvider, useLanguage } from '@/i18n/LanguageProvider';
import { AuthProvider } from '@/auth/AuthProvider';
import { AppRoutes } from '@/App';
import '@/styles/globals.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30_000
    }
  }
});

/**
 * همگام‌سازی جهت، زبان و عنوان سند با فرهنگ فعلی. index.html این مقادیر را قبل از
 * اولین رنگ‌پردازی تنظیم می‌کند؛ این افکت تغییرات زمان اجرا را اعمال می‌کند.
 */
function DocumentDirectionSync() {
  const { culture, direction, locale, t } = useLanguage();

  useEffect(() => {
    const html = document.documentElement;
    html.lang = locale;
    html.dir = direction;
    document.body.setAttribute('dir', direction);
    document.title = t.meta.title;
  }, [culture, direction, locale, t.meta.title]);

  return null;
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <LanguageProvider>
        <DocumentDirectionSync />
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <AppRoutes />
          </AuthProvider>
        </QueryClientProvider>
      </LanguageProvider>
    </BrowserRouter>
  </StrictMode>
);
