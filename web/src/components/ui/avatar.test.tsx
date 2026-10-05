import { act, render as tlRender, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { StrictMode } from 'react';

import { Avatar } from '@/components/ui/avatar';

/**
 * پس‌زدن برای این باگ: پس از بارگذاری مجدد صفحه، آواتار ناپدید می‌شد.
 *
 * رویداد واقعی: بارگذاری تازه‌ی صفحه ← React (در حالت StrictMode) افکتِ
 * کامپوننت آواتار را یک‌بار اجرا، سپس پاک‌سازی (unmount) و دوباره اجرا می‌کند.
 * اگر دانلود مشترک در پاک‌سازی لغو شود، مصرف‌کننده‌ای که بلافاصله دوباره نصب
 * می‌شود همان promise لغوشده را می‌گیرد و آواتار هرزا نمایش داده نمی‌شود.
 */

const avatarUrl =
  '/api/identity/users/00000000-0000-0000-0000-0000000000aa/avatar/strict-mode-remount.png';

let fetchCalls = 0;
let abortCalls = 0;
let resolveDownload: ((blob: Blob) => void) | null = null;

vi.mock('@/api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/client')>();
  return {
    ...actual,
    requestResource: vi.fn((_url: string, signal?: AbortSignal) => {
      fetchCalls += 1;
      return new Promise<Blob>((resolve, reject) => {
        resolveDownload = resolve;
        signal?.addEventListener('abort', () => {
          abortCalls += 1;
          reject(new DOMException('The user aborted a request.', 'AbortError'));
        });
      });
    })
  };
});

function renderAvatar() {
  return tlRender(
    <StrictMode>
      <Avatar src={avatarUrl} displayName="مدیر سامانه" size="sm" />
    </StrictMode>
  );
}

afterEach(() => {
  resolveDownload = null;
  fetchCalls = 0;
  abortCalls = 0;
});

describe('Avatar (StrictMode remount on page load)', () => {
  it('keeps the shared download alive and renders the image after remount', async () => {
    const { container } = renderAvatar();

    // یک دانلود مشترک ساخته می‌شود و پاک‌سازیِ StrictMode نباید آن را لغو کند.
    expect(fetchCalls).toBe(1);
    expect(abortCalls).toBe(0);

    // تصویر با موفقیت دریافت می‌شود (در برنامه‌ی واقعی با HTTP 200).
    await act(async () => {
      resolveDownload?.(new Blob([new Uint8Array([1])], { type: 'image/png' }));
    });

    // کامپوننتِ دوباره‌نصب‌شده باید تصویر را نشان دهد، نه حروف اول.
    await waitFor(() => {
      const imgs = container.querySelectorAll('img.rounded-full');
      expect(imgs.length).toBe(1);
    });
  });
});
