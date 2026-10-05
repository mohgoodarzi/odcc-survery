import { useEffect, useState } from 'react';

import { requestResource } from '@/api/client';
import { cn } from '@/lib/utils';

const sizeClasses = {
  sm: 'size-8 text-xs',
  md: 'size-10 text-sm',
  lg: 'size-20 text-lg'
} as const;

export interface AvatarProps {
  /** نشانی‌ی تصویر آواتار یا null وقتی تصویری وجود ندارد. */
  src?: string | null;
  /** نام نمایشی کاربر؛ برای تولید حروف اولِ آواتار پیش‌فرض استفاده می‌شود. */
  displayName: string;
  size?: keyof typeof sizeClasses;
  className?: string;
}

/**
 * تبدیل یک blob به data URL. از <c>FileReader</c> استفاده می‌شود چون
 * <c>blob.text()</c> برای محتوای باینری مناسب نیست.
 */
function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();

    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error ?? new Error('خواندن تصویر آواتار ناموفق بود.'));

    reader.readAsDataURL(blob);
  });
}

/**
 * کشِ data URLهای تصاویر آواتار.
 *
 * تصویر آواتار فقط برای کاربر احراز هویت‌شده سرو می‌شود و مرورگر نمی‌تواند
 * توکن را همراه درخواستِ <c>&lt;img src&gt;</c> بفرستد؛ پس تصویر با کلاینت
 * API دانلود می‌شود و به یک data URL تبدیل می‌گردد (data در سیاست CSP مجاز
 * است، برخلاف blob).
 *
 * کش به ازای هر نشانی آواتار پر می‌شود تا با رفت‌وآمد بین صفحه‌ها تصویر
 * دوباره دانلود نشود. اندازه‌ی کش محدود است تا حافظه نشت نکند.
 */
const MAX_CACHED_AVATARS = 64;

const avatarDataUrls = new Map<string, string>();
const avatarRequests = new Map<string, Promise<string>>();

/** حذف قدیمی‌ترین ورودی‌های کش وقتی از سقف عبور می‌کند. */
function evictOldAvatars(): void {
  while (avatarDataUrls.size > MAX_CACHED_AVATARS) {
    const oldestUrl = avatarDataUrls.keys().next().value;
    if (oldestUrl === undefined) {
      return;
    }
    avatarDataUrls.delete(oldestUrl);
  }
}

/**
 * دانلود تصویر آواتار و تبدیل آن به data URL. درخواست‌های همزمان برای یک
 * نشانی به یک promise مشترک متصل می‌شوند تا چند کامپوننت یک دان کنند.
 *
 * این درخواستِ مشترک به سیگنال لغوِ هیچ مصرف‌کننده‌ای متصل نیست. دلیل:
 * در بارگذاری اولیه‌ی صفحه، React (در حالت StrictMode) افکت را یک‌بار اجرا،
 * سپس پاک‌سازی (unmount) و دوباره اجرا می‌کند. اگر پاک‌سازیِ هر مصرف‌کننده
 * بتواند دانلودِ مشترک را لغو کند، مصرف‌کننده‌ای که بلافاصله بعد از آن
 * دوباره نصب می‌شود همان promiseِ لغوشده را برمی‌دارد و خطای لغو را به‌جای
 * «تصویر موجود نیست» تفسیر می‌کند. لغو فقط از طریق پرچم <c>cancelled</c>
 * در افکت مدیریت می‌شود تا وضعیتِ کامپوننتِ unmount‌شده به‌روز نشود.
 */
