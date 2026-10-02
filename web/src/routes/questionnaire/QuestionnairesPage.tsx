import { useState } from 'react';
import { ClipboardList, Eye, Plus, Pencil, Trash2, Search, Send, Archive } from 'lucide-react';

import { ApiError } from '@/api/client';
import { QuestionnaireStatus } from '@/api/questionnaires';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  queryKeys,
  useArchiveQuestionnaire,
  useDeleteQuestionnaire,
  usePublishQuestionnaire,
  useQuestionnairesSearch
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
import { formatDate } from '@/i18n/format';
import { useQueryClient } from '@tanstack/react-query';
import type { Dictionary } from '@/i18n/types';
import { QuestionnaireDialog } from './QuestionnaireDialog';
import { QuestionnaireDetailDialog } from './QuestionnaireDetailDialog';

type QuestionnaireAction = 'publish' | 'archive' | 'delete';

/**
 * صفحه‌ی مدیریت پرسشنامه‌ها: فهرست صفحه‌بندی‌شده، فیلتر بر اساس وضعیت،
 * ساخت/ویرایش/حذف و انتشار/بایگانی.
 *
 * پرسشنامه‌ی فعال در فرم ساخت نظرسنجی قابل انتخاب است؛ ضمناً فقط
 * پیش‌نویس‌ها قابل ویرایش/حذف هستند تا ساختار پاسخ‌گویی ثابت بماند.
 */
export function QuestionnairesPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [viewingId, setViewingId] = useState<string | null>(null);
  const [actionTarget, setActionTarget] = useState<{ id: string; action: QuestionnaireAction } | null>(null);

  const {
    data,
    isLoading,
    isError,
    error,
    refetch
  } = useQuestionnairesSearch({ searchText, status, page });

  const deleteMutation = useDeleteQuestionnaire();
  const publishMutation = usePublishQuestionnaire();
  const archiveMutation = useArchiveQuestionnaire();
  const queryClient = useQueryClient();

  const canManage = hasPermission(Permissions.Questionnaire.Manage);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleStatusChange(value: string) {
    setStatus(value === 'all' ? null : Number(value));
    setPage(1);
  }

  const isActionPending =
    deleteMutation.isPending || publishMutation.isPending || archiveMutation.isPending;

  function runAction(id: string, action: QuestionnaireAction) {
    const mutate =
      action === 'delete' ? deleteMutation : action === 'publish' ? publishMutation : archiveMutation;

    mutate.mutate(id, {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: queryKeys.questionnaires });
        setActionTarget(null);
      }
    });
  }

  const actionError =
    (actionTarget?.action === 'delete' ? deleteMutation.error : null) ??
    (actionTarget?.action === 'publish' ? publishMutation.error : null) ??
    (actionTarget?.action === 'archive' ? archiveMutation.error : null);

  const questionnaires = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.questionnaires.title}
        description={t.questionnaires.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.questionnaires.newQuestionnaire}
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
            placeholder={t.questionnaires.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => handleStatusChange(event.target.value)}
          options={statusOptions(t.questionnaires)}
          aria-label={t.questionnaires.status}
        />
      </div>

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={7} />
        ) : questionnaires.length === 0 ? (
          <EmptyState
            title={t.questionnaires.noQuestionnaires}
            description={t.questionnaires.noQuestionnairesDescription}
            icon={<ClipboardList className="size-10" />}
            action={
              canManage
                ? { label: t.questionnaires.newQuestionnaire, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.questionnaires.code}</TableHead>
                <TableHead>{t.questionnaires.title_}</TableHead>
                <TableHead>{t.questionnaires.status}</TableHead>
                <TableHead>{t.questionnaires.version}</TableHead>
                <TableHead>{t.questionnaires.sections}</TableHead>
                <TableHead>{t.questionnaires.questions}</TableHead>
                <TableHead>{t.questionnaires.createdAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {questionnaires.map((questionnaire) => (
                <TableRow key={questionnaire.id}>
                  <TableCell dir="ltr" className="font-medium">
                    {questionnaire.code}
                  </TableCell>
                  <TableCell>{questionnaire.title}</TableCell>
                  <TableCell>
                    <StatusBadge status={questionnaire.status} />
                  </TableCell>
                  <TableCell dir="ltr" className="text-muted-foreground">
                    {questionnaire.version}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{questionnaire.sectionCount}</TableCell>
                  <TableCell className="text-muted-foreground">{questionnaire.itemCount}</TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {formatDate(questionnaire.createdAt, culture)}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => setViewingId(questionnaire.id)}
                        aria-label={t.questionnaires.viewStructure}
                        title={t.questionnaires.viewStructure}
                      >
                        <Eye className="size-4" />
                      </Button>

                      {canManage && questionnaire.status === QuestionnaireStatus.Draft && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setEditingId(questionnaire.id)}
                            aria-label={t.common.edit}
                            title={t.questionnaires.editQuestionnaire}
                          >
                            <Pencil className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setActionTarget({ id: questionnaire.id, action: 'publish' })}
                            aria-label={t.questionnaires.publish}
                            title={t.questionnaires.publish}
                          >
                            <Send className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setActionTarget({ id: questionnaire.id, action: 'delete' })}
                            aria-label={t.common.delete}
                            title={t.common.delete}
                          >
                            <Trash2 className="size-4" />
                          </Button>
                        </>
                      )}

                      {canManage && questionnaire.status !== QuestionnaireStatus.Archived && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setActionTarget({ id: questionnaire.id, action: 'archive' })}
                          aria-label={t.questionnaires.archive}
                          title={t.questionnaires.archive}
                        >
                          <Archive className="size-4" />
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

      {questionnaires.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {createOpen && <QuestionnaireDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingId && (
        <QuestionnaireDialog
          open={!!editingId}
          onClose={() => setEditingId(null)}
          questionnaireId={editingId}
        />
      )}

      <QuestionnaireDetailDialog
        open={!!viewingId}
        onClose={() => setViewingId(null)}
        questionnaireId={viewingId}
      />

      <ConfirmDialog
        open={!!actionTarget}
        onClose={() => setActionTarget(null)}
        title={actionTitle(t.questionnaires, actionTarget?.action)}
        description={actionDescription(t.questionnaires, actionTarget?.action)}
        confirmLabel={actionLabel(t, actionTarget?.action)}
        destructive={actionTarget?.action === 'delete'}
        isPending={isActionPending}
        error={
          actionError instanceof ApiError
            ? t.errors.fromCode(actionError.code)
            : actionError
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!actionTarget) return;
          runAction(actionTarget.id, actionTarget.action);
        }}
      />
    </AppLayout>
  );
}

