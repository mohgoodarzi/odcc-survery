import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';

import { orgUnitsApi, OrgUnitType, type SaveOrgUnitRequest } from '@/api/organization';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { queryKeys, useCreateOrgUnit, useOrgUnits, useUpdateOrgUnit } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';

interface OrgUnitDialogProps {
  open: boolean;
  onClose: () => void;
  unitId?: string | null;
}

/**
 * دیالوگ ایجاد/ویرایش واحد سازمانی. والد می‌تواند انتخاب شود و
 * مسیر به‌صورت خودکار توسط سرور محاسبه می‌شود.
 */
export function OrgUnitDialog({ open, onClose, unitId }: OrgUnitDialogProps) {
  const { t, culture } = useLanguage();
  const queryClient = useQueryClient();
  const isEdit = !!unitId;

  const { data: units } = useOrgUnits();
  const createMutation = useCreateOrgUnit();
  const updateMutation = useUpdateOrgUnit();

  const existingUnit = useMemo(
    () => (unitId ? units?.find((unit) => unit.id === unitId) ?? null : null),
    [unitId, units]
  );

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(
      existingUnit
        ? {
            code: existingUnit.code,
            name: existingUnit.name,
            type: String(existingUnit.type),
            parentId: existingUnit.parentId ?? '',
            isActive: existingUnit.isActive
          }
        : createEmptyForm()
    );
    setErrors({});
    setFormError(undefined);
  }, [open, existingUnit]);

  const mutation = isEdit ? updateMutation : createMutation;

  function updateField<K extends keyof OrgUnitFormState>(field: K, value: OrgUnitFormState[K]) {
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

    if (!form.code.trim()) next.code = t.orgUnits.code + ' ' + t.common.required;
    if (!form.name.trim()) next.name = t.orgUnits.name + ' ' + t.common.required;

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const request: SaveOrgUnitRequest = {
      code: form.code.trim(),
      name: form.name.trim(),
      type: Number(form.type) as OrgUnitType,
      parentId: form.parentId || null,
      isActive: form.isActive
    };

    try {
      if (isEdit && unitId) {
        await orgUnitsApi.update(culture, unitId, request);
      } else {
        await orgUnitsApi.create(culture, request);
      }

      void queryClient.invalidateQueries({ queryKey: queryKeys.orgUnits });
      void queryClient.invalidateQueries({ queryKey: queryKeys.orgUnitTree });
      onClose();
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.status === 400 && Object.keys(error.validationErrors).length > 0) {
          const translated: Record<string, string> = {};
          for (const [field, messages] of Object.entries(error.validationErrors)) {
            translated[field] = messages[0] ?? t.errors.validation;
          }
          setErrors(translated);
        } else {
          setFormError(t.errors.fromCode(error.code));
        }
      } else {
        setFormError(t.errors.generic);
      }
    }
  }

  // واحدهای والد ممکن: همه‌ی واحدها به جز خود واحد و زیردرختش.
  const parentOptions = (units ?? [])
    .filter((unit) => unit.id !== unitId && !isDescendantOf(unit, existingUnit?.path))
    .map((unit) => ({
      value: unit.id,
      label: `${unit.name} (${unit.code})`
    }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.orgUnits.editUnit : t.orgUnits.newUnit}
      description={t.orgUnits.description}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="org-unit-form" disabled={mutation.isPending}>
            {mutation.isPending ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="org-unit-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.orgUnits.code} htmlFor="unitCode" required error={errors.code}>
          <Input
            id="unitCode"
            value={form.code}
            onChange={(event) => updateField('code', event.target.value)}
            disabled={mutation.isPending}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.orgUnits.name} htmlFor="unitName" required error={errors.name}>
          <Input
            id="unitName"
            value={form.name}
            onChange={(event) => updateField('name', event.target.value)}
            disabled={mutation.isPending}
          />
        </FormField>

        <FormField label={t.orgUnits.type} htmlFor="unitType">
          <Select
            id="unitType"
            value={form.type}
            onChange={(event) => updateField('type', event.target.value)}
            options={[
              { value: String(OrgUnitType.Company), label: t.orgUnits.company },
              { value: String(OrgUnitType.Division), label: t.orgUnits.division },
              { value: String(OrgUnitType.Department), label: t.orgUnits.department },
              { value: String(OrgUnitType.Team), label: t.orgUnits.team },
              { value: String(OrgUnitType.Unit), label: t.orgUnits.unit }
            ]}
            disabled={mutation.isPending}
          />
        </FormField>

        <FormField label={t.orgUnits.parent} htmlFor="unitParent">
          <Select
            id="unitParent"
            value={form.parentId}
            onChange={(event) => updateField('parentId', event.target.value)}
            options={parentOptions}
            placeholder={t.orgUnits.selectParent}
            disabled={mutation.isPending}
          />
        </FormField>

        <label className="col-span-full flex cursor-pointer items-center gap-3 rounded-md border p-3">
          <Checkbox
            checked={form.isActive}
            onCheckedChange={(checked) => updateField('isActive', checked)}
            disabled={mutation.isPending}
          />
          <span className="text-sm font-medium">{t.common.active}</span>
        </label>
      </form>
    </Dialog>
  );
}

interface OrgUnitFormState {
  code: string;
  name: string;
  type: string;
  parentId: string;
  isActive: boolean;
}

function createEmptyForm(): OrgUnitFormState {
  return {
    code: '',
    name: '',
    type: String(OrgUnitType.Division),
    parentId: '',
    isActive: true
  };
}

/**
 * آیا unit در زیردرکت unit با مسیر داده‌شده قرار دارد؟
 * برای جلوگیری از ایجاد چرخه در انتخاب والد.
 */
function isDescendantOf(
  unit: { path: string },
  ancestorPath: string | undefined
): boolean {
  if (!ancestorPath) return false;

  return (
    unit.path === ancestorPath ||
    unit.path.startsWith(ancestorPath.endsWith('/') ? ancestorPath : ancestorPath + '/')
  );
}
