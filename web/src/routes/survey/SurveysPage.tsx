import { useState } from 'react';
import { ClipboardList, Plus, Pencil, Trash2, Search, MoreVertical } from 'lucide-react';

import { ApiError } from '@/api/client';
import { SurveyStatus } from '@/api/surveys';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  queryKeys,
  useDeleteSurvey,
  useSurveysSearch,
  useSurveyLifecycle,
  type SurveyAction
} from '@/api/hooks';
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
import { SurveyDialog } from './SurveyDialog';
import { formatDate } from '@/i18n/format';
import { useQueryClient } from '@tanstack/react-query';
import type { Dictionary } from '@/i18n/types';

/**
 * صفحه‌ی مدیریت نظرسنجی‌ها: فهرست صفحه‌بندی‌شده، فیلتر بر اساس وضعیت و
 * مدیریت چرخه‌ی عمر (انتشار، شروع، توقف، از سرگیری، بستن، بایگانی).
 */
export function SurveysPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [lifecycleTarget, setLifecycleTarget] = useState<{ id: string; action: SurveyAction } | null>(null);

  const {
    data,
    isLoading,
    isError,
    error,
    refetch
  } = useSurveysSearch({
    searchText,
    status,
    questionnaireId: null,
    includeArchived,
    page
  });

  const deleteMutation = useDeleteSurvey();
  const lifecycleMutation = useSurveyLifecycle();
  const queryClient = useQueryClient();

  const canCreate = hasPermission(Permissions.Survey.Create);
  const canEdit = hasPermission(Permissions.Survey.Edit);
  const canPublish = hasPermission(Permissions.Survey.Publish);
  const canDelete = hasPermission(Permissions.Survey.Delete);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleStatusChange(value: string) {
    setStatus(value === 'all' ? null : Number(value));
    setPage(1);
  }

  async function runLifecycle(id: string, action: SurveyAction) {
    lifecycleMutation.mutate(
      { id, action },
      {
        onSuccess: () => {
          void queryClient.invalidateQueries({ queryKey: queryKeys.surveys });
          setLifecycleTarget(null);
        }
      }
    );
  }

  const surveys = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.surveys.title}
        description={t.surveys.description}
        actions={
          canCreate ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.surveys.newSurvey}
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
            placeholder={t.surveys.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => handleStatusChange(event.target.value)}
          options={statusOptions(t.surveys)}
          aria-label={t.surveys.status}
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
          {t.surveys.includeArchived}
        </label>
      </div>

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={6} />
        ) : surveys.length === 0 ? (
          <EmptyState
            title={t.surveys.noSurveys}
            description={t.surveys.noSurveysDescription}
            icon={<ClipboardList className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.surveys.code}</TableHead>
                <TableHead>{t.surveys.title_}</TableHead>
                <TableHead>{t.surveys.questionnaire}</TableHead>
                <TableHead>{t.surveys.status}</TableHead>
                <TableHead>{t.surveys.startDate}</TableHead>
                <TableHead>{t.surveys.endDate}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {surveys.map((survey) => (
                <TableRow key={survey.id}>
                  <TableCell dir="ltr" className="font-medium">
                    {survey.code}
                  </TableCell>
                  <TableCell>{survey.title}</TableCell>
                  <TableCell dir="ltr" className="text-muted-foreground">
                    {survey.questionnaireCode}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={survey.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {survey.startDate ? formatDate(survey.startDate, culture) : '—'}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {survey.endDate ? formatDate(survey.endDate, culture) : '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canEdit && survey.status === SurveyStatus.Draft && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setEditingId(survey.id)}
                          aria-label={t.common.edit}
                        >
                          <Pencil className="size-4" />
                        </Button>
                      )}

                      {canPublish && (
                        <LifecycleMenu
                          status={survey.status}
                          onAction={(action) => {
                            if (action === 'publish' || action === 'close' || action === 'archive') {
                              setLifecycleTarget({ id: survey.id, action });
                            } else {
                              void runLifecycle(survey.id, action);
                            }
                          }}
                        />
                      )}

                      {canDelete && survey.status === SurveyStatus.Draft && (
                        <Button
                          variant="ghost"
                          size="icon"
                          className="text-destructive"
                          onClick={() => setDeletingId(survey.id)}
                          aria-label={t.common.delete}
                        >
                          <Trash2 className="size-4" />
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {surveys.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {createOpen && <SurveyDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingId && (
        <SurveyDialog open={!!editingId} onClose={() => setEditingId(null)} surveyId={editingId} />
      )}

      <ConfirmDialog
        open={!!deletingId}
        onClose={() => setDeletingId(null)}
        title={t.surveys.deleteConfirmTitle}
        description={t.surveys.deleteConfirmDescription}
        confirmLabel={t.common.delete}
        isPending={deleteMutation.isPending}
        error={
          deleteMutation.error instanceof ApiError
            ? t.errors.fromCode(deleteMutation.error.code)
            : deleteMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!deletingId) return;
          deleteMutation.mutate(deletingId, { onSuccess: () => setDeletingId(null) });
        }}
      />

      <ConfirmDialog
        open={!!lifecycleTarget}
        onClose={() => setLifecycleTarget(null)}
        title={lifecycleTitle(t.surveys, lifecycleTarget?.action)}
        description={lifecycleDescription(t.surveys, lifecycleTarget?.action)}
        confirmLabel={lifecycleLabel(t.surveys, lifecycleTarget?.action)}
        destructive={lifecycleTarget?.action === 'close' || lifecycleTarget?.action === 'archive'}
        isPending={lifecycleMutation.isPending}
        error={
          lifecycleMutation.error instanceof ApiError
            ? t.errors.fromCode(lifecycleMutation.error.code)
            : lifecycleMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!lifecycleTarget) return;
          void runLifecycle(lifecycleTarget.id, lifecycleTarget.action);
        }}
      />
    </AppLayout>
  );
}

interface LifecycleMenuProps {
  status: SurveyStatus;
  onAction: (action: SurveyAction) => void;
}

/**
 * منوی کرکره‌ای برای اکشن‌های چرخه‌ی عمر بر اساس وضعیت جاری.
 * هر وضعیت فقط انتقال‌های مجاز را نشان می‌دهد.
 */
function LifecycleMenu({ status, onAction }: LifecycleMenuProps) {
  const { t } = useLanguage();
  const [open, setOpen] = useState(false);

  const actions = availableActions(status);

  if (actions.length === 0) {
    return null;
  }

  return (
    <div className="relative">
      <Button
        variant="ghost"
        size="icon"
        onClick={() => setOpen((previous) => !previous)}
        aria-label={t.common.actions}
        aria-expanded={open}
      >
        <MoreVertical className="size-4" />
      </Button>

      {open && (
        <>
          <div className="fixed inset-0 z-30" onClick={() => setOpen(false)} aria-hidden />
          <div className="absolute end-0 top-full z-40 mt-1 w-40 rounded-md border bg-card text-card-foreground shadow-md">
            <div className="flex flex-col p-1">
              {actions.map((action) => (
                <button
                  key={action}
                  type="button"
                  className="rounded-sm px-3 py-2 text-start text-sm transition-colors hover:bg-accent"
                  onClick={() => {
                    setOpen(false);
                    onAction(action);
                  }}
                >
                    {lifecycleLabel(t.surveys, action)}
                </button>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

/**
 * انتقال‌های مجاز از هر وضعیت، بر اساس ماشین وضعیت سمت سرور.
 */
function availableActions(status: SurveyStatus): SurveyAction[] {
  switch (status) {
    case SurveyStatus.Draft: return ['publish'];
    case SurveyStatus.Scheduled: return ['start', 'archive'];
    case SurveyStatus.Active: return ['pause', 'close'];
    case SurveyStatus.Paused: return ['resume', 'close'];
    case SurveyStatus.Closed: return ['archive'];
    default: return [];
  }
}

function lifecycleLabel(t: SurveysDictionary, action?: SurveyAction): string {
  if (!action) return '';
  switch (action) {
    case 'publish': return t.publish;
    case 'start': return t.start;
    case 'pause': return t.pause;
    case 'resume': return t.resume;
    case 'close': return t.close;
    case 'archive': return t.archive;
  }
}

function lifecycleTitle(t: SurveysDictionary, action?: SurveyAction): string {
  if (!action) return '';
  switch (action) {
    case 'publish': return t.publishConfirmTitle;
    case 'close': return t.closeConfirmTitle;
    case 'archive': return t.archiveConfirmTitle;
    default: return '';
  }
}

function lifecycleDescription(t: SurveysDictionary, action?: SurveyAction): string {
  if (!action) return '';
  switch (action) {
    case 'publish': return t.publishConfirmDescription;
    case 'close': return t.closeConfirmDescription;
    case 'archive': return t.archiveConfirmDescription;
    default: return '';
  }
}

function statusOptions(t: SurveysDictionary) {
  return [
    { value: 'all', label: t.all },
    { value: String(SurveyStatus.Draft), label: t.draft },
    { value: String(SurveyStatus.Scheduled), label: t.scheduled },
    { value: String(SurveyStatus.Active), label: t.activeStatus },
    { value: String(SurveyStatus.Paused), label: t.paused },
    { value: String(SurveyStatus.Closed), label: t.closed }
  ];
}

type SurveysDictionary = Dictionary['surveys'];

function StatusBadge({ status }: { status: SurveyStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case SurveyStatus.Draft:
      return <Badge variant="outline">{t.surveys.draft}</Badge>;
    case SurveyStatus.Scheduled:
      return <Badge variant="warning">{t.surveys.scheduled}</Badge>;
    case SurveyStatus.Active:
      return <Badge variant="success">{t.surveys.activeStatus}</Badge>;
    case SurveyStatus.Paused:
      return <Badge variant="warning">{t.surveys.paused}</Badge>;
    case SurveyStatus.Closed:
      return <Badge variant="default">{t.surveys.closed}</Badge>;
    default:
      return <Badge variant="outline">{t.surveys.archived}</Badge>;
  }
}
