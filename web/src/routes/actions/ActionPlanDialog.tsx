import { useEffect, useState, type FormEvent } from 'react';

import {
  ActionPriority,
  type ActionPlanDto,
  type SaveActionPlanRequest
} from '@/api/actions';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useCampaignableSurveys, useOrgUnits, useUsersSearch } from '@/api/hooks';
import { useCreateActionPlan, useUpdateActionPlan } from '@/api/actionsHooks';
import { Dialog } from '@/components/ui/dialog';
import { DateTimePicker } from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import { isoToLocalDateTime, localDateTimeToIso } from '@/lib/datetime';

interface ActionPlanDialogProps {
  open: boolean;
  onClose: () => void;
  /** شناسه‌ی برنامه برای ویرایش؛ null یعنی ایجاد. */
  planId?: string | null;
  /** در حالت ویرایش، داده‌ی فعلی برنامه. */
  existing?: ActionPlanDto | null;
}

/**
 * دیالوگ ایجاد/ویرایش برنامه‌ی اقدام: عنوان، توضیح، نظرسنجی مرتبط، واحد سازمانی،
 * مالک، اولویت و مهلت نهایی.
 */
export function ActionPlanDialog({ open, onClose, planId, existing }: ActionPlanDialogProps) {
  const { t } = useLanguage();
  const isEdit = !!planId;

  const { data: surveys } = useCampaignableSurveys();
  const { data: orgUnits } = useOrgUnits();
  const { data: users } = useUsersSearch({ searchText: null, isActive: true, page: 1 });

  const createMutation = useCreateActionPlan();
  const updateMutation = useUpdateActionPlan();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

  function updateField(field: keyof PlanFormState, value: unknown) {
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

    if (!form.title.trim()) next.title = t.actions.planName + ' ' + t.common.required;

    if (form.dueDate && new Date(form.dueDate) < new Date(Date.now() - 60 * 60 * 1000)) {
      next.dueDate = t.actions.dueDate + ' ≥ ' + t.common.createdAt;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const request: SaveActionPlanRequest = {
      title: form.title.trim(),
      description: form.description.trim() || null,
      surveyId: form.surveyId || null,
      campaignId: null,
      orgUnitId: form.orgUnitId || null,
      ownerUserId: form.ownerUserId || null,
      priority: form.priority,
      dueDate: localDateTimeToIso(form.dueDate),
      activateImmediately: form.activateImmediately
    };

    try {
      if (isEdit && planId) {
        await updateMutation.mutateAsync({ id: planId, request });
      } else {
        await createMutation.mutateAsync(request);
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

  const surveyOptions = (surveys ?? []).map((survey) => ({
    value: survey.id,
    label: `${survey.title} (${survey.code})`
  }));

  const orgUnitOptions = (orgUnits ?? []).map((unit) => ({
    value: unit.id,
    label: `${unit.name} (${unit.code})`
  }));

  const userOptions = (users?.items ?? []).map((user) => ({
    value: user.id,
    label: user.displayName
  }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.actions.editPlan : t.actions.newPlan}
      description={t.actions.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="action-plan-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="action-plan-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.actions.planName} htmlFor="planTitle" required error={errors.title} className="sm:col-span-2">
          <Input
            id="planTitle"
            value={form.title}
            onChange={(event) => updateField('title', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.planDescription} htmlFor="planDescription" className="sm:col-span-2">
          <Input
            id="planDescription"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.survey} htmlFor="planSurvey">
          <Select
            id="planSurvey"
            value={form.surveyId}
            onChange={(event) => updateField('surveyId', event.target.value)}
            options={surveyOptions}
            placeholder={t.actions.selectSurvey}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.orgUnit} htmlFor="planOrgUnit">
          <Select
            id="planOrgUnit"
            value={form.orgUnitId}
            onChange={(event) => updateField('orgUnitId', event.target.value)}
            options={orgUnitOptions}
            placeholder={t.actions.selectOrgUnit}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.owner} htmlFor="planOwner">
          <Select
            id="planOwner"
            value={form.ownerUserId}
            onChange={(event) => updateField('ownerUserId', event.target.value)}
            options={userOptions}
            placeholder={t.actions.selectOwner}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.priority} htmlFor="planPriority" required>
          <Select
            id="planPriority"
            value={String(form.priority)}
            onChange={(event) => updateField('priority', Number(event.target.value))}
            options={priorityOptions(t.actions)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.dueDate} htmlFor="planDueDate" error={errors.dueDate}>
          <DateTimePicker
            id="planDueDate"
            value={form.dueDate}
            onChange={(value) => updateField('dueDate', value)}
            disabled={isSaving}
          />
        </FormField>

        <label className="col-span-full flex cursor-pointer items-center gap-3 rounded-md border p-3">
          <Checkbox
            checked={form.activateImmediately}
            onCheckedChange={(checked) => updateField('activateImmediately', checked)}
            disabled={isSaving}
          />
          <span className="flex flex-col gap-0.5">
            <span className="text-sm font-medium">{t.actions.activateImmediately}</span>
            <span className="text-xs text-muted-foreground">{t.actions.activateImmediatelyHint}</span>
          </span>
        </label>

        <p className="col-span-full text-xs text-muted-foreground">{t.actions.privacyNote}</p>
      </form>
    </Dialog>
  );
}

interface PlanFormState {
  title: string;
  description: string;
  surveyId: string;
  orgUnitId: string;
  ownerUserId: string;
  priority: ActionPriority;
  dueDate: string;
  activateImmediately: boolean;
}

function createEmptyForm(): PlanFormState {
  return {
    title: '',
    description: '',
    surveyId: '',
    orgUnitId: '',
    ownerUserId: '',
    priority: ActionPriority.Medium,
    dueDate: '',
    activateImmediately: false
  };
}

function toForm(existing: ActionPlanDto): PlanFormState {
  return {
    title: existing.title,
    description: existing.description ?? '',
    surveyId: existing.surveyId ?? '',
    orgUnitId: existing.orgUnitId ?? '',
    ownerUserId: existing.ownerUserId ?? '',
    priority: existing.priority,
    dueDate: isoToLocalDateTime(existing.dueDate),
    // پرچم فعال‌سازی بر اساس وضعیت فعلی پر می‌شود تا ویرایش، برنامه‌ی فعال را
    // به پیش‌نویس برگرداند یا برعکس.
    activateImmediately: existing.status === 1
  };
}

export function priorityOptions(t: import('@/i18n/types').Dictionary['actions']) {
  return [
    { value: String(ActionPriority.Low), label: t.priorityLow },
    { value: String(ActionPriority.Medium), label: t.priorityMedium },
    { value: String(ActionPriority.High), label: t.priorityHigh },
    { value: String(ActionPriority.Critical), label: t.priorityCritical }
  ];
}
