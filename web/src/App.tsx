import { Navigate, Route, Routes } from 'react-router-dom';

import { useLanguage } from '@/i18n/LanguageProvider';
import { Dashboard } from '@/routes/Dashboard';
import { NotFound } from '@/routes/NotFound';

/**
 * مسیرها با پیشوند فرهنگ هستند: /fa/dashboard , /en/surveys ...
 * این کار باعث می‌شود هر نسخه‌ی زبانی ایندکس و کرال شود.
 */
export function AppRoutes() {
  const { culture } = useLanguage();

  return (
    <Routes>
      <Route path="/" element={<Navigate to={`/${culture}/dashboard`} replace />} />
      <Route path="/:culture" element={<Navigate to={`/${culture}/dashboard`} replace />} />
      <Route path="/:culture/dashboard" element={<Dashboard />} />
      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}
