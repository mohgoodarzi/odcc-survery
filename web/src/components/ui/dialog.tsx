import { useEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';

import { useLanguage } from '@/i18n/LanguageProvider';
import { cn } from '@/lib/utils';
import { Button } from './button';

interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: ReactNode;
  footer?: ReactNode;
  /** عرض دیالوگ. */
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

/**
 * دیالوگ مدال دست‌ساز با پورتال، فوکوس اولیه و بستن با Escape.
 * بدون وابستگی خارجی (Radix Dialog) تا باندل سبک بماند.
 */
export function Dialog({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  size = 'md',
  className
}: DialogProps) {
  const { t } = useLanguage();
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    document.body.style.overflow = 'hidden';

    // فوکوس اولیه روی اولین عنصر قابل‌تعامل داخل دیالوگ.
    const focusable = panelRef.current?.querySelector<HTMLElement>(
      'input, select, textarea, button, [tabindex]:not([tabindex="-1"])'
    );
    focusable?.focus();

    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = '';
    };
  }, [open, onClose]);

  if (!open) return null;

  const sizeClass = size === 'sm' ? 'max-w-md' : size === 'lg' ? 'max-w-3xl' : 'max-w-xl';

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="dialog-title"
      aria-describedby={description ? 'dialog-description' : undefined}
    >
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-sm"
        onClick={onClose}
        aria-hidden
      />

      <div
        ref={panelRef}
        className={cn(
          'relative z-10 max-h-[90vh] w-full overflow-y-auto rounded-lg border bg-card text-card-foreground shadow-lg',
          sizeClass,
          className
        )}
      >
        <div className="flex items-start justify-between gap-4 border-b p-4">
          <div className="flex flex-col gap-1">
            <h2 id="dialog-title" className="text-lg font-semibold">
              {title}
            </h2>
            {description && (
              <p id="dialog-description" className="text-sm text-muted-foreground">
                {description}
              </p>
            )}
          </div>

          <Button variant="ghost" size="icon" onClick={onClose} aria-label={t.common.close}>
            <X className="size-4" />
          </Button>
        </div>

        <div className="p-4">{children}</div>

        {footer && <div className="flex justify-end gap-2 border-t p-4">{footer}</div>}
      </div>
    </div>,
    document.body
  );
}