function loadAvatarDataUrl(avatarUrl: string): Promise<string> {
  const cached = avatarDataUrls.get(avatarUrl);
  if (cached) {
    return Promise.resolve(cached);
  }

  const existing = avatarRequests.get(avatarUrl);
  if (existing) {
    return existing;
  }

  const request = (async () => {
    const blob = await requestResource(avatarUrl);
    const dataUrl = await blobToDataUrl(blob);

    avatarRequests.delete(avatarUrl);
    avatarDataUrls.set(avatarUrl, dataUrl);
    evictOldAvatars();

    return dataUrl;
  })().catch((error) => {
    avatarRequests.delete(avatarUrl);
    throw error;
  });

  avatarRequests.set(avatarUrl, request);

  return request;
}

/**
 * تصویر آواتار کاربر با یک آواتار پیش‌فرض بر اساس حروف اول نام.
 *
 * - تصاویر در سمت سرور از انبار فایل‌ها سرو می‌شوند و نیازمند احراز هویت
 *   هستند؛ به همین دلیل مستقیماً در <c>src</c> قرار نمی‌گیرند و با توکن
 *   دانلود شده و به data URL تبدیل می‌شوند.
 * - اگر تصویر بارگذاری نشود یا خراب باشد، آواتار پیش‌فرض نشان داده می‌شود.
 * - رنگ‌ها از توکن‌های معنایی تم (روشن/تاریک/سبز) استفاده می‌کنند.
 */
export function Avatar({ src, displayName, size = 'md', className }: AvatarProps) {
  const [imageFailed, setImageFailed] = useState(false);
  const [resolvedSrc, setResolvedSrc] = useState<string | null>(() =>
    src ? (avatarDataUrls.get(src) ?? null) : null
  );

  // تصویر فقط با نشانیِ سرور دانلود می‌شود؛ data URLِ آن در کش می‌ماند.
  useEffect(() => {
    if (!src) {
      setResolvedSrc(null);
      setImageFailed(false);
      return;
    }

    const cached = avatarDataUrls.get(src);

    if (cached) {
      setResolvedSrc(cached);
      setImageFailed(false);
      return;
    }

    // دانلود مشترک لغو نمی‌شود (توضیح در loadAvatarDataUrl)؛ این پرچم فقط
    // مانع به‌روزرسانیِ وضعیت پس از unmount می‌شود.
    let cancelled = false;

    setImageFailed(false);

    loadAvatarDataUrl(src)
      .then((dataUrl) => {
        if (!cancelled) {
          setResolvedSrc(dataUrl);
        }
      })
      .catch(() => {
        if (!cancelled) {
          // تصویر در دسترس نیست (مثلاً ۴۰۴)؛ آواتار پیش‌فرض نشان داده می‌شود.
          setResolvedSrc(null);
          setImageFailed(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [src]);

  const showImage = !!resolvedSrc && !imageFailed;

  if (showImage) {
    return (
      <img
        key={resolvedSrc}
        src={resolvedSrc}
        alt=""
        className={cn(
          // object-cover + برش از مرکز: تصویر بدون اعوجاج، کادرِ مربع و دایره‌ای می‌چسبد.
          'aspect-square shrink-0 rounded-full object-cover object-center ring-1 ring-border bg-muted',
          sizeClasses[size],
          className
        )}
        // آواتار نوار بالا بدون تأخیر بارگذاری می‌شود؛ آواتارهای بزرگ‌تر lazy می‌مانند.
        loading={size === 'sm' ? 'eager' : 'lazy'}
        decoding="async"
        onError={() => setImageFailed(true)}
      />
    );
  }

  return (
    <div
      className={cn(
        'flex aspect-square shrink-0 items-center justify-center rounded-full bg-primary/10 font-medium text-primary ring-1 ring-border overflow-hidden',
        sizeClasses[size],
        className
      )}
      aria-hidden
    >
      {getInitials(displayName)}
    </div>
  );
}

/** حروف اول نام برای آواتار پیش‌فرض. */
export function getInitials(displayName: string): string {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);

  if (parts.length === 0) {
    return '؟';
  }

  if (parts.length === 1) {
    return parts[0].slice(0, 2);
  }

  return (parts[0][0] ?? '') + (parts[1][0] ?? '');
}
