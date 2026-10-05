import { useRef, useState, type ChangeEvent } from 'react';
import { Camera, Trash2 } from 'lucide-react';

import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { cn } from '@/lib/utils';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';

const MAX_AVATAR_BYTES = 5 * 1024 * 1024;
const ALLOWED_AVATAR_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];

export interface AvatarUploaderProps {
  /** نشانی‌ی تصویر آواتار فعلی یا null اگر تصویری وجود ندارد. */
  avatarUrl: string | null;
  /** نام نمایشی برای آواتار پیش‌فرض. */
  displayName: string;
  disabled?: boolean;
  /** بارگذاری فایل انتخاب‌شده. در صورت شکست باید reject شود تا خطا نمایش داده شود. */
  onUpload: (file: File) => Promise<unknown>;
  /** حذف تصویر فعلی (اختیاری). */
  onRemove?: () => Promise<unknown>;
  className?: string;
}

/**
 * بخش انتخاب تصویر پروفایل: آواتار فعلی، دکمه‌ی تغییر/حذف و اعتبارسنجی
 * نوع و اندازه‌ی فایل قبل از ارسال به سرور.
 *
 * خطاهای سرور از طریق کد خطای ApiError به پیام محلی تبدیل می‌شوند.
 */
export function AvatarUploader({
  avatarUrl,
  displayName,
  disabled,
  onUpload,
  onRemove,
  className
}: AvatarUploaderProps) {
  const { t } = useLanguage();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | undefined>();
  const [isBusy, setIsBusy] = useState(false);

  const isDisabled = disabled || isBusy;

  async function run(action: () => Promise<unknown>) {
    setError(undefined);
    setIsBusy(true);

    try {
      await action();
    } catch (apiError) {
      setError(
        apiError instanceof ApiError ? t.errors.fromCode(apiError.code) : t.errors.generic
      );
    } finally {
      setIsBusy(false);
    }
  }

  function validate(file: File): string | undefined {
    if (!ALLOWED_AVATAR_TYPES.includes(file.type)) {
      return t.profile.avatarInvalidType;
    }

    if (file.size > MAX_AVATAR_BYTES) {
      return t.profile.avatarTooLarge;
    }

    return undefined;
  }

  async function handleChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];

    // پاک کردن مقدار ورودی تا بتوان همان فایل را دوباره انتخاب کرد.
    event.target.value = '';

    if (!file) {
      return;
    }

    const validationError = validate(file);

    if (validationError) {
      setError(validationError);
      return;
    }

    await run(() => onUpload(file));
  }

  async function handleRemove() {
    if (!avatarUrl || !onRemove) {
      return;
    }

    await run(onRemove);
  }

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <div className="flex items-center gap-4">
        <Avatar src={avatarUrl} displayName={displayName} size="lg" />

        <div className="flex flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <label
              className={cn(
                'inline-flex cursor-pointer items-center gap-2 rounded-md border border-input bg-background p-2 text-sm font-medium transition-colors hover:bg-accent',
                isDisabled && 'pointer-events-none opacity-50'
              )}
            >
              <Camera className="size-4" />
              {avatarUrl ? t.profile.changeAvatar : t.profile.avatar}
              <input
                ref={inputRef}
                type="file"
                className="sr-only"
                accept={ALLOWED_AVATAR_TYPES.join(',')}
                onChange={handleChange}
                disabled={isDisabled}
              />
            </label>

            {avatarUrl && onRemove && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleRemove}
                disabled={isDisabled}
              >
                <Trash2 className="size-4" />
                {t.profile.removeAvatar}
              </Button>
            )}

            {isBusy && <Spinner className="size-4" />}
          </div>

          <p className="text-xs text-muted-foreground">{t.profile.avatarHint}</p>
        </div>
      </div>

      {error && (
        <p className="text-sm text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
