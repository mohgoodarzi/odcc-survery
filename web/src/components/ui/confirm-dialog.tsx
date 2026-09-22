import { Dialog } from './dialog';
import { Button } from './button';
import { Spinner } from './spinner';
import { useLanguage } from '@/i18n/LanguageProvider';

interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description: string;
  confirmLabel: string;
  onConfirm: () => void;
  isPending?: boolean;
  error?: string;
  /** در صورت true، دکمه‌ی تأیید حالت مخرب می‌گیرد. */
  destructive?: boolean;
}

/**
 * دیالوگ تأیید عملیات مخرب (مثلاً حذف). از انجام اشتباه جلوگیری می‌کند.
 */
export function ConfirmDialog({
  open,
  onClose,
  title,
  description,
  confirmLabel,
  onConfirm,
  isPending,
  error,
  destructive = true
}: ConfirmDialogProps) {
  const { t } = useLanguage();

  return (
    <Dialog open={open} onClose={onClose} title={title} description={description} size="sm">
      {error && (
        <div
          className="mb-4 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
          role="alert"
        >
          {error}
        </div>
      )}

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={onClose} disabled={isPending}>
          {t.common.cancel}
        </Button>
        <Button
          variant={destructive ? 'destructive' : 'default'}
          onClick={onConfirm}
          disabled={isPending}
        >
          {isPending && <Spinner className="size-4" />}
          {confirmLabel}
        </Button>
      </div>
    </Dialog>
  );
}
