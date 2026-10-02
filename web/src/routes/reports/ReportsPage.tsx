import { useState } from 'react';
import { FileBarChart, Plus, Pencil, Search, Play, Archive, Clock, Activity, Table2 } from 'lucide-react';

import { ApiError } from '@/api/client';
import {
  ReportFormat,
  ReportSchedule,
  ReportStatus,
  ReportType,
  type ReportDefinition
} from '@/api/reports';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useActivateReport,
  useArchiveReport,
  useExecuteReport,
  useReportSearch
} from '@/api/reportsHooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { Pagination } from '@/routes/identity/UsersPage';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';
import { ReportDialog } from './ReportDialog';
import { ReportExecutionsDialog } from './ReportExecutionsDialog';
import { ReportResults } from './ReportResults';

/**
 * صفحه‌ی مدیریت گزارش‌ها: فهرست صفحه‌بندی‌شده‌ی تعاریف با فیلتر بر اساس
 * نوع و وضعیت، ایجاد/ویرایش، فعال‌سازی، اجرای فوری، بایگانی و مشاهده‌ی
 * تاریخچه‌ی اجراها و دانلود خروجی.
 */
export function ReportsPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [type, setType] = useState<number | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [executionsFor, setExecutionsFor] = useState<ReportDefinition | null>(null);
  const [activateTarget, setActivateTarget] = useState<string | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<string | null>(null);
  const [executeTarget, setExecuteTarget] = useState<string | null>(null);
  const [resultsFor, setResultsFor] = useState<ReportDefinition | null>(null);

  const { data, isLoading, isError, error, refetch } = useReportSearch({
    searchText,
    type,
    status,
    includeArchived,
    page
  });

  const activateMutation = useActivateReport();
  const archiveMutation = useArchiveReport();
  const executeMutation = useExecuteReport();

  const canManage = hasPermission(Permissions.Reports.Export);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleTypeChange(value: string) {
    setType(value === 'all' ? null : Number(value));
    setPage(1);
  }

  function handleStatusChange(value: string) {
    setStatus(value === 'all' ? null : Number(value));
    setPage(1);
  }

  const reports = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.reports.title}
        description={t.reports.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.reports.newReport}
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute start-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={searchText ?? ''}
            onChange={(event) => handleSearch(event.target.value)}
            placeholder={t.reports.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={type === null ? 'all' : String(type)}
          onChange={(event) => handleTypeChange(event.target.value)}
          options={typeOptions(t)}
          aria-label={t.reports.reportType}
        />

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => handleStatusChange(event.target.value)}
          options={statusOptions(t)}
          aria-label={t.reports.executionStatus}
        />

        <label className="flex cursor-pointer items-center gap-2 rounded-md border p-2.5 text-sm">
          <input
            type="checkbox"
            checked={includeArchived}
            onChange={(event) => {
              setIncludeArchived(event.target.checked);
              setPage(1);
            }}
            className="size-4 accent-primary"
          />
          {t.reports.includeArchived}
        </label>
      </div>

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={7} />
        ) : reports.length === 0 ? (
          <EmptyState
            title={t.reports.noReports}
            description={t.reports.noReportsDescription}
            icon={<FileBarChart className="size-10" />}
            action={
              canManage
                ? { label: t.reports.newReport, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.reports.reportName}</TableHead>
                <TableHead>{t.reports.reportType}</TableHead>
                <TableHead>{t.reports.reportFormat}</TableHead>
                <TableHead>{t.reports.reportSchedule}</TableHead>
                <TableHead>{t.reports.executionStatus}</TableHead>
                <TableHead>{t.reports.lastExecutedAt}</TableHead>
                <TableHead>{t.reports.nextRunAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {reports.map((report) => (
                <TableRow key={report.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium">{report.name}</span>
                      {report.surveyTitle && (
                        <span className="text-xs text-muted-foreground" dir="ltr">
                          {report.surveyTitle} ({report.surveyCode})
                        </span>
                      )}
                      {report.orgUnitPath && (
                        <span className="text-xs text-muted-foreground">{report.orgUnitPath}</span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {typeLabel(t.reports, report.type)}
                  </TableCell>
                  <TableCell dir="ltr" className="text-muted-foreground">
                    {report.format === ReportFormat.Pdf ? t.reports.formatPdf : t.reports.formatExcel}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {scheduleLabel(t.reports, report.schedule)}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={report.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {report.lastExecutedAt ? formatDateTime(report.lastExecutedAt, culture) : '—'}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {report.nextRunAt ? formatDateTime(report.nextRunAt, culture) : '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => setExecutionsFor(report)}
                        aria-label={t.reports.executions}
                        title={t.reports.executions}
                      >
                        <Clock className="size-4" />
                      </Button>

                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => setResultsFor(report)}
                        aria-label={t.reports.showResults}
                        title={t.reports.showResults}
                      >
                        <Table2 className="size-4" />
                      </Button>

                      {canManage && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            disabled={executeMutation.isPending || report.status === ReportStatus.Archived}
                            onClick={() => setExecuteTarget(report.id)}
                            aria-label={t.reports.execute}
                            title={t.reports.executeHint}
                          >
                            <Play className="size-4" />
                          </Button>

                          {report.status !== ReportStatus.Archived && (
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setEditingId(report.id)}
                              aria-label={t.common.edit}
                            >
                              <Pencil className="size-4" />
                            </Button>
                          )}

                          {report.status === ReportStatus.Draft && (
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setActivateTarget(report.id)}
                              aria-label={t.reports.activate}
                              title={t.reports.activateImmediatelyHint}
                            >
                              <Activity className="size-4" />
                            </Button>
                          )}

                          {report.status !== ReportStatus.Archived && (
                            <Button
                              variant="ghost"
                              size="icon"
                              className="text-destructive"
                              onClick={() => setArchiveTarget(report.id)}
                              aria-label={t.reports.archive}
                            >
                              <Archive className="size-4" />
                            </Button>
                          )}
                        </>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {reports.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      <p className="mt-4 flex items-center gap-2 text-xs text-muted-foreground">
        <Activity className="size-4" />
        {t.reports.privacyNote}
      </p>

      {resultsFor && (
        <ReportResults report={resultsFor} onClose={() => setResultsFor(null)} />
      )}

      {createOpen && <ReportDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingId && (
        <ReportDialog open={!!editingId} onClose={() => setEditingId(null)} reportId={editingId} />
      )}

      {executionsFor && (
        <ReportExecutionsDialog
          open={!!executionsFor}
          onClose={() => setExecutionsFor(null)}
          report={executionsFor}
        />
      )}

      <ConfirmDialog
        open={!!activateTarget}
        onClose={() => setActivateTarget(null)}
        title={t.reports.activate}
        description={t.reports.activateImmediatelyHint}
        confirmLabel={t.reports.activate}
        destructive={false}
        isPending={activateMutation.isPending}
        error={
          activateMutation.error instanceof ApiError
            ? t.errors.fromCode(activateMutation.error.code)
            : activateMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!activateTarget) return;
          activateMutation.mutate(activateTarget, { onSuccess: () => setActivateTarget(null) });
        }}
      />

      <ConfirmDialog
        open={!!archiveTarget}
        onClose={() => setArchiveTarget(null)}
        title={t.reports.archiveConfirmTitle}
        description={t.reports.archiveConfirmDescription}
        confirmLabel={t.reports.archive}
        isPending={archiveMutation.isPending}
        error={
          archiveMutation.error instanceof ApiError
            ? t.errors.fromCode(archiveMutation.error.code)
            : archiveMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!archiveTarget) return;
          archiveMutation.mutate(archiveTarget, { onSuccess: () => setArchiveTarget(null) });
        }}
      />

      <ConfirmDialog
        open={!!executeTarget}
        onClose={() => setExecuteTarget(null)}
        title={t.reports.executeConfirmTitle}
        description={t.reports.executeConfirmDescription}
        confirmLabel={executeMutation.isPending ? t.reports.executing : t.reports.execute}
        destructive={false}
        isPending={executeMutation.isPending}
        error={
          executeMutation.error instanceof ApiError
            ? t.errors.fromCode(executeMutation.error.code)
            : executeMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!executeTarget) return;
          executeMutation.mutate(executeTarget, {
            onSuccess: (execution) => {
              setExecuteTarget(null);
              // پس از اجرای موفق، نتایج همان گزارش زیر فهرست نمایش داده می‌شود.
              const executed = reports.find((report) => report.id === execution.reportDefinitionId);
              if (executed) {
                setResultsFor(executed);
              }
            }
          });
        }}
      />
    </AppLayout>
  );
}

function typeOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(ReportType.SurveyAnalytics), label: t.reports.typeSurveyAnalytics },
    { value: String(ReportType.DashboardSummary), label: t.reports.typeDashboardSummary },
    { value: String(ReportType.BenchmarkComparison), label: t.reports.typeBenchmarkComparison }
  ];
}

function statusOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(ReportStatus.Draft), label: t.reports.statusDraft },
    { value: String(ReportStatus.Active), label: t.reports.statusActive },
    { value: String(ReportStatus.Archived), label: t.reports.statusArchived }
  ];
}

function typeLabel(t: Dictionary['reports'], type: ReportType): string {
  switch (type) {
    case ReportType.DashboardSummary: return t.typeDashboardSummary;
    case ReportType.BenchmarkComparison: return t.typeBenchmarkComparison;
    default: return t.typeSurveyAnalytics;
  }
}

function scheduleLabel(t: Dictionary['reports'], schedule: ReportSchedule): string {
  switch (schedule) {
    case ReportSchedule.Daily: return t.scheduleDaily;
    case ReportSchedule.Weekly: return t.scheduleWeekly;
    case ReportSchedule.Monthly: return t.scheduleMonthly;
    default: return t.scheduleOneTime;
  }
}

function StatusBadge({ status }: { status: ReportStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case ReportStatus.Draft:
      return <Badge variant="outline">{t.reports.statusDraft}</Badge>;
    case ReportStatus.Active:
      return <Badge variant="success">{t.reports.statusActive}</Badge>;
    default:
      return <Badge variant="outline">{t.reports.statusArchived}</Badge>;
  }
}
