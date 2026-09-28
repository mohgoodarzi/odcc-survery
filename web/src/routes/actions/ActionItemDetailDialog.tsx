import { useState, type FormEvent } from 'react';
import {
  ClipboardList, MessageSquarePlus, Paperclip, Download, Trash2, Send, Play, Check,
  RotateCcw, X, AlertTriangle
} from 'lucide-react';

import {
  ActionItemStatus,
  EffectivenessRating,
  EscalationLevel,
  type ActionEvidenceDto
} from '@/api/actions';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { formatDateTime } from '@/i18n/format';
import {
  useActionComments,
  useActionEvidence,
  useActionItem,
  useAddActionComment,
  useAssessActionItemEffectiveness,
  useDeleteActionEvidence,
  useDownloadActionEvidence,
  useTransitionActionItem,
  useUploadActionEvidence
} from '@/api/actionsHooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { ErrorState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';

interface ActionItemDetailDialogProps {
  open: boolean;
  onClose: () => void;
  itemId: string | null;
}

/**
 * جزئیات یک آیتم اقدام: وضعیت و گذارها، ارزیابی اثربخشی، دیدگاه‌ها و پیوست‌ها.
 *
 * **مجوزها:** مسئول آیتم می‌تواند وضعیت کار خودش را تغییر دهد؛ لغو و ارزیابی
 * اثربخشی نیازمند مجوز مدیریت است. مرز امنیتی واقعی سمت سرور است.
 */
