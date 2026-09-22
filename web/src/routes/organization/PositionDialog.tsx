import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';

import { positionsApi, type SavePositionRequest } from '@/api/organization';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { queryKeys, useOrgUnits, usePositions } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';

interface PositionDialogProps {
  open: boolean;
  onClose: () => void;
  positionId?: string | null;
}

/**
 * دیالوگ ایجاد/ویرایش موقعیت شغلی.
 */
export function PositionDialog({ open, onClose, positionId }: PositionDialogProps) {
  const { t, culture } = useLanguage();
  const isEdit = !!positionId;

  const { data: orgUnits } = useOrgUnits();
  const { data: positions } = usePositions();
  const queryClient = useQueryClient();
  const [isSaving, setIsSaving] = useState(false);

  const existingPosition = useMemo(
    () => (positionId ? positions?.find((position) => position.id === positionId) ?? null : null),
    [positionId, positions]
  );

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(
      existingPosition
        ? {
            code: existingPosition.code,
            title: existingPosition.title,
            orgUnitId: existingPosition.orgUnitId,
            reportsToPositionId: existingPosition.reportsToPositionId ?? '',
            grade: existingPosition.grade?.toString() ?? '',
            isActive: existingPosition.isActive,
            description: existingPosition.description ?? ''
          }
        : createEmptyForm()
    );
    setErrors({});
    setFormError(undefined);
  }, [open, existingPosition]);

  function updateField(field: string, value: string | boolean) {
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

    if (!form.code.trim()) next.code = t.positions.code + ' ' + t.common.required;
    if (!form.title.trim()) next.title = t.positions.title_ + ' ' + t.common.required;
    if (!form.orgUnitId) next.orgUnitId = t.positions.orgUnit + ' ' + t.common.required;

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);
    setIsSaving(true);

    const request: SavePositionRequest = {
      code: form.code.trim(),
      title: form.title.trim(),
      orgUnitId: form.orgUnitId,
      reportsToPositionId: form.reportsToPositionId || null,
      grade: form.grade ? Number(form.grade) : null,
      isActive: form.isActive,
      description: form.description.trim() || null
    };

    try {
      if (isEdit && positionId) {
        await positionsApi.update(culture, positionId, request);
      } else {
        await positionsApi.create(culture, request);
      }

      void queryClient.invalidateQueries({ queryKey: queryKeys.positions });
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
    } finally {
      setIsSaving(false);
    }
  }

  const orgUnitOptions = (orgUnits ?? []).map((unit) => ({
    value: unit.id,
    label: `${unit.name} (${unit.code})`
  }));

  const reportsToOptions = (positions ?? [])
    .filter((position) => position.id !== positionId)
    .map((position) => ({
      value: position.id,
      label: `${position.title} (${position.code})`
    }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.positions.editPosition : t.positions.newPosition}
      description={t.positions.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="position-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form
        id="position-form"
        onSubmit={handleSubmit}
        className="grid gap-4 sm:grid-cols-2"
        noValidate
      >
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.positions.code} htmlFor="positionCode" required error={errors.code}>
          <Input
            id="positionCode"
            value={form.code}
            onChange={(event) => updateField('code', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.positions.title_} htmlFor="positionTitle" required error={errors.title}>
          <Input
            id="positionTitle"
            value={form.title}
            onChange={(event) => updateField('title', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.positions.orgUnit} htmlFor="positionOrgUnit" required error={errors.orgUnitId}>
          <Select
            id="positionOrgUnit"
            value={form.orgUnitId}
            onChange={(event) => updateField('orgUnitId', event.target.value)}
            options={orgUnitOptions}
            placeholder={t.positions.selectOrgUnit}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.positions.reportsTo} htmlFor="positionReportsTo">
          <Select
            id="positionReportsTo"
            value={form.reportsToPositionId}
            onChange={(event) => updateField('reportsToPositionId', event.target.value)}
            options={reportsToOptions}
            placeholder={t.positions.selectReportsTo}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.positions.grade} htmlFor="positionGrade" error={errors.grade}>
          <Input
            id="positionGrade"
            type="number"
            min="0"
            value={form.grade}
            onChange={(event) => updateField('grade', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <label className="flex cursor-pointer items-center gap-3 rounded-md border p-3">
          <Checkbox
            checked={form.isActive}
            onCheckedChange={(checked) => updateField('isActive', checked)}
            disabled={isSaving}
          />
          <span className="text-sm font-medium">{t.common.active}</span>
        </label>

        <FormField
          label={t.positions.descriptionLabel}
          htmlFor="positionDescription"
          className="col-span-full"
        >
          <Input
            id="positionDescription"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            disabled={isSaving}
          />
        </FormField>
      </form>
    </Dialog>
  );
}

interface PositionFormState {
  code: string;
  title: string;
  orgUnitId: string;
  reportsToPositionId: string;
  grade: string;
  isActive: boolean;
  description: string;
}

function createEmptyForm(): PositionFormState {
  return {
    code: '',
    title: '',
    orgUnitId: '',
    reportsToPositionId: '',
    grade: '',
    isActive: true,
    description: ''
  };
}
