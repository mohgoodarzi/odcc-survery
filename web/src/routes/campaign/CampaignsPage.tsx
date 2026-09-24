import { useEffect, useState, type FormEvent } from 'react';
import { Megaphone, Plus, Pencil, Trash2, Search, MoreVertical, Users } from 'lucide-react';

import { ApiError } from '@/api/client';
import { CampaignStatus, TargetAudienceType } from '@/api/campaigns';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  queryKeys,
  useArchiveCampaign,
  useCampaignsSearch,
  useCompleteCampaign,
  useDeleteCampaign,
  useLaunchCampaign,
  useScheduleCampaign,
  type CampaignAction
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
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { formatDateTime } from '@/i18n/format';
import { useQueryClient } from '@tanstack/react-query';
import type { Dictionary } from '@/i18n/types';
import { CampaignDialog } from './CampaignDialog';
import { CampaignDistributionsDialog } from './CampaignDistributionsDialog';

/**
 * صفحه‌ی مدیریت کمپین‌ها: فهرست صفحه‌بندی‌شده، فیلتر بر اساس وضعیت،
 * چرخه‌ی عمر (زمان‌بندی، اجرا، تکمیل، بایگانی) و پیگیری گیرندگان.
 */
export function CampaignsPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [distributionsId, setDistributionsId] = useState<string | null>(null);
  const [scheduleTarget, setScheduleTarget] = useState<string | null>(null);
  const [lifecycleTarget, setLifecycleTarget] = useState<{ id: string; action: CampaignAction } | null>(null);

  const {
    data,
    isLoading,
    isError,
    error,
    refetch
  } = useCampaignsSearch({
    searchText,
    status,
    surveyId: null,
    includeArchived,
    page
  });

  const deleteMutation = useDeleteCampaign();
  const scheduleMutation = useScheduleCampaign();
  const lifecycleMutation = useCampaignLifecycle();
  const queryClient = useQueryClient();

  const canManage = hasPermission(Permissions.Campaign.Manage);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleStatusChange(value: string) {
    setStatus(value === 'all' ? null : Number(value));
    setPage(1);
  }

  async function runLifecycle(id: string, action: CampaignAction) {
    lifecycleMutation.mutate(
      { id, action },
      {
        onSuccess: () => {
          void queryClient.invalidateQueries({ queryKey: queryKeys.campaigns });
          setLifecycleTarget(null);
        }
      }
    );
  }

  const campaigns = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.campaigns.title}
        description={t.campaigns.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.campaigns.newCampaign}
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
            placeholder={t.campaigns.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => handleStatusChange(event.target.value)}
          options={statusOptions(t.campaigns)}
          aria-label={t.campaigns.status}
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
          {t.campaigns.includeArchived}
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
        ) : campaigns.length === 0 ? (
          <EmptyState
            title={t.campaigns.noCampaigns}
            description={t.campaigns.noCampaignsDescription}
            icon={<Megaphone className="size-10" />}
            action={
              canManage
                ? { label: t.campaigns.newCampaign, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.campaigns.code}</TableHead>
                <TableHead>{t.campaigns.title_}</TableHead>
                <TableHead>{t.campaigns.survey}</TableHead>
                <TableHead>{t.campaigns.audienceType}</TableHead>
                <TableHead>{t.campaigns.status}</TableHead>
                <TableHead>{t.campaigns.scheduledAt}</TableHead>
                <TableHead>{t.campaigns.recipients}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {campaigns.map((campaign) => (
                <TableRow key={campaign.id}>
                  <TableCell dir="ltr" className="font-medium">
                    {campaign.code}
                  </TableCell>
                  <TableCell>{campaign.title}</TableCell>
                  <TableCell dir="ltr" className="text-muted-foreground">
                    {campaign.surveyCode}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {audienceLabel(t.campaigns, campaign.audienceType)}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={campaign.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {campaign.scheduledAt ? formatDateTime(campaign.scheduledAt, culture) : '—'}
                  </TableCell>
                  <TableCell dir="ltr">{campaign.totalDistributions ?? '—'}</TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => setDistributionsId(campaign.id)}
                        aria-label={t.campaigns.distributions}
                        title={t.campaigns.distributions}
                      >
                        <Users className="size-4" />
                      </Button>

                      {canManage && campaign.status === CampaignStatus.Draft && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setEditingId(campaign.id)}
                            aria-label={t.common.edit}
                          >
                            <Pencil className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setDeletingId(campaign.id)}
                            aria-label={t.common.delete}
                          >
                            <Trash2 className="size-4" />
                          </Button>
                        </>
                      )}

                      {canManage && (
                        <LifecycleMenu
                          status={campaign.status}
                          onAction={(action) => {
                            if (action === 'schedule') {
                              setScheduleTarget(campaign.id);
                            } else {
                              // اجرا، تکمیل و بایگانی نیازمند تأیید هستند چون اثرات قابل‌بازگشت نیست.
                              setLifecycleTarget({ id: campaign.id, action });
                            }
                          }}
                        />
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {campaigns.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {createOpen && <CampaignDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingId && (
        <CampaignDialog open={!!editingId} onClose={() => setEditingId(null)} campaignId={editingId} />
      )}

      {distributionsId && (
        <CampaignDistributionsDialog
          open={!!distributionsId}
          onClose={() => setDistributionsId(null)}
          campaignId={distributionsId}
          campaignTitle={campaigns.find((c) => c.id === distributionsId)?.title ?? ''}
          totalDistributions={campaigns.find((c) => c.id === distributionsId)?.totalDistributions ?? 0}
        />
      )}

      <ScheduleDialog
        campaignId={scheduleTarget}
        isPending={scheduleMutation.isPending}
        error={
          scheduleMutation.error instanceof ApiError
            ? t.errors.fromCode(scheduleMutation.error.code)
            : scheduleMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={(scheduledAt) => {
          if (!scheduleTarget) return;
          scheduleMutation.mutate(
            { id: scheduleTarget, scheduledAt },
            {
              onSuccess: () => {
                void queryClient.invalidateQueries({ queryKey: queryKeys.campaigns });
                setScheduleTarget(null);
              }
            }
          );
        }}
        onClose={() => setScheduleTarget(null)}
      />

      <ConfirmDialog
        open={!!deletingId}
        onClose={() => setDeletingId(null)}
        title={t.campaigns.deleteConfirmTitle}
        description={t.campaigns.deleteConfirmDescription}
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
        title={lifecycleTitle(t.campaigns, lifecycleTarget?.action)}
        description={lifecycleDescription(t.campaigns, lifecycleTarget?.action)}
        confirmLabel={lifecycleLabel(t.campaigns, lifecycleTarget?.action)}
        destructive={lifecycleTarget?.action === 'archive'}
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

/**
 * اکشن‌های چرخه‌ی عمر: زمان‌بندی، اجرا، تکمیل و بایگانی.
 * اجرای کمپین نیازمند تأیید است چون دعوت‌نامه‌ها ارسال می‌شوند،
 * تکمیل و بایگانی هم قابل بازگشت نیستند.
 */
function useCampaignLifecycle() {
  const launch = useLaunchCampaign();
  const complete = useCompleteCampaign();
  const archive = useArchiveCampaign();

  return {
    mutate: ({ id, action }: { id: string; action: CampaignAction }, options?: { onSuccess?: () => void }) => {
      switch (action) {
        case 'launch':
          launch.mutate(id, options);
          break;
        case 'complete':
          complete.mutate(id, options);
          break;
        case 'archive':
          archive.mutate(id, options);
          break;
        // 'schedule' مسیر جداگانه‌ی ScheduleDialog را طی می‌کند.
      }
    },
    isPending: launch.isPending || complete.isPending || archive.isPending,
    error: launch.error ?? complete.error ?? archive.error
  };
}

interface LifecycleMenuProps {
  status: CampaignStatus;
  onAction: (action: CampaignAction) => void;
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
                  {lifecycleLabel(t.campaigns, action)}
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
 *
 * Draft → Scheduled | Launch
 * Scheduled → Launch
 * Running → Complete
 * Completed → Archive
 * هر وضعیتی به غیر از بایگانی‌شده → Archive
 */
function availableActions(status: CampaignStatus): CampaignAction[] {
  switch (status) {
    case CampaignStatus.Draft: return ['schedule', 'launch', 'archive'];
    case CampaignStatus.Scheduled: return ['launch', 'archive'];
    case CampaignStatus.Running: return ['complete', 'archive'];
    case CampaignStatus.Completed: return ['archive'];
    default: return [];
  }
}

function lifecycleLabel(t: Dictionary['campaigns'], action?: CampaignAction): string {
  if (!action) return '';
  switch (action) {
    case 'schedule': return t.schedule;
    case 'launch': return t.launch;
    case 'complete': return t.complete;
    case 'archive': return t.archive;
  }
}

function lifecycleTitle(t: Dictionary['campaigns'], action?: CampaignAction): string {
  if (!action) return '';
  switch (action) {
    case 'launch': return t.launchConfirmTitle;
    case 'complete': return t.completeConfirmTitle;
    case 'archive': return t.archiveConfirmTitle;
    default: return '';
  }
}

function lifecycleDescription(t: Dictionary['campaigns'], action?: CampaignAction): string {
  if (!action) return '';
  switch (action) {
    case 'launch': return t.launchConfirmDescription;
    case 'complete': return t.completeConfirmDescription;
    case 'archive': return t.archiveConfirmDescription;
    default: return '';
  }
}

function audienceLabel(t: Dictionary['campaigns'], audienceType: TargetAudienceType): string {
  switch (audienceType) {
    case TargetAudienceType.OrgUnits: return t.audienceOrgUnits;
    case TargetAudienceType.Employees: return t.audienceEmployees;
    default: return t.audienceAllCompany;
  }
}

function statusOptions(t: Dictionary['campaigns']) {
  return [
    { value: 'all', label: t.all },
    { value: String(CampaignStatus.Draft), label: t.draft },
    { value: String(CampaignStatus.Scheduled), label: t.scheduled },
    { value: String(CampaignStatus.Running), label: t.running },
    { value: String(CampaignStatus.Completed), label: t.completed }
  ];
}

function StatusBadge({ status }: { status: CampaignStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case CampaignStatus.Draft:
      return <Badge variant="outline">{t.campaigns.draft}</Badge>;
    case CampaignStatus.Scheduled:
      return <Badge variant="warning">{t.campaigns.scheduled}</Badge>;
    case CampaignStatus.Running:
      return <Badge variant="success">{t.campaigns.running}</Badge>;
    case CampaignStatus.Completed:
      return <Badge variant="default">{t.campaigns.completed}</Badge>;
    default:
      return <Badge variant="outline">{t.campaigns.archived}</Badge>;
  }
}

interface ScheduleDialogProps {
  campaignId: string | null;
  isPending: boolean;
  error?: string;
  onConfirm: (scheduledAt: string) => void;
  onClose: () => void;
}

/**
 * دیالوگ زمان‌بندی کمپین: زمان شروع با ورودی datetime-local.
 * زمان انتخاب‌شده به UTC تبدیل می‌شود تا با سرور هماهنگ باشد.
 */
function ScheduleDialog({ campaignId, isPending, error, onConfirm, onClose }: ScheduleDialogProps) {
  const { t } = useLanguage();
  const [scheduledAt, setScheduledAt] = useState('');

  // هر بار که دیالوگ برای کمپین جدید باز می‌شود، فیلد پاک می‌شود.
  useEffect(() => {
    setScheduledAt('');
  }, [campaignId]);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!scheduledAt) return;

    const iso = new Date(scheduledAt).toISOString();
    onConfirm(iso);
  }

  return (
    <Dialog
      open={!!campaignId}
      onClose={onClose}
      title={t.campaigns.scheduleTitle}
      description={t.campaigns.scheduleDescription}
      size="sm"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isPending}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="schedule-form" disabled={isPending || !scheduledAt}>
            {isPending ? t.common.saving : t.campaigns.schedule}
          </Button>
        </>
      }
    >
      <form id="schedule-form" onSubmit={handleSubmit} className="grid gap-4">
        {error && (
          <div
            className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {error}
          </div>
        )}

        <FormField label={t.campaigns.scheduledAt} htmlFor="campaignScheduleAt" required>
          <Input
            id="campaignScheduleAt"
            type="datetime-local"
            value={scheduledAt}
            onChange={(event) => setScheduledAt(event.target.value)}
            disabled={isPending}
            dir="ltr"
          />
        </FormField>

        <p className="text-xs text-muted-foreground">{t.campaigns.scheduleHint}</p>
      </form>
    </Dialog>
  );
}