export function ActionItemDetailDialog({ open, onClose, itemId }: ActionItemDetailDialogProps) {
  const { t, culture } = useLanguage();

  const { data: item, isLoading, isError, error, refetch } = useActionItem(itemId);
  const { data: comments, refetch: refetchComments } = useActionComments(itemId);
  const { data: evidence } = useActionEvidence(itemId);

  const transitionMutation = useTransitionActionItem();
  const effectivenessMutation = useAssessActionItemEffectiveness();
  const commentMutation = useAddActionComment();
  const uploadMutation = useUploadActionEvidence();
  const downloadMutation = useDownloadActionEvidence();
  const deleteMutation = useDeleteActionEvidence();

  const [commentBody, setCommentBody] = useState('');
  const [cancelTarget, setCancelTarget] = useState(false);
  const [deleteEvidenceTarget, setDeleteEvidenceTarget] = useState<ActionEvidenceDto | null>(null);

  async function handleTransition(newStatus: ActionItemStatus) {
    if (!itemId) return;

    try {
      await transitionMutation.mutateAsync({ id: itemId, request: { newStatus } });
      await refetch();
    } catch (error) {
      handleApiError(error);
    }
  }

  async function handleAssessEffectiveness(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!itemId) return;

    const formData = new FormData(event.currentTarget);
    const rating = Number(formData.get('effectivenessRating')) as EffectivenessRating;
    const note = String(formData.get('effectivenessNote') ?? '');

    try {
      await effectivenessMutation.mutateAsync({
        id: itemId,
        request: { rating, note: note.trim() || null }
      });
      await refetch();
    } catch (error) {
      handleApiError(error);
    }
  }

  async function handleAddComment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!itemId || !commentBody.trim()) return;

    try {
      await commentMutation.mutateAsync({ itemId, request: { body: commentBody.trim() } });
      setCommentBody('');
      await refetchComments();
    } catch (error) {
      handleApiError(error);
    }
  }

  async function handleUploadFile(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file || !itemId) return;

    try {
      await uploadMutation.mutateAsync({ itemId, file });
    } catch (error) {
      handleApiError(error);
    } finally {
      // پاک کردن مقدار ورودی تا بتوان همان فایل را دوباره انتخاب کرد.
      event.target.value = '';
    }
  }

  async function handleDownload(e: ActionEvidenceDto) {
    try {
      await downloadMutation.mutateAsync({ id: e.id, fileName: e.fileName });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function handleConfirmDeleteEvidence() {
    if (!deleteEvidenceTarget) return;

    try {
      await deleteMutation.mutateAsync(deleteEvidenceTarget.id);
      setDeleteEvidenceTarget(null);
    } catch (error) {
      handleApiError(error);
    }
  }

  function handleApiError(error: unknown) {
    if (error instanceof ApiError) {
      window.alert(t.errors.fromCode(error.code));
      return;
    }
    window.alert(t.errors.generic);
  }

  if (isLoading) {
    return (
      <Dialog open={open} onClose={onClose} title={t.actions.itemTitle} size="lg">
        <div className="flex items-center justify-center p-8">
          <Spinner className="size-6" />
        </div>
      </Dialog>
    );
  }

  if (isError || !item) {
    return (
      <Dialog open={open} onClose={onClose} title={t.actions.itemTitle} size="lg">
        <ErrorState
          message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
          onRetry={() => void refetch()}
        />
      </Dialog>
    );
  }

  const isOpen = item.status === ActionItemStatus.Open || item.status === ActionItemStatus.InProgress;
  const isFinished = item.status === ActionItemStatus.Done || item.status === ActionItemStatus.Cancelled;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={item.title}
      description={item.description ?? undefined}
      size="lg"
    >
      <div className="flex flex-wrap items-center gap-2">
        <ItemStatusBadge status={item.status} />
        <PriorityBadge priority={item.priority} />
        {item.escalationLevel !== EscalationLevel.None && (
          <Badge variant="destructive">
            <AlertTriangle className="size-3" />
            {escalationLabel(t.actions, item.escalationLevel)}
          </Badge>
        )}
        {item.isOverdue && (
          <Badge variant="destructive">{t.actions.statsOverdueItems}</Badge>
        )}
      </div>

      <dl className="mt-4 grid grid-cols-2 gap-3 text-sm">
        <div>
          <dt className="text-muted-foreground">{t.actions.planTitle}</dt>
          <dd className="font-medium">{item.planTitle ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t.actions.assignee}</dt>
          <dd className="font-medium">{item.assigneeUserName ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t.actions.dueDate}</dt>
          <dd className="font-medium" dir="ltr">
            {item.dueDate ? formatDateTime(item.dueDate, culture) : '—'}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t.actions.escalation}</dt>
          <dd className="font-medium">{escalationLabel(t.actions, item.escalationLevel)}</dd>
        </div>
      </dl>

      {/* --- گذارهای وضعیت ------------------------------------------------------- */}
      {isOpen && (
        <div className="mt-6 flex flex-wrap gap-2 border-t pt-4">
          {item.status === ActionItemStatus.Open && (
            <Button
              variant="outline"
              size="sm"
              disabled={transitionMutation.isPending}
              onClick={() => handleTransition(ActionItemStatus.InProgress)}
            >
              <Play className="size-4" />
              {t.actions.start}
            </Button>
          )}

          <Button
            variant="outline"
            size="sm"
            disabled={transitionMutation.isPending}
            onClick={() => handleTransition(ActionItemStatus.Done)}
          >
            <Check className="size-4" />
            {t.actions.done}
          </Button>

          <Button
            variant="outline"
            size="sm"
            className="text-destructive"
            disabled={transitionMutation.isPending}
            onClick={() => setCancelTarget(true)}
          >
            <X className="size-4" />
            {t.actions.cancel}
          </Button>
        </div>
      )}

      {item.status === ActionItemStatus.Done && (
        <div className="mt-6 flex flex-wrap gap-2 border-t pt-4">
          <Button
            variant="outline"
            size="sm"
            disabled={transitionMutation.isPending}
            onClick={() => handleTransition(ActionItemStatus.InProgress)}
          >
            <RotateCcw className="size-4" />
            {t.actions.reopen}
          </Button>
        </div>
      )}

      {/* --- ارزیابی اثربخشی ----------------------------------------------------- */}
      {isFinished && (
        <form
          onSubmit={handleAssessEffectiveness}
          className="mt-6 grid gap-4 border-t pt-4 sm:grid-cols-2"
        >
          <h3 className="col-span-full text-sm font-semibold">{t.actions.assessEffectiveness}</h3>

          <FormField label={t.actions.effectiveness} htmlFor="effectivenessRating" required>
            <Select
              id="effectivenessRating"
              name="effectivenessRating"
              defaultValue={String(item.effectiveness)}
              options={effectivenessOptions(t.actions)}
              disabled={effectivenessMutation.isPending}
            />
          </FormField>

          <FormField label={t.actions.effectivenessNote} htmlFor="effectivenessNote">
            <Input
              id="effectivenessNote"
              name="effectivenessNote"
              defaultValue={item.effectivenessNote ?? ''}
              disabled={effectivenessMutation.isPending}
            />
          </FormField>

          <div className="col-span-full flex justify-end">
            <Button type="submit" size="sm" disabled={effectivenessMutation.isPending}>
              {t.common.save}
            </Button>
          </div>
        </form>
      )}

      {/* --- دیدگاه‌ها ------------------------------------------------------------- */}
      <section className="mt-6 border-t pt-4">
        <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold">
          <ClipboardList className="size-4" />
          {t.actions.comments}
        </h3>

        {(comments ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground">{t.actions.noComments}</p>
        ) : (
          <ul className="flex flex-col gap-3">
            {(comments ?? []).map((comment) => (
              <li key={comment.id} className="rounded-md border p-3">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-sm font-medium">{comment.authorUserName ?? '—'}</span>
                  <span className="text-xs text-muted-foreground" dir="ltr">
                    {formatDateTime(comment.createdAt, culture)}
                  </span>
                </div>
                <p className="mt-1 text-sm">{comment.body}</p>
              </li>
            ))}
          </ul>
        )}

        <form onSubmit={handleAddComment} className="mt-3 flex items-end gap-2">
          <FormField label={t.actions.addComment} htmlFor="commentBody" className="flex-1">
            <Input
              id="commentBody"
              value={commentBody}
              onChange={(event) => setCommentBody(event.target.value)}
              placeholder={t.actions.commentPlaceholder}
              disabled={commentMutation.isPending}
            />
          </FormField>

          <Button
            type="submit"
            size="icon"
            disabled={commentMutation.isPending || !commentBody.trim()}
            aria-label={t.actions.addComment}
          >
            <Send className="size-4" />
          </Button>
        </form>
      </section>

      {/* --- پیوست‌ها -------------------------------------------------------------- */}
      <section className="mt-6 border-t pt-4">
        <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold">
          <Paperclip className="size-4" />
          {t.actions.evidence}
        </h3>

        {(evidence ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground">{t.actions.noEvidence}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {(evidence ?? []).map((e) => (
              <li
                key={e.id}
                className="flex items-center justify-between gap-2 rounded-md border p-3"
              >
                <div className="flex min-w-0 flex-col">
                  <span className="truncate text-sm font-medium" title={e.fileName}>
                    {e.fileName}
                  </span>
                  <span className="text-xs text-muted-foreground" dir="ltr">
                    {formatDateTime(e.uploadedAt, culture)}
                  </span>
                </div>

                <div className="flex items-center gap-1">
                  <Button
                    variant="ghost"
                    size="icon"
                    disabled={downloadMutation.isPending}
                    onClick={() => handleDownload(e)}
                    aria-label={t.actions.downloadEvidence}
                    title={t.actions.downloadEvidence}
                  >
                    <Download className="size-4" />
                  </Button>

                  <Button
                    variant="ghost"
                    size="icon"
                    className="text-destructive"
                    disabled={deleteMutation.isPending}
                    onClick={() => setDeleteEvidenceTarget(e)}
                    aria-label={t.actions.deleteEvidence}
                    title={t.actions.deleteEvidence}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}

        <div className="mt-3 flex items-center gap-2">
          <label className="inline-flex cursor-pointer items-center gap-2 rounded-md border p-2.5 text-sm">
            <MessageSquarePlus className="size-4" />
            {t.actions.uploadEvidence}
            <input
              type="file"
              className="sr-only"
              onChange={handleUploadFile}
              disabled={uploadMutation.isPending}
              accept=".pdf,.png,.jpg,.jpeg,.gif,.webp,.docx,.xlsx,.txt,.zip"
            />
          </label>

          {uploadMutation.isPending && <Spinner className="size-4" />}
        </div>
      </section>

      <ConfirmDialog
        open={cancelTarget}
        onClose={() => setCancelTarget(false)}
        title={t.actions.statusItemCancelled}
        description={t.actions.cancelConfirmDescription}
        confirmLabel={t.actions.cancel}
        destructive
        isPending={transitionMutation.isPending}
        onConfirm={() => {
          setCancelTarget(false);
          void handleTransition(ActionItemStatus.Cancelled);
        }}
      />

      <ConfirmDialog
        open={!!deleteEvidenceTarget}
        onClose={() => setDeleteEvidenceTarget(null)}
        title={t.actions.deleteEvidenceConfirmTitle}
        description={t.actions.deleteEvidenceConfirmDescription}
        confirmLabel={t.actions.deleteEvidence}
        destructive
        isPending={deleteMutation.isPending}
        onConfirm={handleConfirmDeleteEvidence}
      />
    </Dialog>
  );
}

