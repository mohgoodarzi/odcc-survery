import { Navigate, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';

import { useAuth } from './AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { FullPageSpinner } from '@/components/ui/spinner';

interface RequireAuthProps {
  children: ReactNode;
  /** مجوزهای لازم برای دیدن این مسیر (حداقل یکی کافی است). */
  permissions?: string[];
}

/**
 * مسیر محافظت‌شده: کاربر باید احراز هویت شده باشد و (در صورت تعیین)
   حداقل یکی از مجوزهای لازم را داشته باشد.
 *
 * مسیر فعلی به‌عنوان redirect ذخیره می‌شود تا پس از ورود به آن بازگردد.
 * اگر کاربر مجوز لازم را نداشته باشد، به صفحه‌ی «دسترسی غیرمجاز» هدایت می‌شود.
 */
export function RequireAuth({ children, permissions }: RequireAuthProps) {
  const { isAuthenticated, isLoading, hasAnyPermission } = useAuth();
  const { culture } = useLanguage();
  const location = useLocation();

  if (isLoading) {
    return <FullPageSpinner />;
  }

  if (!isAuthenticated) {
    const redirect = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/${culture}/login?redirect=${redirect}`} replace state={{ from: location }} />;
  }

  if (permissions && permissions.length > 0 && !hasAnyPermission(...permissions)) {
    return <Navigate to={`/${culture}/forbidden`} replace />;
  }

  return <>{children}</>;
}
