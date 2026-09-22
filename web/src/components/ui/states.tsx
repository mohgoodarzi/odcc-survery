import type { ReactNode } from 'react';

import { useLanguage } from '@/i18n/LanguageProvider';
import { cn } from '@/lib/utils';
import { Button } from './button';

interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: ReactNode;
  action?: {
    label: string;
    onClick: () => void;
  };
  className?: string;
}

/**
 * حالت خالی: زمانی که لیستی بدون داده است. آیکن و توضیح و اقدام اختیاری.
 */
export function EmptyState({ title, description, icon, action, className }: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed p-10 text-center',
        className
      )}
    >
      {icon && <div className="text-muted-foreground">{icon}</div>}
      <div className="flex flex-col gap-1">
        <h3 className="text-base font-medium">{title}</h3>
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      {action && (
        <Button variant="outline" size="sm" onClick={action.onClick}>
          {action.label}
        </Button>
      )}
    </div>
  );
}

interface ErrorStateProps {
  message?: string;
  onRetry?: () => void;
  className?: string;
}

/**
 * حالت خطا: پیام و دکمه‌ی تلاش مجدد.
 */
export function ErrorState({ message, onRetry, className }: ErrorStateProps) {
  const { t } = useLanguage();

  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-3 rounded-lg border border-destructive/30 bg-destructive/5 p-10 text-center',
        className
      )}
      role="alert"
    >
      <div className="flex flex-col gap-1">
        <h3 className="text-base font-medium text-destructive">{t.common.error}</h3>
        <p className="text-sm text-muted-foreground">{message ?? t.errors.generic}</p>
      </div>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          {t.common.retry}
        </Button>
      )}
    </div>
  );
}

/**
 * ردیف اسکلتون برای زمان بارگذاری جدول‌ها.
 */
export function TableLoading({ columns = 4 }: { columns?: number }) {
  const { t } = useLanguage();

  return (
    <div className="flex flex-col gap-2 p-4" role="status" aria-live="polite">
      <span className="text-sm text-muted-foreground">{t.common.loading}</span>
      {Array.from({ length: 4 }).map((_, rowIndex) => (
        <div key={rowIndex} className="flex gap-4">
          {Array.from({ length: columns }).map((_, colIndex) => (
            <div
              key={colIndex}
              className="h-8 flex-1 animate-pulse rounded bg-muted"
              aria-hidden
            />
          ))}
        </div>
      ))}
    </div>
  );
}
