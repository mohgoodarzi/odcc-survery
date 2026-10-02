import { useEffect, useState, type FormEvent } from 'react';
import { Flag, Search, Plus, Pencil } from 'lucide-react';

import {
  FeatureFlagState,
  type FeatureFlagDto
} from '@/api/systemConfiguration';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useCreateFeatureFlag,
  useFeatureFlagSearch,
  useTurnFeatureOff,
  useTurnFeatureOn,
  useUpdateFeatureFlag
} from '@/api/systemConfigurationHooks';
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
import { DateTimePicker } from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { formatDateTime } from '@/i18n/format';
import { isoToLocalDateTime } from '@/lib/datetime';
import type { Dictionary } from '@/i18n/types';

/**
 * صفحه‌ی پرچم‌های ویژگی: فهرست صفحه‌بندی‌شده با فیلتر بر اساس حالت؛ روشن/خاموش
 * کردن سریع و پیکربندی کامل (درصد، فهرست مجاز، انقضا).
 */
export function FeatureFlagsPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [state, setState] = useState<number | null>(null);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<FeatureFlagDto | null>(null);
  const [toggleTarget, setToggleTarget] = useState<{ key: string; on: boolean } | null>(null);

  const request = { searchText, state, page, pageSize: 50 };

  const { data, isLoading, isError, error, refetch } = useFeatureFlagSearch(request);

  const turnOnMutation = useTurnFeatureOn();
  const turnOffMutation = useTurnFeatureOff();

  const canManage = hasPermission(Permissions.System.Manage);

  const flags = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.system.featureFlagsTitle}
        description={t.system.featureFlagsDescription}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.system.newFeatureFlag}
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
            placeholder={t.system.flagKey + ' / ' + t.system.flagName}
            className="ps-9"
            dir="ltr"
          />
        </div>

        <Select
          value={state === null ? 'all' : String(state)}
          onChange={(event) => {
            setState(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={flagStateOptions(t)}
          aria-label={t.system.flagState}
        />
      </div>

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={5} />
        ) : flags.length === 0 ? (
          <EmptyState
            title={t.system.noFeatureFlags}
            description={t.system.noFeatureFlagsDescription}
            icon={<Flag className="size-10" />}
            action={
              canManage
                ? { label: t.system.newFeatureFlag, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.system.flagKey}</TableHead>
                <TableHead>{t.system.flagState}</TableHead>
                <TableHead>{t.system.flagIsEnabled}</TableHead>
                <TableHead>{t.system.expiresAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {flags.map((flag) => (
                <TableRow key={flag.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium" dir="ltr">
                        {flag.key}
                      </span>
                      <span className="text-xs text-muted-foreground">{flag.name}</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <FlagStateBadge state={flag.state} />
                      {flag.state === FeatureFlagState.Percentage && (
                        <span className="text-xs text-muted-foreground" dir="ltr">
                          {flag.percentage}%
                        </span>
                      )}
                      {flag.state === FeatureFlagState.AllowList && (
                        <span className="text-xs text-muted-foreground" dir="ltr">
                          {flag.allowedUserIds.length} {t.system.allowedUsers} ·{' '}
                          {flag.allowedRoles.length} {t.system.allowedRoles}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    {flag.isEnabled ? (
                      <Badge variant="success">{t.common.active}</Badge>
                    ) : (
                      <Badge variant="outline">{t.common.inactive}</Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {flag.expiresAt ? formatDateTime(flag.expiresAt, culture) : '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canManage && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setToggleTarget({ key: flag.key, on: !flag.isEnabled })}
                            aria-label={flag.isEnabled ? t.system.turnOff : t.system.turnOn}
                            title={flag.isEnabled ? t.system.turnOff : t.system.turnOn}
                          >
                            {flag.isEnabled ? '◌' : '●'}
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setEditing(flag)}
                            aria-label={t.common.edit}
                          >
                            <Pencil className="size-4" />
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

      {flags.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {createOpen && <FeatureFlagDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editing && (
        <FeatureFlagDialog
          open={!!editing}
          onClose={() => setEditing(null)}
          existing={editing}
        />
      )}

      {toggleTarget && (
        <ConfirmToggleDialog
          target={toggleTarget}
          onClose={() => setToggleTarget(null)}
          isPending={turnOnMutation.isPending || turnOffMutation.isPending}
          error={
            (turnOnMutation.error instanceof ApiError
              ? t.errors.fromCode(turnOnMutation.error.code)
              : turnOnMutation.error
                ? t.errors.generic
                : undefined) ??
            (turnOffMutation.error instanceof ApiError
              ? t.errors.fromCode(turnOffMutation.error.code)
              : turnOffMutation.error
                ? t.errors.generic
                : undefined)
          }
          onConfirm={() => {
            const mutation = toggleTarget.on ? turnOnMutation : turnOffMutation;
            mutation.mutate(toggleTarget.key, { onSuccess: () => setToggleTarget(null) });
          }}
        />
      )}
    </AppLayout>
  );
}

/**
 * دیالوگ تأیید روشن/خاموش کردن سریع یک پرچم.
 */
function ConfirmToggleDialog({
  target,
  onClose,
  onConfirm,
  isPending,
  error
}: {
  target: { key: string; on: boolean };
  onClose: () => void;
  onConfirm: () => void;
  isPending: boolean;
  error?: string;
}) {
  const { t } = useLanguage();

  return (
    <Dialog
      open
      onClose={onClose}
      title={target.on ? t.system.turnOn : t.system.turnOff}
      description={t.system.featureFlagsDescription}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isPending}>
            {t.common.cancel}
          </Button>
          <Button onClick={onConfirm} disabled={isPending}>
            {isPending ? t.common.saving : target.on ? t.system.turnOn : t.system.turnOff}
          </Button>
        </>
      }
    >
      {error && (
        <div
          className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
          role="alert"
        >
          {error}
        </div>
      )}
      <p className="font-mono text-sm" dir="ltr">
        {target.key}
      </p>
    </Dialog>
  );
}

/**
 * دیالوگ ایجاد/پیکربندی کامل یک پرچم ویژگی.
 */
function FeatureFlagDialog({
  open,
  onClose,
  existing
}: {
  open: boolean;
  onClose: () => void;
  existing?: FeatureFlagDto | null;
}) {
  const { t } = useLanguage();
  const isEdit = !!existing;

  const createMutation = useCreateFeatureFlag();
  const updateMutation = useUpdateFeatureFlag();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  const [form, setForm] = useState<FlagFormState>(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

  function updateField<K extends keyof FlagFormState>(field: K, value: FlagFormState[K]) {
    setForm((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => {
      if (!(field in previous)) return previous;
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  function validate(): boolean {
    const next: Record<string, string> = {};

    if (!isEdit && !form.key.trim()) next.key = t.system.flagKey + ' ' + t.common.required;
    if (!isEdit && !form.name.trim()) next.name = t.system.flagName + ' ' + t.common.required;

    if (form.state === FeatureFlagState.Percentage) {
      if (form.percentage < 0 || form.percentage > 100) {
        next.percentage = t.system.percentageHint;
      }
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const expiresAt = form.expiresAtRaw ? new Date(form.expiresAtRaw).toISOString() : null;

    try {
      if (isEdit && existing) {
        await updateMutation.mutateAsync({
          key: existing.key,
          request: {
            state: form.state,
            percentage: form.state === FeatureFlagState.Percentage ? form.percentage : null,
            allowedUserIds:
              form.state === FeatureFlagState.AllowList ? splitList(form.allowedUsersRaw) : null,
            allowedRoles:
              form.state === FeatureFlagState.AllowList ? splitList(form.allowedRolesRaw) : null,
            expiresAt
          }
        });
      } else {
        await createMutation.mutateAsync({
          key: form.key.trim(),
          name: form.name.trim(),
          description: form.description.trim() || null,
          state: form.state,
          percentage: form.state === FeatureFlagState.Percentage ? form.percentage : null,
          allowedUserIds:
            form.state === FeatureFlagState.AllowList ? splitList(form.allowedUsersRaw) : null,
          allowedRoles:
            form.state === FeatureFlagState.AllowList ? splitList(form.allowedRolesRaw) : null,
          expiresAt
        });
      }

      onClose();
    } catch (error) {
      setErrorsFromApi(error);
    }
  }

  function setErrorsFromApi(error: unknown) {
    if (!(error instanceof ApiError)) {
      setFormError(t.errors.generic);
      return;
    }

    if (error.status === 400 && Object.keys(error.validationErrors).length > 0) {
      const translated: Record<string, string> = {};
      const firstMessage = Object.values(error.validationErrors)[0]?.[0];

      for (const [field, messages] of Object.entries(error.validationErrors)) {
        translated[field] = messages[0] ?? t.errors.validation;
      }

      setErrors(translated);
      setFormError(firstMessage ?? t.errors.validation);
      return;
    }

    setFormError(t.errors.fromCode(error.code));
  }

  const isAllowList = form.state === FeatureFlagState.AllowList;
  const isPercentage = form.state === FeatureFlagState.Percentage;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.common.edit : t.system.newFeatureFlag}
      description={t.system.featureFlagsDescription}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="flag-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="flag-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.system.flagKey} htmlFor="flagKey" required error={errors.key}>
          <Input
            id="flagKey"
            value={form.key}
            onChange={(event) => updateField('key', event.target.value)}
            disabled={isSaving || isEdit}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.system.flagName} htmlFor="flagName" required error={errors.name}>
          <Input
            id="flagName"
            value={form.name}
            onChange={(event) => updateField('name', event.target.value)}
            disabled={isSaving || isEdit}
          />
        </FormField>

        <FormField label={t.system.flagState} htmlFor="flagState">
          <Select
            id="flagState"
            value={String(form.state)}
            onChange={(event) =>
              updateField('state', Number(event.target.value) as FeatureFlagState)
            }
            options={flagStateOptions(t)}
            disabled={isSaving}
          />
        </FormField>

        {isPercentage && (
          <FormField
            label={t.system.percentage}
            htmlFor="flagPercentage"
            error={errors.percentage}
            hint={t.system.percentageHint}
          >
            <Input
              id="flagPercentage"
              type="number"
              min={0}
              max={100}
              value={form.percentage}
              onChange={(event) => updateField('percentage', Number(event.target.value))}
              disabled={isSaving}
              dir="ltr"
            />
          </FormField>
        )}

        {isAllowList && (
          <>
            <FormField
              label={t.system.allowedUsers}
              htmlFor="flagAllowedUsers"
              hint={t.system.allowedUsersHint}
              className="sm:col-span-2"
            >
              <Input
                id="flagAllowedUsers"
                value={form.allowedUsersRaw}
                onChange={(event) => updateField('allowedUsersRaw', event.target.value)}
                disabled={isSaving}
                dir="ltr"
              />
            </FormField>

            <FormField
              label={t.system.allowedRoles}
              htmlFor="flagAllowedRoles"
              hint={t.system.allowedRolesHint}
              className="sm:col-span-2"
            >
              <Input
                id="flagAllowedRoles"
                value={form.allowedRolesRaw}
                onChange={(event) => updateField('allowedRolesRaw', event.target.value)}
                disabled={isSaving}
                dir="ltr"
              />
            </FormField>
          </>
        )}

        <FormField label={t.system.expiresAt} htmlFor="flagExpiresAt">
          <DateTimePicker
            id="flagExpiresAt"
            value={form.expiresAtRaw}
            onChange={(value) => updateField('expiresAtRaw', value)}
            disabled={isSaving}
          />
        </FormField>
      </form>
    </Dialog>
  );
}

interface FlagFormState {
  key: string;
  name: string;
  description: string;
  state: FeatureFlagState;
  percentage: number;
  allowedUsersRaw: string;
  allowedRolesRaw: string;
  expiresAtRaw: string;
}

function createEmptyForm(): FlagFormState {
  return {
    key: '',
    name: '',
    description: '',
    state: FeatureFlagState.Off,
    percentage: 0,
    allowedUsersRaw: '',
    allowedRolesRaw: '',
    expiresAtRaw: ''
  };
}

function toForm(existing: FeatureFlagDto): FlagFormState {
  return {
    key: existing.key,
    name: existing.name,
    description: existing.description ?? '',
    state: existing.state,
    percentage: existing.percentage || 0,
    allowedUsersRaw: existing.allowedUserIds.join(', '),
    allowedRolesRaw: existing.allowedRoles.join(', '),
    expiresAtRaw: isoToLocalDateTime(existing.expiresAt)
  };
}

function splitList(value: string): string[] {
  return value
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean);
}

function flagStateOptions(t: Dictionary) {
  return [
    { value: String(FeatureFlagState.Off), label: t.system.flagOff },
    { value: String(FeatureFlagState.On), label: t.system.flagOn },
    { value: String(FeatureFlagState.Percentage), label: t.system.flagPercentage },
    { value: String(FeatureFlagState.AllowList), label: t.system.flagAllowList }
  ];
}

function FlagStateBadge({ state }: { state: FeatureFlagState }) {
  const { t } = useLanguage();

  switch (state) {
    case FeatureFlagState.On:
      return <Badge variant="success">{t.system.flagOn}</Badge>;
    case FeatureFlagState.Percentage:
      return <Badge variant="warning">{t.system.flagPercentage}</Badge>;
    case FeatureFlagState.AllowList:
      return <Badge variant="default">{t.system.flagAllowList}</Badge>;
    default:
      return <Badge variant="outline">{t.system.flagOff}</Badge>;
  }
}
