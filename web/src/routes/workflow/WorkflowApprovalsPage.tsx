import { useState } from 'react';
import { Check, X, ShieldCheck } from 'lucide-react';

import { ApprovalStatus, type WorkflowApprovalRequestDto } from '@/api/workflows';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useDecideWorkflowApproval,
  useWorkflowApprovalSearch
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
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';

/**
 * صفحه‌ی درخواست‌های تأیید گردش کار. مرز واقعی سمت سرور است: سرویس فقط
 * درخواست‌هایی را برمی‌گرداند که کاربر جاری مجوز تصمیم‌گیری درباره‌ی آن‌ها را
 * داشته باشد (یا با فیلتر «قابل تأیید توسط من»).
 */
export function WorkflowApprovalsPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [status, setStatus] = useState<number | null>(ApprovalStatus.Pending);
  const [approvableByMe, setApprovableByMe] = useState(true);
  const [page, setPage] = useState(1);

  const [decision, setDecision] = useState<{ request: WorkflowApprovalRequestDto; kind: 'approve' | 'reject' } | null>(
    null
  );

  const request = { status, approvableByMe, page, pageSize: 20 };

  const { data, isLoading, isError, error, refetch } = useWorkflowApprovalSearch(request);

  const decideMutation = useDecideWorkflowApproval();

  const canApprove = hasPermission(Permissions.Workflows.Approve);

  const approvals = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader title={t.workflows.approvals} description={t.workflows.description} />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => {
            setStatus(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={approvalStatusOptions(t)}
          aria-label={t.workflows.status}
        />

        <label className="flex cursor-pointer items-center gap-2 rounded-md border p-2.5 text-sm">
          <input
            type="checkbox"
            checked={approvableByMe}
            onChange={(event) => {
              setApprovableByMe(event.target.checked);
              setPage(1);
            }}
            className="size-4 accent-primary"
          />
          {t.workflows.approvableByMe}
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
        ) : approvals.length === 0 ? (
          <EmptyState
            title={t.workflows.noApprovals}
            description={t.workflows.noApprovalsDescription}
            icon={<ShieldCheck className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.workflows.transitions}</TableHead>
                <TableHead>{t.workflows.status}</TableHead>
                <TableHead>{t.workflows.requestedAt}</TableHead>
                <TableHead>{t.workflows.expiresAt}</TableHead>
                <TableHead>{t.workflows.decidedBy}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {approvals.map((approval) => (
                <TableRow key={approval.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium" dir="ltr">
                        {approval.transitionCode}
                      </span>
                      <span className="text-xs text-muted-foreground" dir="ltr">
                        {approval.fromStateCode} → {approval.toStateCode}
                      </span>
                      {approval.decisionNote && (
                        <span className="text-xs text-muted-foreground">{approval.decisionNote}</span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <ApprovalStatusBadge status={approval.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {formatDateTime(approval.requestedAt, culture)}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {approval.expiresAt ? formatDateTime(approval.expiresAt, culture) : '—'}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {approval.decidedByUserName ?? '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canApprove && approval.status === ApprovalStatus.Pending && approval.canCurrentUserDecide && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setDecision({ request: approval, kind: 'approve' })}
                            aria-label={t.workflows.approve}
                            title={t.workflows.approve}
                          >
                            <Check className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setDecision({ request: approval, kind: 'reject' })}
                            aria-label={t.workflows.reject}
                            title={t.workflows.reject}
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

      {approvals.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {decision && (
        <DecisionDialog
          approval={decision.request}
          kind={decision.kind}
          onClose={() => setDecision(null)}
          isPending={decideMutation.isPending}
          error={
            decideMutation.error instanceof ApiError
              ? t.errors.fromCode(decideMutation.error.code)
              : decideMutation.error
                ? t.errors.generic
                : undefined
          }
          onDecide={(note) =>
            decideMutation.mutate(
              { id: decision.request.id, decision: decision.kind, request: { note: note || null } },
              { onSuccess: () => setDecision(null) }
            )
          }
        />
      )}
    </AppLayout>
  );
}

/**
 * دیالوگ ثبت تصمیم (تأیید یا رد) روی یک درخواست تأیید.
 */
function DecisionDialog({
  approval,
  kind,
  onClose,
  onDecide,
  isPending,
  error
}: {
  approval: WorkflowApprovalRequestDto;
  kind: 'approve' | 'reject';
  onClose: () => void;
  onDecide: (note: string) => void;
  isPending: boolean;
  error?: string;
}) {
  const { t } = useLanguage();

  const [note, setNote] = useState('');

  const isApprove = kind === 'approve';

  return (
    <Dialog
      open
      onClose={onClose}
      title={isApprove ? t.workflows.approveConfirmTitle : t.workflows.rejectConfirmTitle}
      description={isApprove
        ? t.workflows.approveConfirmDescription
        : t.workflows.rejectConfirmDescription}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isPending}>
            {t.common.cancel}
          </Button>
          <Button
            variant={isApprove ? 'default' : 'destructive'}
            onClick={() => onDecide(note)}
            disabled={isPending}
          >
            {isPending ? t.common.saving : isApprove ? t.workflows.approve : t.workflows.reject}
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

        <div className="rounded-md border p-3 text-sm">
          <div className="flex items-center justify-between gap-2">
            <span className="text-muted-foreground">{t.workflows.transitions}</span>
            <span className="font-medium" dir="ltr">
              {approval.transitionCode}
            </span>
          </div>
          <div className="mt-1 flex items-center justify-between gap-2">
            <span className="text-muted-foreground">{t.workflows.currentState}</span>
            <span className="font-medium" dir="ltr">
              {approval.fromStateCode} → {approval.toStateCode}
            </span>
          </div>
        </div>

        <FormField label={t.workflows.decisionNote} htmlFor="decisionNote" hint={t.workflows.decisionNoteHint}>
          <Input
            id="decisionNote"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            disabled={isPending}
          />
        </FormField>
      </div>
    </Dialog>
  );
}

function approvalStatusOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(ApprovalStatus.Pending), label: t.workflows.approvalPending },
    { value: String(ApprovalStatus.Approved), label: t.workflows.approvalApproved },
    { value: String(ApprovalStatus.Rejected), label: t.workflows.approvalRejected },
    { value: String(ApprovalStatus.Cancelled), label: t.workflows.approvalCancelled },
    { value: String(ApprovalStatus.Expired), label: t.workflows.approvalExpired }
  ];
}

function ApprovalStatusBadge({ status }: { status: ApprovalStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case ApprovalStatus.Approved:
      return <Badge variant="success">{t.workflows.approvalApproved}</Badge>;
    case ApprovalStatus.Rejected:
      return <Badge variant="destructive">{t.workflows.approvalRejected}</Badge>;
    case ApprovalStatus.Cancelled:
      return <Badge variant="outline">{t.workflows.approvalCancelled}</Badge>;
    case ApprovalStatus.Expired:
      return <Badge variant="outline">{t.workflows.approvalExpired}</Badge>;
    default:
      return <Badge variant="warning">{t.workflows.approvalPending}</Badge>;
  }
}
