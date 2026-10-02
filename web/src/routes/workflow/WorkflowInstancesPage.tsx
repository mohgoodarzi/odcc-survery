import { useState } from 'react';
import { Workflow as WorkflowIcon, Search, X, ArrowRight } from 'lucide-react';

import { WorkflowInstanceState, type WorkflowInstanceDto } from '@/api/workflows';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useCancelWorkflowInstance,
  useTransitionWorkflowInstance,
  useWorkflowInstanceSearch
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
import { Dialog } from '@/components/ui/dialog';
import { DateTimePicker } from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';
import { localDateTimeToIso } from '@/lib/datetime';

/**
 * صفحه‌ی نمونه‌های گردش کار: فهرست صفحه‌بندی‌شده با فیلتر بر اساس وضعیت،
 * گذارهای مجاز روی نمونه‌های در حال اجرا و لغو.
 */
export function WorkflowInstancesPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [runningOnly, setRunningOnly] = useState(false);
  const [page, setPage] = useState(1);

  const [transitionTarget, setTransitionTarget] = useState<WorkflowInstanceDto | null>(null);
  const [cancelTarget, setCancelTarget] = useState<string | null>(null);

  const request = { searchText, status, runningOnly, page, pageSize: 20 };

  const { data, isLoading, isError, error, refetch } = useWorkflowInstanceSearch(request);

  const transitionMutation = useTransitionWorkflowInstance();
  const cancelMutation = useCancelWorkflowInstance();

  const canManage = hasPermission(Permissions.Workflows.Manage);

  const instances = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader title={t.workflows.instances} description={t.workflows.description} />

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
          value={status === null ? 'all' : String(status)}
          onChange={(event) => {
            setStatus(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={instanceStatusOptions(t)}
          aria-label={t.workflows.status}
        />

        <label className="flex cursor-pointer items-center gap-2 rounded-md border p-2.5 text-sm">
          <input
            type="checkbox"
            checked={runningOnly}
            onChange={(event) => {
              setRunningOnly(event.target.checked);
              setPage(1);
            }}
            className="size-4 accent-primary"
          />
          {t.workflows.runningOnly}
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
        ) : instances.length === 0 ? (
          <EmptyState
            title={t.workflows.noInstances}
            description={t.workflows.noInstancesDescription}
            icon={<WorkflowIcon className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.workflows.definition}</TableHead>
                <TableHead>{t.workflows.currentState}</TableHead>
                <TableHead>{t.workflows.status}</TableHead>
                <TableHead>{t.workflows.startedBy}</TableHead>
                <TableHead>{t.workflows.startedAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {instances.map((instance) => (
                <TableRow key={instance.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium" dir="ltr">
                        {instance.workflowCode} · v{instance.workflowVersion}
                      </span>
                      <span className="text-xs text-muted-foreground" dir="ltr">
                        {instance.entityId}
                      </span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <Badge variant="default">{instance.currentStateCode}</Badge>
                  </TableCell>
                  <TableCell>
                    <InstanceStateBadge status={instance.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {instance.startedByUserName ?? '—'}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {formatDateTime(instance.startedAt, culture)}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {instance.pendingApproval && (
                        <Badge variant="warning">{t.workflows.approvalPending}</Badge>
                      )}

                      {canManage && instance.status === WorkflowInstanceState.Running && (
                        <>
                          {instance.nextTransitions.length > 0 && !instance.pendingApproval && (
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setTransitionTarget(instance)}
                              aria-label={t.workflows.nextTransitions}
                              title={t.workflows.nextTransitions}
                            >
                              <ArrowRight className="size-4" />
                            </Button>
                          )}

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setCancelTarget(instance.id)}
                            aria-label={t.workflows.cancel}
                            title={t.workflows.cancel}
                          >
                            <X className="size-4" />
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

      {instances.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {transitionTarget && (
        <TransitionDialog
          instance={transitionTarget}
          onClose={() => setTransitionTarget(null)}
          isPending={transitionMutation.isPending}
          error={
            transitionMutation.error instanceof ApiError
              ? t.errors.fromCode(transitionMutation.error.code)
              : transitionMutation.error
                ? t.errors.generic
                : undefined
          }
          onTransition={(transitionCode, note, expiresAt) =>
            transitionMutation.mutate(
              {
                id: transitionTarget.id,
                request: {
                  transitionCode,
                  note: note || null,
                  approvalExpiresAtUtc: localDateTimeToIso(expiresAt)
                }
              },
              { onSuccess: () => setTransitionTarget(null) }
            )
          }
        />
      )}

      <ConfirmDialog
        open={!!cancelTarget}
        onClose={() => setCancelTarget(null)}
        title={t.workflows.cancelConfirmTitle}
        description={t.workflows.cancelConfirmDescription}
        confirmLabel={t.workflows.cancel}
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
    </AppLayout>
  );
}

/**
 * دیالوگ انتخاب یک گذار مجاز از وضعیت فعلی نمونه. اگر گذار نیازمند تأیید باشد،
 * یک درخواست تأیید ساخته می‌شود.
 */
function TransitionDialog({
  instance,
  onClose,
  onTransition,
  isPending,
  error
}: {
  instance: WorkflowInstanceDto;
  onClose: () => void;
  onTransition: (transitionCode: string, note: string, expiresAt: string | null) => void;
  isPending: boolean;
  error?: string;
}) {
  const { t } = useLanguage();

  const [transitionCode, setTransitionCode] = useState(instance.nextTransitions[0]?.code ?? '');
  const [note, setNote] = useState('');
  const [expiresAt, setExpiresAt] = useState('');

  const selected = instance.nextTransitions.find((tr) => tr.code === transitionCode);

  return (
    <Dialog
      open
      onClose={onClose}
      title={t.workflows.nextTransitions}
      description={`${instance.workflowCode} · ${instance.currentStateCode}`}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isPending}>
            {t.common.cancel}
          </Button>
          <Button onClick={() => onTransition(transitionCode, note, expiresAt || null)} disabled={isPending || !transitionCode}>
            {isPending ? t.common.saving : t.workflows.decide}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error && (
          <div
            className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {error}
          </div>
        )}

        <FormField label={t.workflows.transitions} htmlFor="transitionSelect">
          <Select
            id="transitionSelect"
            value={transitionCode}
            onChange={(event) => setTransitionCode(event.target.value)}
            options={instance.nextTransitions.map((tr) => ({
              value: tr.code,
              label: `${tr.name} (${tr.fromStateCode} → ${tr.toStateCode})${
                tr.requiresApproval ? ' · ' + t.workflows.requiresApproval : ''
              }`
            }))}
            disabled={isPending}
          />
        </FormField>

        {selected?.requiresApproval && (
          <>
            <FormField
              label={t.workflows.note}
              htmlFor="transitionNote"
              hint={t.workflows.decisionNoteHint}
            >
              <Input
                id="transitionNote"
                value={note}
                onChange={(event) => setNote(event.target.value)}
                disabled={isPending}
              />
            </FormField>

            <FormField label={t.workflows.approvalExpiry} htmlFor="transitionExpiry" hint={t.workflows.approvalExpiryHint}>
              <DateTimePicker
                id="transitionExpiry"
                value={expiresAt}
                onChange={setExpiresAt}
                disabled={isPending}
              />
            </FormField>
          </>
        )}
      </div>
    </Dialog>
  );
}

function instanceStatusOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(WorkflowInstanceState.Running), label: t.workflows.instanceRunning },
    { value: String(WorkflowInstanceState.Completed), label: t.workflows.instanceCompleted },
    { value: String(WorkflowInstanceState.Cancelled), label: t.workflows.instanceCancelled },
    { value: String(WorkflowInstanceState.Failed), label: t.workflows.instanceFailed }
  ];
}

function InstanceStateBadge({ status }: { status: WorkflowInstanceState }) {
  const { t } = useLanguage();

  switch (status) {
    case WorkflowInstanceState.Completed:
      return <Badge variant="success">{t.workflows.instanceCompleted}</Badge>;
    case WorkflowInstanceState.Cancelled:
      return <Badge variant="outline">{t.workflows.instanceCancelled}</Badge>;
    case WorkflowInstanceState.Failed:
      return <Badge variant="destructive">{t.workflows.instanceFailed}</Badge>;
    default:
      return <Badge variant="warning">{t.workflows.instanceRunning}</Badge>;
  }
}
