import { useEffect, useState, type FormEvent } from 'react';

import {
  ActionPriority,
  type ActionItemDto,
  type SaveActionItemRequest
} from '@/api/actions';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useUsersSearch } from '@/api/hooks';
import { useCreateActionItem, useUpdateActionItem } from '@/api/actionsHooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { priorityOptions } from './ActionPlanDialog';

interface ActionItemDialogProps {
  open: boolean;
  onClose: () => void;
  /** شناسه‌ی برنامه‌ی مالک (الزامی برای ایجاد). */
  planId: string;
  /** در حالت ویرایش، داده‌ی فعلی آیتم. */
  existing?: ActionItemDto | null;
}

/**
 * دیالوگ ایجاد/ویرایش آیتم اقدام: عنوان، شرح، مسئول، اولویت، ترتیب نمایش،
 * مهلت نهایی و زمان یادآوری.
 */
export function ActionItemDialog({ open, onClose, planId, existing }: ActionItemDialogProps) {
  const { t } = useLanguage();
  const isEdit = !!existing;

  const { data: users } = useUsersSearch({ searchText: null, isActive: true, page: 1 });

  const createMutation = useCreateActionItem();
  const updateMutation = useUpdateActionItem();
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

  function updateField(field: keyof ItemFormState, value: unknown) {
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

    if (!form.title.trim()) next.title = t.actions.itemTitle + ' ' + t.common.required;

    if (form.dueDate && form.remindAt && new Date(form.remindAt) > new Date(form.dueDate)) {
      next.remindAt = t.actions.remindAt + ' ≤ ' + t.actions.dueDate;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const request: SaveActionItemRequest = {
      title: form.title.trim(),
      description: form.description.trim() || null,
      assigneeUserId: form.assigneeUserId || null,
      priority: form.priority,
      displayOrder: form.displayOrder,
      dueDate: toIso(form.dueDate),
      remindAt: toIso(form.remindAt)
    };

    try {
      if (isEdit && existing) {
        await updateMutation.mutateAsync({ id: existing.id, request });
      } else {
        await createMutation.mutateAsync({ planId, request });
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

  const userOptions = (users?.items ?? []).map((user) => ({
    value: user.id,
    label: user.displayName
  }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.actions.editItem : t.actions.newItem}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="action-item-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="action-item-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.actions.itemTitle} htmlFor="itemTitle" required error={errors.title} className="sm:col-span-2">
          <Input
            id="itemTitle"
            value={form.title}
            onChange={(event) => updateField('title', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.itemDescription} htmlFor="itemDescription" className="sm:col-span-2">
          <Input
            id="itemDescription"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.assignee} htmlFor="itemAssignee">
          <Select
            id="itemAssignee"
            value={form.assigneeUserId}
            onChange={(event) => updateField('assigneeUserId', event.target.value)}
            options={userOptions}
            placeholder={t.actions.selectAssignee}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.priority} htmlFor="itemPriority" required>
          <Select
            id="itemPriority"
            value={String(form.priority)}
            onChange={(event) => updateField('priority', Number(event.target.value))}
            options={priorityOptions(t.actions)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.actions.displayOrder} htmlFor="itemOrder">
          <Input
            id="itemOrder"
            type="number"
            min={0}
            value={String(form.displayOrder)}
            onChange={(event) => updateField('displayOrder', Number(event.target.value))}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.actions.dueDate} htmlFor="itemDueDate">
          <Input
            id="itemDueDate"
            type="datetime-local"
            value={form.dueDate}
            onChange={(event) => updateField('dueDate', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.actions.remindAt} htmlFor="itemRemindAt" error={errors.remindAt}>
          <Input
            id="itemRemindAt"
            type="datetime-local"
            value={form.remindAt}
            onChange={(event) => updateField('remindAt', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>
      </form>
    </Dialog>
  );
}

interface ItemFormState {
  title: string;
  description: string;
  assigneeUserId: string;
  priority: ActionPriority;
  displayOrder: number;
  dueDate: string;
  remindAt: string;
}

function createEmptyForm(): ItemFormState {
  return {
    title: '',
    description: '',
    assigneeUserId: '',
    priority: ActionPriority.Medium,
    displayOrder: 0,
    dueDate: '',
    remindAt: ''
  };
}

function toForm(existing: ActionItemDto): ItemFormState {
  return {
    title: existing.title,
    description: existing.description ?? '',
    assigneeUserId: existing.assigneeUserId ?? '',
    priority: existing.priority,
    displayOrder: existing.displayOrder,
    dueDate: toLocalInputValue(existing.dueDate),
    remindAt: toLocalInputValue(existing.remindAt)
  };
}

function toIso(localDateTime: string): string | null {
  if (!localDateTime) return null;
  const parsed = new Date(localDateTime);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

function toLocalInputValue(iso: string | null | undefined): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';

  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
