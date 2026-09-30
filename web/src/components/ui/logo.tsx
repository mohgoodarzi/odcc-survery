import { useLanguage } from '@/i18n/LanguageProvider';
import { cn } from '@/lib/utils';

/**
 * لوگوی سازمانی ODCC.
 *
 * از نسخه‌ی برداری (SVG) استفاده می‌شود تا در هر اندازه‌ای بدون افت کیفیت
 * نمایش داده شود. نسبت ابعاد اصلی لوگو با `object-contain` همواره حفظ می‌شود
 * و اندازه فقط با کلاس‌های Tailwind (مثلاً `size-9` یا `h-10`) کنترل می‌شود،
 * بنابراین هیچ‌گونه کشیدگی یا تغییر شکل غیرضروری رخ نمی‌دهد.
 */
export function Logo({ className, alt }: { className?: string; alt?: string }) {
  const { t } = useLanguage();

  return (
    <img
      src={`${import.meta.env.BASE_URL}odcc.svg`}
      alt={alt ?? t.app.name}
      className={cn('h-8 w-auto shrink-0 object-contain', className)}
    />
  );
}
