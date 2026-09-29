import { useEffect, useState, type FormEvent } from 'react';
import { Plus, Trash2 } from 'lucide-react';

import {
  WorkflowEntityType,
  WorkflowStatus,
  type WorkflowDto,
  type SaveWorkflowRequest
} from '@/api/workflows';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useCreateWorkflow, useUpdateWorkflow } from '@/api/workflowsHooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import type { Dictionary } from '@/i18n/types';

interface WorkflowDialogProps {
  open: boolean;
  onClose: () => void;
  /** شناسه برای ویرایش؛ null یعنی ایجاد. */
  workflowId?: string | null;
  /** در حالت ویرایش، داده‌ی فعلی تعریف. */
  existing?: WorkflowDto | null;
}

/**
 * دیالوگ ایجاد/ویرایش تعریف گردش کار: نام/کد، نوع موجودیت و ویرایشگر وضعیت‌ها
 * و گذارها. ساختار ماشین وضعیت در سمت سرور اعتبارسنجی می‌شود.
 */
export function WorkflowDialog({ open, onClose, workflowId, existing }: WorkflowDialogProps) {
  const { t } = useLanguage();
  const isEdit = !!workflowId;

  const createMutation = useCreateWorkflow();
  const updateMutation = useUpdateWorkflow();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  const [form, setForm] = useState<WorkflowFormState>(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

  function updateField<K extends keyof WorkflowFormState>(field: K, value: WorkflowFormState[K]) {
    setForm((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => {
      if (!(field in previous)) return previous;
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  function addState() {
    setForm((previous) => ({
      ...previous,
      states: [
        ...previous.states,
        { code: '', name: '', isInitial: false, isFinal: false, displayOrder: previous.states.length }
      ]
    }));
  }

  function updateState(index: number, patch: Partial<WorkflowStateForm>) {
    setForm((previous) => ({
      ...previous,
      states: previous.states.map((state, i) => (i === index ? { ...state, ...patch } : state))
    }));
  }

  function removeState(index: number) {
    const removedCode = form.states[index]?.code;
    setForm((previous) => ({
      ...previous,
      states: previous.states.filter((_, i) => i !== index),
      transitions: previous.transitions.filter(
        (tr) => tr.fromStateCode !== removedCode && tr.toStateCode !== removedCode
      )
    }));
  }

  function addTransition() {
    setForm((previous) => ({
      ...previous,
      transitions: [
        ...previous.transitions,
        {
          code: '',
          name: '',
          fromStateCode: previous.states[0]?.code ?? '',
          toStateCode: previous.states[0]?.code ?? '',
          requiresApproval: false,
          approverPermission: null,
          displayOrder: previous.transitions.length
        }
      ]
    }));
  }

  function updateTransition(index: number, patch: Partial<WorkflowTransitionForm>) {
    setForm((previous) => ({
      ...previous,
      transitions: previous.transitions.map((tr, i) => (i === index ? { ...tr, ...patch } : tr))
    }));
  }

  function removeTransition(index: number) {
    setForm((previous) => ({
      ...previous,
      transitions: previous.transitions.filter((_, i) => i !== index)
    }));
  }

  function validate(): boolean {
    const next: Record<string, string> = {};

    if (!form.name.trim()) next.name = t.workflows.workflowName + ' ' + t.common.required;
    if (!form.code.trim()) next.code = t.workflows.code + ' ' + t.common.required;
    if (form.states.length === 0) next.states = t.workflows.initialStateRequired;
    if (form.states.length > 0 && !form.states.some((state) => state.isInitial)) {
      next.states = t.workflows.initialStateRequired;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const request: SaveWorkflowRequest = {
      name: form.name.trim(),
      code: form.code.trim(),
      description: form.description.trim() || null,
      entityType: form.entityType,
      states: form.states.map((state) => ({
        code: state.code.trim(),
        name: state.name.trim() || state.code.trim(),
        isInitial: state.isInitial,
        isFinal: state.isFinal,
        displayOrder: state.displayOrder
      })),
      transitions: form.transitions.length === 0 ? null : form.transitions.map((tr) => ({
        code: tr.code.trim(),
        name: tr.name.trim() || tr.code.trim(),
        fromStateCode: tr.fromStateCode,
        toStateCode: tr.toStateCode,
        requiresApproval: tr.requiresApproval,
        approverPermission: tr.requiresApproval ? (tr.approverPermission?.trim() || null) : null,
        displayOrder: tr.displayOrder
      })),
      activateImmediately: form.activateImmediately
    };

    try {
      if (isEdit && workflowId) {
        await updateMutation.mutateAsync({ id: workflowId, request });
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

  const stateOptions = form.states.map((state) => ({
    value: state.code,
    label: state.name || state.code || `#${state.displayOrder}`
  }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.workflows.editWorkflow : t.workflows.newWorkflow}
      description={t.workflows.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="workflow-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="workflow-form" onSubmit={handleSubmit} className="flex flex-col gap-6" noValidate>
        {formError && (
          <div
            className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label={t.workflows.workflowName} htmlFor="workflowName" required error={errors.name}>
            <Input
              id="workflowName"
              value={form.name}
              onChange={(event) => updateField('name', event.target.value)}
              disabled={isSaving}
            />
          </FormField>

          <FormField label={t.workflows.code} htmlFor="workflowCode" required error={errors.code}>
            <Input
              id="workflowCode"
              value={form.code}
              onChange={(event) => updateField('code', event.target.value)}
              disabled={isSaving || (isEdit && existing?.status !== WorkflowStatus.Draft)}
              dir="ltr"
            />
          </FormField>

          <FormField label={t.workflows.workflowDescription} htmlFor="workflowDescription" className="sm:col-span-2">
            <Input
              id="workflowDescription"
              value={form.description}
              onChange={(event) => updateField('description', event.target.value)}
              disabled={isSaving}
            />
          </FormField>

          <FormField label={t.workflows.entityType} htmlFor="workflowEntityType">
            <Select
              id="workflowEntityType"
              value={String(form.entityType)}
              onChange={(event) => updateField('entityType', Number(event.target.value) as WorkflowEntityType)}
              options={entityTypeOptions(t)}
              disabled={isSaving}
            />
          </FormField>
        </div>

        {/* وضعیت‌ها */}
        <section className="flex flex-col gap-3 rounded-lg border p-4">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-sm font-medium">{t.workflows.states}</h3>
            <Button type="button" variant="outline" size="sm" onClick={addState} disabled={isSaving}>
              <Plus className="size-4" />
              {t.workflows.addState}
            </Button>
          </div>

          {errors.states && <p className="text-xs text-destructive" role="alert">{errors.states}</p>}

          {form.states.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t.workflows.initialStateRequired}</p>
          ) : (
            <div className="flex flex-col gap-2">
              {form.states.map((state, index) => (
                <div key={index} className="grid grid-cols-1 items-end gap-2 sm:grid-cols-[1fr_1fr_auto_auto_auto]">
                  <FormField label={t.workflows.stateCode} htmlFor={`state-code-${index}`}>
                    <Input
                      id={`state-code-${index}`}
                      value={state.code}
                      onChange={(event) => updateState(index, { code: event.target.value })}
                      disabled={isSaving}
                      dir="ltr"
                    />
                  </FormField>

                  <FormField label={t.workflows.stateName} htmlFor={`state-name-${index}`}>
                    <Input
                      id={`state-name-${index}`}
                      value={state.name}
                      onChange={(event) => updateState(index, { name: event.target.value })}
                      disabled={isSaving}
                    />
                  </FormField>

                  <label className="flex items-center gap-1.5 pb-2.5 text-sm">
                    <Checkbox
                      checked={state.isInitial}
                      onCheckedChange={(checked) => updateState(index, { isInitial: checked })}
                      disabled={isSaving}
                    />
                    {t.workflows.isInitial}
                  </label>

                  <label className="flex items-center gap-1.5 pb-2.5 text-sm">
                    <Checkbox
                      checked={state.isFinal}
                      onCheckedChange={(checked) => updateState(index, { isFinal: checked })}
                      disabled={isSaving}
                    />
                    {t.workflows.isFinal}
                  </label>

                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="text-destructive"
                    onClick={() => removeState(index)}
                    disabled={isSaving}
                    aria-label={t.workflows.removeState}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* گذارها */}
        <section className="flex flex-col gap-3 rounded-lg border p-4">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-sm font-medium">{t.workflows.transitions}</h3>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={addTransition}
              disabled={isSaving || form.states.length < 2}
            >
              <Plus className="size-4" />
              {t.workflows.addTransition}
            </Button>
          </div>

          {form.transitions.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t.workflows.transitions}</p>
          ) : (
            <div className="flex flex-col gap-2">
              {form.transitions.map((tr, index) => (
                <div key={index} className="flex flex-col gap-2 rounded-md border p-3">
                  <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                    <FormField label={t.workflows.transitionCode} htmlFor={`tr-code-${index}`}>
                      <Input
                        id={`tr-code-${index}`}
                        value={tr.code}
                        onChange={(event) => updateTransition(index, { code: event.target.value })}
                        disabled={isSaving}
                        dir="ltr"
                      />
                    </FormField>

                    <FormField label={t.workflows.transitionName} htmlFor={`tr-name-${index}`}>
                      <Input
                        id={`tr-name-${index}`}
                        value={tr.name}
                        onChange={(event) => updateTransition(index, { name: event.target.value })}
                        disabled={isSaving}
                      />
                    </FormField>

                    <FormField label={t.workflows.fromState} htmlFor={`tr-from-${index}`}>
                      <Select
                        id={`tr-from-${index}`}
                        value={tr.fromStateCode}
                        onChange={(event) => updateTransition(index, { fromStateCode: event.target.value })}
                        options={stateOptions}
                        disabled={isSaving}
                      />
                    </FormField>

                    <FormField label={t.workflows.toState} htmlFor={`tr-to-${index}`}>
                      <Select
                        id={`tr-to-${index}`}
                        value={tr.toStateCode}
                        onChange={(event) => updateTransition(index, { toStateCode: event.target.value })}
                        options={stateOptions}
                        disabled={isSaving}
                      />
                    </FormField>

                    {tr.requiresApproval && (
                      <FormField label={t.workflows.approverPermission} htmlFor={`tr-perm-${index}`} className="sm:col-span-2">
                        <Input
                          id={`tr-perm-${index}`}
                          value={tr.approverPermission ?? ''}
                          onChange={(event) =>
                            updateTransition(index, { approverPermission: event.target.value || null })
                          }
                          disabled={isSaving}
                          dir="ltr"
                        />
                      </FormField>
                    )}
                  </div>

                  <div className="flex items-center justify-between gap-2">
                    <label className="flex items-center gap-1.5 text-sm">
                      <Checkbox
                        checked={tr.requiresApproval}
                        onCheckedChange={(checked) => updateTransition(index, { requiresApproval: checked })}
                        disabled={isSaving}
                      />
                      {t.workflows.requiresApproval}
                    </label>

                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className="text-destructive"
                      onClick={() => removeTransition(index)}
                      disabled={isSaving}
                      aria-label={t.workflows.removeTransition}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>

        <label className="flex cursor-pointer items-center gap-3 rounded-md border p-3">
          <Checkbox
            checked={form.activateImmediately}
            onCheckedChange={(checked) => updateField('activateImmediately', checked)}
            disabled={isSaving}
          />
          <span className="flex flex-col gap-0.5">
            <span className="text-sm font-medium">{t.workflows.activateImmediately}</span>
            <span className="text-xs text-muted-foreground">{t.workflows.activateImmediatelyHint}</span>
          </span>
        </label>
      </form>
    </Dialog>
  );
}

interface WorkflowStateForm {
  code: string;
  name: string;
  isInitial: boolean;
  isFinal: boolean;
  displayOrder: number;
}

interface WorkflowTransitionForm {
  code: string;
  name: string;
  fromStateCode: string;
  toStateCode: string;
  requiresApproval: boolean;
  approverPermission: string | null;
  displayOrder: number;
}

interface WorkflowFormState {
  name: string;
  code: string;
  description: string;
  entityType: WorkflowEntityType;
  states: WorkflowStateForm[];
  transitions: WorkflowTransitionForm[];
  activateImmediately: boolean;
}

function createEmptyForm(): WorkflowFormState {
  return {
    name: '',
    code: '',
    description: '',
    entityType: WorkflowEntityType.Custom,
    states: [
      { code: 'draft', name: '', isInitial: true, isFinal: false, displayOrder: 0 },
      { code: 'active', name: '', isInitial: false, isFinal: false, displayOrder: 1 },
      { code: 'closed', name: '', isInitial: false, isFinal: true, displayOrder: 2 }
    ],
    transitions: [],
    activateImmediately: false
  };
}

function toForm(existing: WorkflowDto): WorkflowFormState {
  return {
    name: existing.name,
    code: existing.code,
    description: existing.description ?? '',
    entityType: existing.entityType,
    states: existing.states
      .slice()
      .sort((a, b) => a.displayOrder - b.displayOrder)
      .map((state) => ({
        code: state.code,
        name: state.name,
        isInitial: state.isInitial,
        isFinal: state.isFinal,
        displayOrder: state.displayOrder
      })),
    transitions: existing.transitions
      .slice()
      .sort((a, b) => a.displayOrder - b.displayOrder)
      .map((tr): WorkflowTransitionForm => ({
        code: tr.code,
        name: tr.name,
        fromStateCode: tr.fromStateCode,
        toStateCode: tr.toStateCode,
        requiresApproval: tr.requiresApproval,
        approverPermission: tr.approverPermission ?? null,
        displayOrder: tr.displayOrder
      })),
    // در زمان ویرایش، پرچم بر اساس وضعیت فعلی پر می‌شود.
    activateImmediately: existing.status === WorkflowStatus.Active
  };
}

export function entityTypeOptions(t: Dictionary) {
  return [
    { value: String(WorkflowEntityType.Survey), label: t.workflows.entitySurvey },
    { value: String(WorkflowEntityType.Campaign), label: t.workflows.entityCampaign },
    { value: String(WorkflowEntityType.ActionPlan), label: t.workflows.entityActionPlan },
    { value: String(WorkflowEntityType.ReportDefinition), label: t.workflows.entityReportDefinition },
    { value: String(WorkflowEntityType.Custom), label: t.workflows.entityCustom }
  ];
}

export function workflowStatusOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(WorkflowStatus.Draft), label: t.workflows.statusDraft },
    { value: String(WorkflowStatus.Active), label: t.workflows.statusActive },
    { value: String(WorkflowStatus.Archived), label: t.workflows.statusArchived }
  ];
}
