import { useState } from 'react';
import { Download } from 'lucide-react';

import { ReportExecutionStatus, type ReportDefinition } from '@/api/reports';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useDownloadArtifact, useExecutionSearch } from '@/api/reportsHooks';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { formatDateTime } from '@/i18n/format';

interface ReportExecutionsDialogProps {
  open: boolean;
  onClose: () => void;
  report: ReportDefinition;
}

/**
 * تاریخچه‌ی اجراهای یک تعریف گزارش: وضعیت، زمان‌ها، مدت اجرا،
 * فایل خروجی و دانلود آن. ردیف اجرا پیش از رندر ثبت می‌شود،
 * بنابراین حتی اجراهای ناموفق هم در تاریخچه دیده می‌شوند.
 */
export function ReportExecutionsDialog({ open, onClose, report }: ReportExecutionsDialogProps) {
  const { t, culture } = useLanguage();

  const [page, setPage] = useState(1);
  const [downloadError, setDownloadError] = useState<string | undefined>();

  const { data, isLoading, isError, error, refetch } = useExecutionSearch({
    reportDefinitionId: report.id,
    page
  });

  const downloadMutation = useDownloadArtifact();

  function handleDownload(executionId: string, fileName: string) {
    setDownloadError(undefined);

    downloadMutation.mutate(
      { id: executionId, fileName },
      {
        onError: (error) => {
          setDownloadError(
            error instanceof ApiError
              ? t.errors.fromCode(error.code)
              : t.reports.errorArtifactMissing
          );
        }
      }
    );
  }

  const executions = data?.items ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t.reports.executions}
      description={`${report.name} — ${t.reports.executionsDescription}`}
      size="lg"
      footer={
        <Button variant="outline" onClick={onClose}>
          {t.common.close}
        </Button>
      }
    >
      {downloadError && (
        <div
          className="mb-4 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
          role="alert"
        >
          {downloadError}
        </div>
      )}

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={6} />
        ) : executions.length === 0 ? (
          <EmptyState
            title={t.reports.noExecutions}
            description={t.reports.noExecutionsDescription}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.reports.executionStatus}</TableHead>
                <TableHead>{t.reports.queuedAt}</TableHead>
                <TableHead>{t.reports.completedAt}</TableHead>
                <TableHead>{t.reports.duration}</TableHead>
                <TableHead>{t.reports.fileSize}</TableHead>
                <TableHead className="text-end">{t.reports.download}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {executions.map((execution) => (
                <TableRow key={execution.id}>
                  <TableCell>
                    <div className="flex flex-col gap-1">
                      <ExecutionStatusBadge status={execution.status} />
                      {execution.errorMessage && (
                        <span className="text-xs text-destructive" title={execution.errorMessage}>
                          {execution.errorMessage}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {formatDateTime(execution.queuedAt, culture)}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {execution.completedAt ? formatDateTime(execution.completedAt, culture) : '—'}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {formatDuration(execution)}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {execution.fileSizeBytes ? formatFileSize(execution.fileSizeBytes) : '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex justify-end">
                      <Button
                        variant="ghost"
                        size="icon"
                        disabled={!execution.hasArtifact || downloadMutation.isPending}
                        onClick={() => handleDownload(execution.id, execution.fileName ?? execution.id)}
                        aria-label={t.reports.download}
                        title={t.reports.downloadHint}
                      >
                        <Download className="size-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {executions.length > 0 && (
        <div className="mt-4 flex items-center justify-between gap-2 text-sm text-muted-foreground">
          <span>
            {t.common.total}: {data?.totalCount ?? 0} {t.common.results}
          </span>
          <div className="flex items-center gap-2">
            <span>
              {t.common.page} {page} {t.common.of} {totalPages}
            </span>
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => setPage((previous) => previous - 1)}
            >
              {t.common.previous}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= totalPages}
              onClick={() => setPage((previous) => previous + 1)}
            >
              {t.common.next}
            </Button>
          </div>
        </div>
      )}
    </Dialog>
  );
}

function ExecutionStatusBadge({ status }: { status: ReportExecutionStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case ReportExecutionStatus.Pending:
      return <Badge variant="outline">{t.reports.statusPending}</Badge>;
    case ReportExecutionStatus.Running:
      return <Badge variant="warning">{t.reports.statusRunning}</Badge>;
    case ReportExecutionStatus.Succeeded:
      return <Badge variant="success">{t.reports.statusSucceeded}</Badge>;
    default:
      return <Badge variant="destructive">{t.reports.statusFailed}</Badge>;
  }
}

/** مدت زمان اجرا بر اساس اختلاف شروع و تکمیل (در صورت وجود هر دو). */
function formatDuration(
  execution: { startedAt?: string | null; completedAt?: string | null }
): string {
  if (!execution.startedAt || !execution.completedAt) return '—';

  const startedAt = new Date(execution.startedAt);
  const completedAt = new Date(execution.completedAt);
  const milliseconds = completedAt.getTime() - startedAt.getTime();

  if (Number.isNaN(milliseconds) || milliseconds < 0) return '—';
  if (milliseconds < 1000) return `${milliseconds}ms`;

  const seconds = Math.round(milliseconds / 1000);
  return `${seconds}s`;
}

/** اندازه‌ی فایل به شکل خوانا (بایت/کیلوبایت/مگابایت). */
function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes}B`;

  const kilobytes = bytes / 1024;
  if (kilobytes < 1024) return `${kilobytes.toFixed(1)}KB`;

  return `${(kilobytes / 1024).toFixed(1)}MB`;
}
