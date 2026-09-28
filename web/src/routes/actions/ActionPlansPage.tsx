import { useState } from 'react';
import { ClipboardCheck, Plus, Pencil, Search, Check, X, Archive, Activity } from 'lucide-react';

import {
  ActionPlanStatus,
  ActionPriority,
  type ActionPlanDto
} from '@/api/actions';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useActionPlanSearch,
  useActivateActionPlan,
  useArchiveActionPlan,
  useCancelActionPlan,
  useCompleteActionPlan
} from '@/api/actionsHooks';
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
import { ActionPlanDialog } from './ActionPlanDialog';

/**
 * صفحه‌ی مدیریت برنامه‌های اقدام: فهرست صفحه‌بندی‌شده با فیلتر بر اساس وضعیت،
 * اولویت و منشأ؛ ایجاد/ویرایش، فعال‌سازی، تکمیل، لغو و بایگانی.
 */
export function ActionPlansPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [priority, setPriority] = useState<number | null>(null);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<ActionPlanDto | null>(null);
  const [activateTarget, setActivateTarget] = useState<string | null>(null);
  const [completeTarget, setCompleteTarget] = useState<string | null>(null);
  const [cancelTarget, setCancelTarget] = useState<string | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<string | null>(null);

  const request = {
    searchText,
    status,
    priority,
    includeArchived,
    page,
    pageSize: 20
  };

  const { data, isLoading, isError, error, refetch } = useActionPlanSearch(request);

  const activateMutation = useActivateActionPlan();
  const completeMutation = useCompleteActionPlan();
  const cancelMutation = useCancelActionPlan();
  const archiveMutation = useArchiveActionPlan();

  const canManage = hasPermission(Permissions.Actions.Manage);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleStatusChange(value: string) {
    setStatus(value === 'all' ? null : Number(value));
    setPage(1);
  }

  function handlePriorityChange(value: string) {
    setPriority(value === 'all' ? null : Number(value));
    setPage(1);
  }

  const plans = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.actions.title}
        description={t.actions.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.actions.newPlan}
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
            placeholder={t.actions.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => handleStatusChange(event.target.value)}
          options={statusOptions(t)}
          aria-label={t.actions.status}
        />

        <Select
          value={priority === null ? 'all' : String(priority)}
          onChange={(event) => handlePriorityChange(event.target.value)}
          options={priorityFilterOptions(t)}
          aria-label={t.actions.priority}
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
          {t.actions.includeArchived}
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
        ) : plans.length === 0 ? (
          <EmptyState
            title={t.actions.noPlans}
            description={t.actions.noPlansDescription}
            icon={<ClipboardCheck className="size-10" />}
            action={
              canManage
                ? { label: t.actions.newPlan, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.actions.planTitle}</TableHead>
                <TableHead>{t.actions.status}</TableHead>
                <TableHead>{t.actions.priority}</TableHead>
                <TableHead>{t.actions.owner}</TableHead>
                <TableHead>{t.actions.progress}</TableHead>
                <TableHead>{t.actions.dueDate}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {plans.map((plan) => (
                <TableRow key={plan.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium">{plan.title}</span>
                      {plan.surveyTitle && (
                        <span className="text-xs text-muted-foreground" dir="ltr">
                          {plan.surveyTitle} ({plan.surveyCode})
                        </span>
                      )}
                      {plan.orgUnitPath && (
                        <span className="text-xs text-muted-foreground">{plan.orgUnitPath}</span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <PlanStatusBadge status={plan.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {priorityLabel(t.actions, plan.priority)}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {plan.ownerUserName ?? '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col gap-1">
                      <span className="text-sm" dir="ltr">
                        {plan.completedItemCount}/{plan.totalItemCount} ({plan.progressPercentage}%)
                      </span>
                      <div className="h-1.5 w-24 overflow-hidden rounded-full bg-muted">
                        <div
                          className="h-full bg-primary"
                          style={{ width: `${Math.min(100, plan.progressPercentage)}%` }}
                        />
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {plan.dueDate ? formatDateTime(plan.dueDate, culture) : '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canManage && plan.status !== ActionPlanStatus.Archived && (
                        <>
                          {plan.status === ActionPlanStatus.Draft && (
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setActivateTarget(plan.id)}
                              aria-label={t.actions.activate}
                              title={t.actions.activate}
                            >
                              <Activity className="size-4" />
                            </Button>
                          )}

                          {plan.status === ActionPlanStatus.Active && (
                            <>
                              <Button
                                variant="ghost"
                                size="icon"
                                onClick={() => setCompleteTarget(plan.id)}
                                aria-label={t.actions.complete}
                                title={t.actions.complete}
                              >
                                <Check className="size-4" />
                              </Button>

                              <Button
                                variant="ghost"
                                size="icon"
                                onClick={() => setCancelTarget(plan.id)}
                                aria-label={t.actions.cancel}
                                title={t.actions.cancel}
                              >
                                <X className="size-4" />
                              </Button>
                            </>
                          )}

                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setEditing(plan)}
                            aria-label={t.common.edit}
                          >
                            <Pencil className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setArchiveTarget(plan.id)}
                            aria-label={t.actions.archive}
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

      {plans.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      <p className="mt-4 flex items-center gap-2 text-xs text-muted-foreground">
        <ClipboardCheck className="size-4" />
        {t.actions.privacyNote}
      </p>

      {createOpen && <ActionPlanDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editing && (
        <ActionPlanDialog
          open={!!editing}
          onClose={() => setEditing(null)}
          planId={editing.id}
          existing={editing}
        />
      )}

      <ConfirmDialog
        open={!!activateTarget}
        onClose={() => setActivateTarget(null)}
        title={t.actions.activateConfirmTitle}
        description={t.actions.activateConfirmDescription}
        confirmLabel={t.actions.activate}
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
        open={!!completeTarget}
        onClose={() => setCompleteTarget(null)}
        title={t.actions.completeConfirmTitle}
        description={t.actions.completeConfirmDescription}
        confirmLabel={t.actions.complete}
        destructive={false}
        isPending={completeMutation.isPending}
        error={
          completeMutation.error instanceof ApiError
            ? t.errors.fromCode(completeMutation.error.code)
            : completeMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!completeTarget) return;
          completeMutation.mutate(completeTarget, { onSuccess: () => setCompleteTarget(null) });
        }}
      />

      <ConfirmDialog
        open={!!cancelTarget}
        onClose={() => setCancelTarget(null)}
        title={t.actions.cancelConfirmTitle}
        description={t.actions.cancelConfirmDescription}
        confirmLabel={t.actions.cancel}
        destructive
        isPending={cancelMutation.isPending}
        error={
          cancelMutation.error instanceof ApiError
            ? t.errors.fromCode(cancelMutation.error.code)
            : cancelMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!cancelTarget) return;
          cancelMutation.mutate(cancelTarget, { onSuccess: () => setCancelTarget(null) });
        }}
      />

      <ConfirmDialog
        open={!!archiveTarget}
        onClose={() => setArchiveTarget(null)}
        title={t.actions.archiveConfirmTitle}
        description={t.actions.archiveConfirmDescription}
        confirmLabel={t.actions.archive}
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

function statusOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(ActionPlanStatus.Draft), label: t.actions.statusDraft },
    { value: String(ActionPlanStatus.Active), label: t.actions.statusActive },
    { value: String(ActionPlanStatus.Completed), label: t.actions.statusCompleted },
    { value: String(ActionPlanStatus.Cancelled), label: t.actions.statusCancelled },
    { value: String(ActionPlanStatus.Archived), label: t.actions.statusArchived }
  ];
}

function priorityFilterOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(ActionPriority.Low), label: t.actions.priorityLow },
    { value: String(ActionPriority.Medium), label: t.actions.priorityMedium },
    { value: String(ActionPriority.High), label: t.actions.priorityHigh },
    { value: String(ActionPriority.Critical), label: t.actions.priorityCritical }
  ];
}

function PlanStatusBadge({ status }: { status: ActionPlanStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case ActionPlanStatus.Active:
      return <Badge variant="success">{t.actions.statusActive}</Badge>;
    case ActionPlanStatus.Completed:
      return <Badge variant="default">{t.actions.statusCompleted}</Badge>;
    case ActionPlanStatus.Cancelled:
      return <Badge variant="outline">{t.actions.statusCancelled}</Badge>;
    case ActionPlanStatus.Archived:
      return <Badge variant="outline">{t.actions.statusArchived}</Badge>;
    default:
      return <Badge variant="outline">{t.actions.statusDraft}</Badge>;
  }
}

function priorityLabel(t: Dictionary['actions'], priority: number): string {
  switch (priority) {
    case 4: return t.priorityCritical;
    case 3: return t.priorityHigh;
    case 1: return t.priorityLow;
    default: return t.priorityMedium;
  }
}
