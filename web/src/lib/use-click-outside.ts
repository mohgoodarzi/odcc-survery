import { useEffect, useRef, type RefObject } from 'react';

/**
 * بستن یک المان با کلیک بیرون از آن یا فشردن Escape.
 * برای منوهای بازشونده‌ی سبک که دیالوگ نیستند (دیالوگ‌ها مدیریت فوکوس
 * خودشان را دارند).
 *
 * مدیریت یادآور در یک ref نگه‌داری می‌شود تا نیازی به پایدار بودن
 * تابع فراخوانی در هر رندر نباشد.
 */
export function useClickOutside(
  ref: RefObject<HTMLElement | null>,
  onOutside: () => void,
  enabled = true
): void {
  const handlerRef = useRef(onOutside);
  handlerRef.current = onOutside;

  useEffect(() => {
    if (!enabled) return;

    function handlePointerDown(event: MouseEvent) {
      const node = ref.current;
      if (node && !node.contains(event.target as Node)) {
        handlerRef.current();
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        handlerRef.current();
      }
    }

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [ref, enabled]);
}