function statusOptions(t: QuestionnairesDictionary) {
  return [
    { value: 'all', label: t.all },
    { value: String(QuestionnaireStatus.Draft), label: t.draft },
    { value: String(QuestionnaireStatus.Active), label: t.activeStatus },
    { value: String(QuestionnaireStatus.Archived), label: t.archived }
  ];
}

type QuestionnairesDictionary = Dictionary['questionnaires'];

function actionLabel(t: Dictionary, action?: QuestionnaireAction): string {
  if (!action) return '';
  switch (action) {
    case 'publish': return t.questionnaires.publish;
    case 'archive': return t.questionnaires.archive;
    case 'delete': return t.common.delete;
  }
}

function actionTitle(t: QuestionnairesDictionary, action?: QuestionnaireAction): string {
  if (!action) return '';
  switch (action) {
    case 'publish': return t.publishConfirmTitle;
    case 'archive': return t.archiveConfirmTitle;
    case 'delete': return t.deleteConfirmTitle;
  }
}

function actionDescription(t: QuestionnairesDictionary, action?: QuestionnaireAction): string {
  if (!action) return '';
  switch (action) {
    case 'publish': return t.publishConfirmDescription;
    case 'archive': return t.archiveConfirmDescription;
    case 'delete': return t.deleteConfirmDescription;
  }
}

function StatusBadge({ status }: { status: QuestionnaireStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case QuestionnaireStatus.Active:
      return <Badge variant="success">{t.questionnaires.activeStatus}</Badge>;
    case QuestionnaireStatus.Archived:
      return <Badge variant="outline">{t.questionnaires.archived}</Badge>;
    default:
      return <Badge variant="warning">{t.questionnaires.draft}</Badge>;
  }
}
