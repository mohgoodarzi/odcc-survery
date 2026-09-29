import { useState } from 'react';
import { Workflow as WorkflowIcon, Plus, Pencil, Search, Archive, Activity } from 'lucide-react';

import { WorkflowStatus, type WorkflowDto } from '@/api/workflows';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useActivateWorkflow,
  useArchiveWorkflow,
  useWorkflowSearch
} from '@/api/workflowsHooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { Pagination } from '@/routes/identity/UsersPage';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';
import { entityTypeOptions, workflowStatusOptions, WorkflowDialog } from './WorkflowDialog';

/**
 * صفحه‌ی مدیریت تعاریف گردش کار: فهرست صفحه‌بندی‌شده با فیلتر بر اساس نوع
 * موجودیت و وضعیت؛ ایجاد/ویرایش، فعال‌سازی و بایگانی.
 */
export function WorkflowsPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [entityType, setEntityType] = useState<number | null>(null);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<WorkflowDto | null>(null);
  const [activateTarget, setActivateTarget] = useState<string | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<string | null>(null);

  const request = { searchText, status, entityType, includeArchived, page, pageSize: 20 };

  const { data, isLoading, isError, error, refetch } = useWorkflowSearch(request);

  const activateMutation = useActivateWorkflow();
  const archiveMutation = useArchiveWorkflow();

  const canManage = hasPermission(Permissions.Workflows.Manage);

  const workflows = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.workflows.title}
        description={t.workflows.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.workflows.newWorkflow}
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute start-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={searchText ?? ''}
            onChange={(event) => {
              setSearchText(event.target.value || null);
              setPage(1);
            }}
            placeholder={t.workflows.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={entityType === null ? 'all' : String(entityType)}
          onChange={(event) => {
            setEntityType(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={entityTypeOptions(t)}
          aria-label={t.workflows.entityType}
        />

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => {
            setStatus(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={workflowStatusOptions(t)}
          aria-label={t.workflows.status}
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
          {t.workflows.includeArchived}
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
        ) : workflows.length === 0 ? (
          <EmptyState
            title={t.workflows.noWorkflows}
            description={t.workflows.noWorkflowsDescription}
            icon={<WorkflowIcon className="size-10" />}
            action={
              canManage
                ? { label: t.workflows.newWorkflow, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.workflows.name}</TableHead>
                <TableHead>{t.workflows.entityType}</TableHead>
                <TableHead>{t.workflows.status}</TableHead>
                <TableHead>{t.workflows.states}</TableHead>
                <TableHead>{t.workflows.createdAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {workflows.map((workflow) => (
                <TableRow key={workflow.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium">{workflow.name}</span>
                      <span className="text-xs text-muted-foreground" dir="ltr">
                        {workflow.code} · v{workflow.version}
                      </span>
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {entityTypeLabel(t, workflow.entityType)}
                  </TableCell>
                  <TableCell>
                    <WorkflowStatusBadge status={workflow.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {workflow.states.length} / {workflow.transitions.length}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {formatDateTime(workflow.createdAt, culture)}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canManage && workflow.status !== WorkflowStatus.Archived && (
                        <>
                          {workflow.status === WorkflowStatus.Draft && (
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setActivateTarget(workflow.id)}
                              aria-label={t.workflows.activate}
                              title={t.workflows.activate}
                            >
                              <Activity className="size-4" />
                            </Button>
                          )}

                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setEditing(workflow)}
                            aria-label={t.common.edit}
                          >
                            <Pencil className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setArchiveTarget(workflow.id)}
                            aria-label={t.workflows.archive}
                          >
                            <Archive className="size-4" />
                          </Button>
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

      {workflows.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {createOpen && <WorkflowDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editing && (
        <WorkflowDialog
          open={!!editing}
          onClose={() => setEditing(null)}
          workflowId={editing.id}
          existing={editing}
        />
      )}

      <ConfirmDialog
        open={!!activateTarget}
        onClose={() => setActivateTarget(null)}
        title={t.workflows.activate}
        description={t.workflows.activateImmediatelyHint}
        confirmLabel={t.workflows.activate}
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
        title={t.workflows.archiveConfirmTitle}
        description={t.workflows.archiveConfirmDescription}
        confirmLabel={t.workflows.archive}
        destructive
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
    </AppLayout>
  );
}

function entityTypeLabel(t: Dictionary, type: number): string {
  return entityTypeOptions(t).find((option) => Number(option.value) === type)?.label
    ?? t.workflows.entityCustom;
}

function WorkflowStatusBadge({ status }: { status: WorkflowStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case WorkflowStatus.Active:
      return <Badge variant="success">{t.workflows.statusActive}</Badge>;
    case WorkflowStatus.Archived:
      return <Badge variant="outline">{t.workflows.statusArchived}</Badge>;
    default:
      return <Badge variant="outline">{t.workflows.statusDraft}</Badge>;
  }
}
