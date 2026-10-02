import { useEffect, useState, type FormEvent } from 'react';
import { Plus, Trash2 } from 'lucide-react';

import {
  Language,
  QuestionType,
  type QuestionLocalization,
  type QuestionOptionLocalization,
  type SaveQuestionOptionRequest,
  type SaveQuestionRequest
} from '@/api/questionBank';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useCreateQuestion } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { questionTypeLabel, typeHasOptions } from './questionType';

interface QuestionDialogProps {
  open: boolean;
  onClose: () => void;
  /** پس از ایجاد موفق، شناسه‌ی سؤال جدید به فراخوان داده می‌شود. */
  onCreated: (questionId: string) => void;
}

/**
 * دیالوگ ایجاد سؤال جدید در کتابخانه‌ی سؤالات — در دسترس از داخل ویرایشگر
 * پرسشنامه تا مدیر بتواند سؤال و گزینه‌های پاسخ آن را تعریف کند.
 *
 * سؤال‌ها در کتابخانه‌ی سؤالات (ماژول موجود) ذخیره می‌شوند تا در همه‌ی
 * پرسشنامه‌ها قابل استفاده‌ی مجدد باشند — مدلی جدیدی ساخته نمی‌شود.
 */
export function QuestionDialog({ open, onClose, onCreated }: QuestionDialogProps) {
  const { t } = useLanguage();

  const createMutation = useCreateQuestion();
  const isSaving = createMutation.isPending;

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open]);

  function updateField(field: string, value: string | number) {
    setForm((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => {
      if (!(field in previous)) return previous;
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  function addOption() {
    setForm((previous) => ({
      ...previous,
      options: [...previous.options, { key: createKey(), code: '', text: '' }]
    }));
  }

  function updateOption(optionKey: string, patch: Partial<OptionForm>) {
    setForm((previous) => ({
      ...previous,
      options: previous.options.map((option) =>
        option.key === optionKey ? { ...option, ...patch } : option
      )
    }));
    setErrors((previous) => {
      const next = { ...previous };
      delete next[`option-code-${optionKey}`];
      delete next[`option-text-${optionKey}`];
      return next;
    });
  }

  function removeOption(optionKey: string) {
    setForm((previous) => ({
      ...previous,
      options: previous.options.filter((option) => option.key !== optionKey)
    }));
  }

  const needsOptions = typeHasOptions(form.type);

  function validate(): boolean {
    const next: Record<string, string> = {};

    if (!form.code.trim()) {
      next.code = `${t.questionnaires.questionCode} ${t.common.required}`;
    } else if (!/^[A-Za-z0-9\-_.]+$/.test(form.code.trim())) {
      next.code = t.questionnaires.questionCode + ' — A-Z a-z 0-9 - _ .';
    }

    if (!form.text.trim()) {
      next.text = `${t.questionnaires.questionText} ${t.common.required}`;
    }

    if (form.type === QuestionType.Rating) {
      const scale = Number(form.scaleMax);
      if (!Number.isFinite(scale) || scale < 2 || scale > 20) {
        next.scaleMax = t.questionnaires.scaleMaxHint;
      }
    }

    if (needsOptions) {
      if (form.options.length < 2) {
        next.options = t.questionnaires.addOption;
      }
      form.options.forEach((option) => {
        if (!option.code.trim()) {
          next[`option-code-${option.key}`] = `${t.questionnaires.optionCode} ${t.common.required}`;
        }
        if (!option.text.trim()) {
          next[`option-text-${option.key}`] = `${t.questionnaires.optionText} ${t.common.required}`;
        }
      });
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const localizations: QuestionLocalization[] = [
      { language: Language.Fa, text: form.text.trim(), description: null }
    ];

    if (form.textEn.trim()) {
      localizations.push({ language: Language.En, text: form.textEn.trim(), description: null });
    }

    const options: SaveQuestionOptionRequest[] = needsOptions
      ? form.options.map((option, index) => ({
          code: option.code.trim(),
          displayOrder: index,
          localizations: [
            { language: Language.Fa, text: option.text.trim() } satisfies QuestionOptionLocalization
          ]
        }))
      : [];

    const request: SaveQuestionRequest = {
      code: form.code.trim(),
      type: form.type,
      scaleMax: form.type === QuestionType.Rating ? Number(form.scaleMax) : 5,
      localizations,
      options
    };

    try {
      const created = await createMutation.mutateAsync(request);
      onCreated(created.id);
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

  const typeOptions = (Object.keys(QuestionType) as unknown as string[])
    .filter((key) => Number.isNaN(Number(key)))
    .map((key) => ({
      value: String(QuestionType[key as keyof typeof QuestionType]),
      label: questionTypeLabel(t.questionnaires, QuestionType[key as keyof typeof QuestionType])
    }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t.questionnaires.newQuestion}
      description={t.questionnaires.newQuestionDescription}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="question-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="question-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField
          label={t.questionnaires.questionCode}
          htmlFor="questionCode"
          required
          error={errors.code}
        >
          <Input
            id="questionCode"
            value={form.code}
            onChange={(event) => updateField('code', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.questionnaires.questionType} htmlFor="questionType" required>
          <Select
            id="questionType"
            value={String(form.type)}
            onChange={(event) => updateField('type', Number(event.target.value))}
            options={typeOptions}
            disabled={isSaving}
          />
        </FormField>

        <FormField
          label={t.questionnaires.questionText}
          htmlFor="questionText"
          required
          error={errors.text}
        >
          <Input
            id="questionText"
            value={form.text}
            onChange={(event) => updateField('text', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.questionnaires.questionTextEn} htmlFor="questionTextEn">
          <Input
            id="questionTextEn"
            value={form.textEn}
            onChange={(event) => updateField('textEn', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        {form.type === QuestionType.Rating && (
          <FormField
            label={t.questionnaires.scaleMax}
            htmlFor="questionScaleMax"
            required
            error={errors.scaleMax}
            hint={t.questionnaires.scaleMaxHint}
          >
            <Input
              id="questionScaleMax"
              type="number"
              min="2"
              max="20"
              value={form.scaleMax}
              onChange={(event) => updateField('scaleMax', event.target.value)}
              disabled={isSaving}
              dir="ltr"
            />
          </FormField>
        )}

        {needsOptions && (
          <div className="col-span-full flex flex-col gap-3 border-t pt-4">
            <div className="flex items-center justify-between gap-2">
              <div className="flex flex-col">
                <h3 className="text-sm font-semibold">{t.questionnaires.answerOptions}</h3>
                {errors.options && (
                  <p className="text-xs text-destructive" role="alert">
                    {errors.options}
                  </p>
                )}
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={addOption}
                disabled={isSaving}
              >
                <Plus className="size-4" />
                {t.questionnaires.addOption}
              </Button>
            </div>

            {form.options.length === 0 ? (
              <p className="text-xs text-muted-foreground">{t.questionnaires.noOptionsYet}</p>
            ) : (
              <div className="flex flex-col gap-2">
                {form.options.map((option) => (
                  <div key={option.key} className="flex items-end gap-2">
                    <FormField
                      label={t.questionnaires.optionCode}
                      htmlFor={`option-code-${option.key}`}
                      error={errors[`option-code-${option.key}`]}
                      className="w-32 shrink-0"
                    >
                      <Input
                        id={`option-code-${option.key}`}
                        value={option.code}
                        onChange={(event) => updateOption(option.key, { code: event.target.value })}
                        disabled={isSaving}
                        dir="ltr"
                      />
                    </FormField>

                    <FormField
                      label={t.questionnaires.optionText}
                      htmlFor={`option-text-${option.key}`}
                      error={errors[`option-text-${option.key}`]}
                      className="flex-1"
                    >
                      <Input
                        id={`option-text-${option.key}`}
                        value={option.text}
                        onChange={(event) => updateOption(option.key, { text: event.target.value })}
                        disabled={isSaving}
                      />
                    </FormField>

                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className="size-9 shrink-0 text-destructive"
                      onClick={() => removeOption(option.key)}
                      disabled={isSaving}
                      aria-label={t.questionnaires.removeOption}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </form>
    </Dialog>
  );
}

interface OptionForm {
  key: string;
  code: string;
  text: string;
}

interface QuestionForm {
  code: string;
  type: QuestionType;
  text: string;
  textEn: string;
  scaleMax: string;
  options: OptionForm[];
}

function createKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `key-${Math.random().toString(36).slice(2)}`;
}

function createEmptyForm(): QuestionForm {
  return {
    code: '',
    type: QuestionType.SingleChoice,
    text: '',
    textEn: '',
    scaleMax: '5',
    options: []
  };
}
