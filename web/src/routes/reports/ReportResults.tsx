import { Table2, X } from 'lucide-react';

import { ApiError } from '@/api/client';
import { ReportColumnType, type ReportDefinition } from '@/api/reports';
import { useReportData } from '@/api/reportsHooks';
import { useLanguage } from '@/i18n/LanguageProvider';
import { formatDateTime } from '@/i18n/format';
import { Button } from '@/components/ui/button';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';

interface ReportResultsProps {
  /** تعریف گزارشی که نتایج آن نمایش داده می‌شود. */
  report: ReportDefinition;
  /** بستن پنل نتایج. */
  onClose: () => void;
}

/**
 * نمایش نتایج یک گزارش روی صفحه به‌صورت بخش‌های جدولی.
 *
 * داده از همان مسیری می‌آید که فایل خروجی از آن رندر می‌شود
 * (<c>GET /reports/{id}/data</c>)، بنابراین جدول با فایل قابل‌دانلود
 * یکسان است. حالت‌های بارگذاری، خالی و خطا به‌طور کامل پوشش داده شده‌اند.
 *
 * **حریم خصوصی:** داده فقط شامل تجمع‌های تحلیلی است؛ هیچ شناسه‌ی
 * پاسخ‌گویی در آن نیست.
 */
export function ReportResults({ report, onClose }: ReportResultsProps) {
  const { t, culture } = useLanguage();

  const { data, isLoading, isError, error, refetch } = useReportData(report.id);

  const totalRows = data?.sections.reduce((sum, section) => sum + section.rows.length, 0) ?? 0;

  return (
    <section
      className="mt-6 rounded-lg border bg-card"
      aria-labelledby="report-results-title"
    >
      <header className="flex flex-wrap items-start justify-between gap-3 border-b p-4">
        <div className="flex flex-col gap-1">
          <h2 id="report-results-title" className="flex items-center gap-2 text-base font-medium">
            <Table2 className="size-4 text-primary" />
            {t.reports.resultsFor}: {report.name}
          </h2>
          <p className="text-xs text-muted-foreground">{t.reports.resultsDescription}</p>
        </div>

        <Button variant="ghost" size="icon" onClick={onClose} aria-label={t.reports.hideResults}>
          <X className="size-4" />
        </Button>
      </header>

      {data && (
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b bg-muted/30 px-4 py-2 text-xs text-muted-foreground">
          <span dir="ltr">{t.reports.generatedAt}: {formatDateTime(data.generatedAt, culture)}</span>
          <span>{t.reports.generatedBy}: {data.generatedBy}</span>
          <span dir="ltr">{totalRows} {t.reports.rows}</span>
          {data.subtitle && <span className="opacity-80">{data.subtitle}</span>}
        </div>
      )}

      <div className="p-4">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={4} />
        ) : !data || data.sections.length === 0 ? (
          <EmptyState
            title={t.reports.resultsEmpty}
            description={t.reports.resultsEmptyDescription}
            icon={<Table2 className="size-10" />}
          />
        ) : (
          <div className="flex flex-col gap-6">
            {data.sections.map((section, sectionIndex) => (
              <section key={`${section.title}-${sectionIndex}`} className="flex flex-col gap-2">
                <h3 className="text-sm font-medium text-primary">{section.title}</h3>

                <div className="rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        {section.columns.map((column, columnIndex) => (
                          <TableHead key={`${column.title}-${columnIndex}`}>
                            {column.title}
                          </TableHead>
                        ))}
                      </TableRow>
                    </TableHeader>

                    <TableBody>
                      {section.rows.length === 0 ? (
                        <TableRow>
                          <TableCell
                            colSpan={section.columns.length}
                            className="text-center text-muted-foreground"
                          >
                            {t.common.noData}
                          </TableCell>
                        </TableRow>
                      ) : (
                        section.rows.map((row, rowIndex) => (
                          <TableRow key={rowIndex}>
                            {row.map((value, columnIndex) => (
                              <TableCell
                                key={columnIndex}
                                dir={isNumericColumn(section.columns[columnIndex]) ? 'ltr' : undefined}
                                className={
                                  isNumericColumn(section.columns[columnIndex])
                                    ? 'text-end font-mono text-xs'
                                    : undefined
                                }
                              >
                                {formatCellValue(value, section.columns[columnIndex])}
                              </TableCell>
                            ))}
                          </TableRow>
                        ))
                      )}
                    </TableBody>
                  </Table>
                </div>

                {section.footnote && (
                  <p className="text-xs text-muted-foreground">{section.footnote}</p>
                )}
              </section>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

/** آیا این ستون عددی/درصدی است و باید چپ‌چین و با فونت عددی نمایش داده شود؟ */
function isNumericColumn(column?: { columnType: ReportColumnType }): boolean {
  return column?.columnType === ReportColumnType.Number
    || column?.columnType === ReportColumnType.Percent;
}

/**
 * قالب‌بندی مقدار یک سلول بر اساس نوع ستون — همان منطقی که رندر PDF/Excel
 * استفاده می‌کند، تا جدول روی صفحه با فایل خروجی یکسان باشد.
 */
function formatCellValue(
  value: string | number | null,
  column?: { columnType: ReportColumnType }
): string {
  if (value === null || value === undefined) return '—';

  if (column?.columnType === ReportColumnType.Percent && typeof value === 'number') {
    return `${formatNumber(value)}٪`;
  }

  if (typeof value === 'number') {
    return formatNumber(value);
  }

  return String(value);
}

/** اعداد با حداکثر دو رقم اعشار و جداکننده‌ی هزارگان (محلی). */
function formatNumber(value: number): string {
  const rounded = Math.round(value * 100) / 100;
  return rounded.toLocaleString(undefined, { maximumFractionDigits: 2 });
}
