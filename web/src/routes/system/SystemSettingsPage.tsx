import { useEffect, useState, type FormEvent } from 'react';
import { Settings as SettingsIcon, Search, Plus, Pencil, Lock } from 'lucide-react';

import { SettingValueType, type SettingDto } from '@/api/systemConfiguration';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useCreateSetting,
  useSettingSearch,
  useUpdateSetting
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
import { FormField } from '@/components/ui/form-field';
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';

/**
 * صفحه‌ی تنظیمات سامانه: فهرست صفحه‌بندی‌شده با فیلتر بر اساس گروه، نوع مقدار و
 * حساسیت؛ ایجاد و ویرایش مقدار. مقادیر حساس هرگز نمایش داده نمی‌شوند.
 */
export function SystemSettingsPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [valueType, setValueType] = useState<number | null>(null);
  const [isSensitive, setIsSensitive] = useState<boolean | null>(null);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<SettingDto | null>(null);

  const request = { searchText, valueType, isSensitive, page, pageSize: 50 };

  const { data, isLoading, isError, error, refetch } = useSettingSearch(request);

  const canManage = hasPermission(Permissions.System.Manage);

  const settings = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.system.settingsTitle}
        description={t.system.settingsDescription}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.system.newSetting}
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
            placeholder={t.system.settingKey + ' / ' + t.system.settingName}
            className="ps-9"
            dir="ltr"
          />
        </div>

        <Select
          value={valueType === null ? 'all' : String(valueType)}
          onChange={(event) => {
            setValueType(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={valueTypeOptions(t)}
          aria-label={t.system.valueType}
        />

        <Select
          value={isSensitive === null ? 'all' : String(isSensitive)}
          onChange={(event) => {
            setIsSensitive(event.target.value === 'all' ? null : event.target.value === 'true');
            setPage(1);
          }}
          options={sensitiveFilterOptions(t)}
          aria-label={t.system.isSensitive}
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
        ) : settings.length === 0 ? (
          <EmptyState
            title={t.system.noSettings}
            description={t.system.noSettingsDescription}
            icon={<SettingsIcon className="size-10" />}
            action={
              canManage
                ? { label: t.system.newSetting, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.system.settingKey}</TableHead>
                <TableHead>{t.system.valueType}</TableHead>
                <TableHead>{t.system.settingValue}</TableHead>
                <TableHead>{t.system.updatedAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {settings.map((setting) => (
                <TableRow key={setting.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium" dir="ltr">
                        {setting.key}
                      </span>
                      <span className="text-xs text-muted-foreground">{setting.name}</span>
                      {setting.isSensitive && (
                        <Badge variant="warning">{t.system.isSensitive}</Badge>
                      )}
                      {setting.isReadOnly && (
                        <Badge variant="outline">{t.system.isReadOnly}</Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {valueTypeLabel(t, setting.valueType)}
                  </TableCell>
                  <TableCell>
                    {setting.isSensitive ? (
                      <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                        <Lock className="size-3.5" />
                        {setting.hasValue ? t.system.sensitiveValueHidden : t.common.noData}
                      </span>
                    ) : (
                      <span className="font-mono text-xs" dir="ltr">
                        {setting.value ?? setting.defaultValue ?? '—'}
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {setting.updatedAt
                      ? `${formatDateTime(setting.updatedAt, culture)} · ${setting.lastModifiedByUserName ?? '—'}`
                      : '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canManage && !setting.isReadOnly && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setEditing(setting)}
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

      {settings.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {createOpen && <SettingDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editing && (
        <SettingDialog
          open={!!editing}
          onClose={() => setEditing(null)}
          existing={editing}
        />
      )}
    </AppLayout>
  );
}

/**
 * دیالوگ ایجاد تنظیم یا ویرایش مقدار آن. در حالت ویرایش فقط مقدار تغییر
 * می‌کند (کلید و نوع مقدار ثابت می‌مانند).
 */
function SettingDialog({
  open,
  onClose,
  existing
}: {
  open: boolean;
  onClose: () => void;
  existing?: SettingDto | null;
}) {
  const { t } = useLanguage();
  const isEdit = !!existing;

  const createMutation = useCreateSetting();
  const updateMutation = useUpdateSetting();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  const [form, setForm] = useState<SettingFormState>(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

  function updateField<K extends keyof SettingFormState>(field: K, value: SettingFormState[K]) {
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

    if (!isEdit && !form.key.trim()) next.key = t.system.settingKey + ' ' + t.common.required;
    if (!isEdit && !form.name.trim()) next.name = t.system.settingName + ' ' + t.common.required;
    if (!form.value.trim() && !form.isSensitive) next.value = t.system.settingValue + ' ' + t.common.required;

    if (form.valueType === SettingValueType.WholeNumber && form.value && !/^-?\d+$/.test(form.value.trim())) {
      next.value = t.system.valueWholeNumber + ' ' + t.common.required;
    }
    if (
      form.valueType === SettingValueType.FractionalNumber &&
      form.value &&
      Number.isNaN(Number(form.value.trim()))
    ) {
      next.value = t.system.valueFractionalNumber + ' ' + t.common.required;
    }
    if (form.valueType === SettingValueType.TrueFalse && form.value && !/^(true|false)$/i.test(form.value.trim())) {
      next.value = t.system.valueTrueFalse + ' ' + t.common.required;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    try {
      if (isEdit && existing) {
        await updateMutation.mutateAsync({ key: existing.key, request: { value: form.value } });
      } else {
        await createMutation.mutateAsync({
          key: form.key.trim(),
          name: form.name.trim(),
          description: form.description.trim() || null,
          valueType: form.valueType,
          value: form.value,
          defaultValue: form.defaultValue.trim() || null,
          group: form.group.trim() || null,
          isSensitive: form.isSensitive
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
      title={isEdit ? t.common.edit : t.system.newSetting}
      description={t.system.settingsDescription}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="setting-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="setting-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.system.settingKey} htmlFor="settingKey" required error={errors.key}>
          <Input
            id="settingKey"
            value={form.key}
            onChange={(event) => updateField('key', event.target.value)}
            disabled={isSaving || isEdit}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.system.settingName} htmlFor="settingName" required error={errors.name}>
          <Input
            id="settingName"
            value={form.name}
            onChange={(event) => updateField('name', event.target.value)}
            disabled={isSaving || isEdit}
          />
        </FormField>

        <FormField label={t.system.settingValue} htmlFor="settingValue" required error={errors.value}>
          <Input
            id="settingValue"
            value={form.value}
            onChange={(event) => updateField('value', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.system.settingDefaultValue} htmlFor="settingDefaultValue">
          <Input
            id="settingDefaultValue"
            value={form.defaultValue}
            onChange={(event) => updateField('defaultValue', event.target.value)}
            disabled={isSaving || isEdit}
            dir="ltr"
          />
        </FormField>

        {!isEdit && (
          <FormField label={t.system.valueType} htmlFor="settingValueType">
            <Select
              id="settingValueType"
              value={String(form.valueType)}
              onChange={(event) =>
                updateField('valueType', Number(event.target.value) as SettingValueType)
              }
              options={valueTypeOptions(t)}
              disabled={isSaving}
            />
          </FormField>
        )}

        {!isEdit && (
          <FormField label={t.system.settingGroup} htmlFor="settingGroup">
            <Input
              id="settingGroup"
              value={form.group}
              onChange={(event) => updateField('group', event.target.value)}
              disabled={isSaving}
              dir="ltr"
            />
          </FormField>
        )}

        {!isEdit && (
          <label className="col-span-full flex cursor-pointer items-center gap-3 rounded-md border p-3">
            <input
              type="checkbox"
              checked={form.isSensitive}
              onChange={(event) => updateField('isSensitive', event.target.checked)}
              disabled={isSaving}
              className="size-4 accent-primary"
            />
            <span className="flex flex-col gap-0.5">
              <span className="text-sm font-medium">{t.system.isSensitive}</span>
              <span className="text-xs text-muted-foreground">{t.system.isSensitiveHint}</span>
            </span>
          </label>
        )}

        <p className="col-span-full text-xs text-muted-foreground">{t.system.securityNote}</p>
      </form>
    </Dialog>
  );
}

interface SettingFormState {
  key: string;
  name: string;
  description: string;
  valueType: SettingValueType;
  value: string;
  defaultValue: string;
  group: string;
  isSensitive: boolean;
}

function createEmptyForm(): SettingFormState {
  return {
    key: '',
    name: '',
    description: '',
    valueType: SettingValueType.Text,
    value: '',
    defaultValue: '',
    group: '',
    isSensitive: false
  };
}

function toForm(existing: SettingDto): SettingFormState {
  return {
    key: existing.key,
    name: existing.name,
    description: existing.description ?? '',
    valueType: existing.valueType,
    value: existing.value ?? existing.defaultValue ?? '',
    defaultValue: existing.defaultValue ?? '',
    group: existing.group ?? '',
    isSensitive: existing.isSensitive
  };
}

function sensitiveFilterOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: 'true', label: t.system.isSensitive },
    { value: 'false', label: t.system.settingsTitle }
  ];
}

export function valueTypeOptions(t: Dictionary) {
  return [
    { value: String(SettingValueType.Text), label: t.system.valueText },
    { value: String(SettingValueType.WholeNumber), label: t.system.valueWholeNumber },
    { value: String(SettingValueType.FractionalNumber), label: t.system.valueFractionalNumber },
    { value: String(SettingValueType.TrueFalse), label: t.system.valueTrueFalse },
    { value: String(SettingValueType.Date), label: t.system.valueDate },
    { value: String(SettingValueType.Email), label: t.system.valueEmail },
    { value: String(SettingValueType.Url), label: t.system.valueUrl },
    { value: String(SettingValueType.Duration), label: t.system.valueDuration },
    { value: String(SettingValueType.Json), label: t.system.valueJson }
  ];
}

function valueTypeLabel(t: Dictionary, type: number): string {
  return valueTypeOptions(t).find((option) => Number(option.value) === type)?.label ?? t.system.valueText;
}
