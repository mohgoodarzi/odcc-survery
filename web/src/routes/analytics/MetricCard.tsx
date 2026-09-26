import { useLanguage } from '@/i18n/LanguageProvider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { formatCount, formatNumber } from '@/i18n/format';

interface MetricCardProps {
  label: string;
  value: number | null | undefined;
  /** مقدار به‌صورت درصد نمایش داده می‌شود. */
  asPercentage?: boolean;
  /** توضیح کوتاه زیر مقدار. */
  hint?: string;
  /** در صورت null بودن مقدار، این متن نمایش داده می‌شود. */
  emptyLabel?: string;
}

/**
 * کارت یک شاخص کلیدی برای داشبورد تحلیلات.
 * مقادیر null به‌معنای «داده‌ای وجود ندارد» هستند (مثلاً سؤال NPS نبودن).
 */
export function MetricCard({ label, value, asPercentage, hint, emptyLabel }: MetricCardProps) {
  const { culture, t } = useLanguage();

  const hasValue = value !== null && value !== undefined && !Number.isNaN(value);

  return (
    <Card>
      <CardHeader>
        <CardDescription>{label}</CardDescription>
        <CardTitle className="text-3xl" dir="ltr">
          {hasValue
            ? asPercentage
              ? `${formatNumber(value as number, culture)}٪`
              : formatCount(value as number, culture)
            : (emptyLabel ?? t.analytics.noData)}
        </CardTitle>
      </CardHeader>
      {hint && (
        <CardContent>
          <p className="text-xs text-muted-foreground">{hint}</p>
        </CardContent>
      )}
    </Card>
  );
}
