import { useEffect, useState, type FormEvent } from 'react';
import { ScrollText, Search, Plus, Pencil } from 'lucide-react';

import {
  SystemPolicyType,
  type SystemPolicyDto
} from '@/api/systemConfiguration';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useCreateSystemPolicy,
  useSystemPolicySearch,
  useUpdateSystemPolicy
} from '@/api/systemConfigurationHooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { Pagination } from '@/routes/identity/UsersPage';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import type { Dictionary } from '@/i18n/types';

/**
 * صفحه‌ی سیاست‌های سیستمی: فهرست صفحه‌بندی‌شده با فیلتر بر اساس نوع و وضعیت
 * فعالیت؛ ایجاد و ویرایش مقدار سیاست.
 */
export function SystemPoliciesPage() {
  const { t } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [type, setType] = useState<number | null>(null);
  const [isEnabled, setIsEnabled] = useState<boolean | null>(null);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<SystemPolicyDto | null>(null);

  const request = { type, searchText, isEnabled, page, pageSize: 50 };

  const { data, isLoading, isError, error, refetch } = useSystemPolicySearch(request);

  const canManage = hasPermission(Permissions.System.Manage);

  const policies = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.system.policiesTitle}
        description={t.system.policiesDescription}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.system.newPolicy}
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
            placeholder={t.system.policyKey + ' / ' + t.system.policyName}
            className="ps-9"
          />
        </div>

        <Select
          value={type === null ? 'all' : String(type)}
          onChange={(event) => {
            setType(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={policyTypeOptions(t)}
          aria-label={t.system.policyType}
        />

        <Select
          value={isEnabled === null ? 'all' : String(isEnabled)}
          onChange={(event) => {
            setIsEnabled(event.target.value === 'all' ? null : event.target.value === 'true');
            setPage(1);
          }}
          options={enabledFilterOptions(t)}
          aria-label={t.system.policyIsEnabled}
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
        ) : policies.length === 0 ? (
          <EmptyState
            title={t.system.noPolicies}
            description={t.system.noPoliciesDescription}
            icon={<ScrollText className="size-10" />}
            action={
              canManage
                ? { label: t.system.newPolicy, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.system.policyKey}</TableHead>
                <TableHead>{t.system.policyType}</TableHead>
                <TableHead>{t.system.policyValue}</TableHead>
                <TableHead>{t.system.policyIsEnabled}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {policies.map((policy) => (
                <TableRow key={policy.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium" dir="ltr">
                        {policy.key}
                      </span>
                      <span className="text-xs text-muted-foreground">{policy.name}</span>
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {policyTypeLabel(t, policy.type)}
                  </TableCell>
                  <TableCell>
                    <span className="font-mono text-xs" dir="ltr">
                      {policy.value}
                    </span>
                  </TableCell>
                  <TableCell>
                    {policy.isEnabled ? (
                      <Badge variant="success">{t.common.active}</Badge>
                    ) : (
                      <Badge variant="outline">{t.common.inactive}</Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canManage && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setEditing(policy)}
                          aria-label={t.common.edit}
                        >
                          <Pencil className="size-4" />
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

      {policies.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {policies.length > 0 && (
        <p className="mt-4 text-xs text-muted-foreground" dir="ltr">
          {t.system.policiesDescription}
        </p>
      )}

      {createOpen && <PolicyDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editing && (
        <PolicyDialog open={!!editing} onClose={() => setEditing(null)} existing={editing} />
      )}
    </AppLayout>
  );
}

/**
 * دیالوگ ایجاد/ویرایش یک سیاست سیستمی.
 */
function PolicyDialog({
  open,
  onClose,
  existing
}: {
  open: boolean;
  onClose: () => void;
  existing?: SystemPolicyDto | null;
}) {
  const { t } = useLanguage();
  const isEdit = !!existing;

  const createMutation = useCreateSystemPolicy();
  const updateMutation = useUpdateSystemPolicy();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  const [form, setForm] = useState<PolicyFormState>(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

  function updateField<K extends keyof PolicyFormState>(field: K, value: PolicyFormState[K]) {
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

    if (!isEdit && !form.key.trim()) next.key = t.system.policyKey + ' ' + t.common.required;
    if (!isEdit && !form.name.trim()) next.name = t.system.policyName + ' ' + t.common.required;
    if (!form.value.trim()) next.value = t.system.policyValue + ' ' + t.common.required;

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    try {
      if (isEdit && existing) {
        await updateMutation.mutateAsync({
          type: existing.type,
          key: existing.key,
          request: { value: form.value, isEnabled: form.isEnabled }
        });
      } else {
        await createMutation.mutateAsync({
          type: form.type,
          key: form.key.trim(),
          name: form.name.trim(),
          description: form.description.trim() || null,
          value: form.value,
          defaultValue: form.defaultValue.trim() || null,
          isEnabled: form.isEnabled
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

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.common.edit : t.system.newPolicy}
      description={t.system.policiesDescription}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="policy-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="policy-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        {!isEdit && (
          <FormField label={t.system.policyType} htmlFor="policyType">
            <Select
              id="policyType"
              value={String(form.type)}
              onChange={(event) =>
                updateField('type', Number(event.target.value) as SystemPolicyType)
              }
              options={policyTypeOptions(t)}
              disabled={isSaving}
            />
          </FormField>
        )}

        <FormField label={t.system.policyKey} htmlFor="policyKey" required error={errors.key}>
          <Input
            id="policyKey"
            value={form.key}
            onChange={(event) => updateField('key', event.target.value)}
            disabled={isSaving || isEdit}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.system.policyName} htmlFor="policyName" required error={errors.name}>
          <Input
            id="policyName"
            value={form.name}
            onChange={(event) => updateField('name', event.target.value)}
            disabled={isSaving || isEdit}
          />
        </FormField>

        <FormField
          label={t.system.policyValue}
          htmlFor="policyValue"
          required
          error={errors.value}
          className="sm:col-span-2"
        >
          <Input
            id="policyValue"
            value={form.value}
            onChange={(event) => updateField('value', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.system.policyDefaultValue} htmlFor="policyDefaultValue">
          <Input
            id="policyDefaultValue"
            value={form.defaultValue}
            onChange={(event) => updateField('defaultValue', event.target.value)}
            disabled={isSaving || isEdit}
            dir="ltr"
          />
        </FormField>

        <label className="col-span-full flex cursor-pointer items-center gap-3 rounded-md border p-3">
          <Checkbox
            checked={form.isEnabled}
            onCheckedChange={(checked) => updateField('isEnabled', checked)}
            disabled={isSaving}
          />
          <span className="text-sm font-medium">{t.system.policyIsEnabled}</span>
        </label>

        {isEdit && existing && (
          <p className="col-span-full text-xs text-muted-foreground" dir="ltr">
            {t.system.updatedAt}:{' '}
            {existing.updatedAt ? existing.updatedAt : existing.createdAt}
          </p>
        )}
      </form>
    </Dialog>
  );
}

interface PolicyFormState {
  type: SystemPolicyType;
  key: string;
  name: string;
  description: string;
  value: string;
  defaultValue: string;
  isEnabled: boolean;
}

function createEmptyForm(): PolicyFormState {
  return {
    type: SystemPolicyType.Password,
    key: '',
    name: '',
    description: '',
    value: '',
    defaultValue: '',
    isEnabled: true
  };
}

function toForm(existing: SystemPolicyDto): PolicyFormState {
  return {
    type: existing.type,
    key: existing.key,
    name: existing.name,
    description: existing.description ?? '',
    value: existing.value,
    defaultValue: existing.defaultValue ?? '',
    isEnabled: existing.isEnabled
  };
}

function enabledFilterOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: 'true', label: t.common.active },
    { value: 'false', label: t.common.inactive }
  ];
}

function policyTypeOptions(t: Dictionary) {
  return [
    { value: String(SystemPolicyType.Password), label: t.system.policyPassword },
    { value: String(SystemPolicyType.Session), label: t.system.policySession },
    { value: String(SystemPolicyType.ResponsePrivacy), label: t.system.policyResponsePrivacy },
    { value: String(SystemPolicyType.DataRetention), label: t.system.policyDataRetention },
    { value: String(SystemPolicyType.LoginSecurity), label: t.system.policyLoginSecurity },
    { value: String(SystemPolicyType.Custom), label: t.system.policyCustom }
  ];
}

function policyTypeLabel(t: Dictionary, type: number): string {
  return policyTypeOptions(t).find((option) => Number(option.value) === type)?.label
    ?? t.system.policyCustom;
}
