import { Link } from 'react-router-dom';
import { ShieldAlert } from 'lucide-react';

import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { Button } from '@/components/ui/button';

/**
 * صفحه‌ی «دسترسی غیرمجاز»: کاربر احراز هویت کرده اما مجوز لازم را ندارد.
 */
export function Forbidden() {
  const { t, culture } = useLanguage();
  const { user } = useAuth();

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-6 text-center">
      <ShieldAlert className="size-12 text-amber-500" />
      <h1 className="text-2xl font-bold">{t.errors.forbidden}</h1>
      <p className="max-w-md text-sm text-muted-foreground">{t.errors.forbiddenDescription}</p>

      {user && (
        <p className="text-xs text-muted-foreground" dir="ltr">
          {user.userName}
        </p>
      )}

      <Button asChild>
        <Link to={`/${culture}/dashboard`}>{t.common.goHome}</Link>
      </Button>
    </div>
  );
}
