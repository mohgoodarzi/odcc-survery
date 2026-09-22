import { Loader2 } from 'lucide-react';

import { useLanguage } from '@/i18n/LanguageProvider';
import { cn } from '@/lib/utils';

export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn('size-4 animate-spin', className)} aria-hidden />;
}

/**
 * اسپینر تمام‌صفحه برای بارگذاری‌های مسدودکننده (مثل بازیابی نشست).
 */
export function FullPageSpinner() {
  const { t } = useLanguage();

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-background text-foreground">
      <Spinner className="size-8 text-primary" />
      <p className="text-sm text-muted-foreground" role="status">
        {t.common.loading}
      </p>
    </div>
  );
}