function ItemStatusBadge({ status }: { status: ActionItemStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case ActionItemStatus.Open:
      return <Badge variant="outline">{t.actions.statusOpen}</Badge>;
    case ActionItemStatus.InProgress:
      return <Badge variant="default">{t.actions.statusInProgress}</Badge>;
    case ActionItemStatus.Done:
      return <Badge variant="success">{t.actions.statusDone}</Badge>;
    default:
      return <Badge variant="outline">{t.actions.statusItemCancelled}</Badge>;
  }
}

function PriorityBadge({ priority }: { priority: number }) {
  const { t } = useLanguage();

  switch (priority) {
    case 4:
      return <Badge variant="destructive">{t.actions.priorityCritical}</Badge>;
    case 3:
      return <Badge variant="warning">{t.actions.priorityHigh}</Badge>;
    case 1:
      return <Badge variant="outline">{t.actions.priorityLow}</Badge>;
    default:
      return <Badge variant="outline">{t.actions.priorityMedium}</Badge>;
  }
}

function escalationLabel(t: import('@/i18n/types').Dictionary['actions'], level: EscalationLevel): string {
  switch (level) {
    case EscalationLevel.Reminder: return t.escalationReminder;
    case EscalationLevel.EscalatedToOwner: return t.escalationOwner;
    case EscalationLevel.EscalatedToManagement: return t.escalationManagement;
    default: return t.escalationNone;
  }
}

function effectivenessOptions(t: import('@/i18n/types').Dictionary['actions']) {
  return [
    { value: String(EffectivenessRating.Effective), label: t.effectivenessEffective },
    { value: String(EffectivenessRating.PartiallyEffective), label: t.effectivenessPartially },
    { value: String(EffectivenessRating.Ineffective), label: t.effectivenessIneffective }
  ];
}
