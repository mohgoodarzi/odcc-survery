import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, GripVertical } from 'lucide-react';

import {
  questionnairesApi,
  type BranchingRule,
  type Questionnaire,
  type QuestionnaireLocalization,
  type SaveBranchingRuleRequest,
  type SaveItemRequest,
  type SaveQuestionnaireRequest,
  type SaveSectionRequest,
  type SectionLocalization
} from '@/api/questionnaires';
import { Language } from '@/api/surveys';
import { ApiError } from '@/api/client';
import { QuestionType, type Question } from '@/api/questionBank';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  queryKeys,
  useCreateQuestionnaire,
  useQuestionBankSearch,
  useQuestionnaire,
  useUpdateQuestionnaire
} from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import { questionTypeLabel } from './questionType';
import { QuestionDialog } from './QuestionDialog';

interface QuestionnaireDialogProps {
  open: boolean;
  onClose: () => void;
  questionnaireId?: string | null;
}

/**
 * دیالوگ ایجاد/ویرایش پرسشنامه: ساختار کامل بخش‌ها و سؤال‌ها.
 *
 * سؤال‌ها و گزینه‌های پاسخ در کتابخانه‌ی سؤالات تعریف می‌شوند و این‌جا
 * فقط به آن‌ها ارجاع داده می‌شود (با امکان تنظیم اجباری بودن و عنوان جایگزین).
 * قوانین انشعابِ آیتم‌های موجود هنگام ویرایش دست‌نخورده حفظ می‌شوند.
 */
export function QuestionnaireDialog({ open, onClose, questionnaireId }: QuestionnaireDialogProps) {
  const { t, culture } = useLanguage();
  const isEdit = !!questionnaireId;

  const { data: existing } = useQuestionnaire(questionnaireId ?? null);
  const { data: questionsPage } = useQuestionBankSearch({ searchText: null, type: null });

  const queryClient = useQueryClient();

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();
  const [newQuestionFor, setNewQuestionFor] = useState<{ sectionKey: string; itemKey: string } | null>(null);

  const createMutation = useCreateQuestionnaire();
  const updateMutation = useUpdateQuestionnaire();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

  const questionLookup = useMemo(() => {
    const map = new Map<string, Question>();
    for (const question of questionsPage?.items ?? []) {
      map.set(question.id, question);
    }
    return map;
  }, [questionsPage]);

  const questionOptions = useMemo(
    () =>
      (questionsPage?.items ?? []).map((question) => ({
        value: question.id,
        label: `${question.text || question.code} (${question.code})`
      })),
    [questionsPage]
  );

  function updateField(field: string, value: string | boolean) {
    setForm((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => {
      if (!(field in previous)) return previous;
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  function updateSection(sectionKey: string, patch: Partial<SectionForm>) {
    setForm((previous) => ({
      ...previous,
      sections: previous.sections.map((section) =>
        section.key === sectionKey ? { ...section, ...patch } : section
      )
    }));
  }

  function addSection() {
    setForm((previous) => ({
      ...previous,
      sections: [
        ...previous.sections,
        { key: createKey(), id: null, title: '', titleEn: '', isOptional: false, items: [] }
      ]
    }));
  }

  function removeSection(sectionKey: string) {
    setForm((previous) => ({
      ...previous,
      sections: previous.sections.filter((section) => section.key !== sectionKey)
    }));
  }

  function addItem(sectionKey: string) {
    setForm((previous) => ({
      ...previous,
      sections: previous.sections.map((section) =>
        section.key === sectionKey
          ? {
              ...section,
              items: [
                ...section.items,
                {
                  key: createKey(),
                  id: null,
                  questionId: '',
                  isRequired: true,
                  titleOverride: '',
                  branchingRules: []
                }
              ]
            }
          : section
      )
    }));
  }

  function updateItem(sectionKey: string, itemKey: string, patch: Partial<ItemForm>) {
    setForm((previous) => ({
      ...previous,
      sections: previous.sections.map((section) =>
        section.key === sectionKey
          ? {
              ...section,
              items: section.items.map((item) =>
                item.key === itemKey ? { ...item, ...patch } : item
              )
            }
          : section
      )
    }));
  }

  function removeItem(sectionKey: string, itemKey: string) {
    setForm((previous) => ({
      ...previous,
      sections: previous.sections.map((section) =>
        section.key === sectionKey
          ? { ...section, items: section.items.filter((item) => item.key !== itemKey) }
          : section
      )
    }));
  }

  function validate(): boolean {
    const next: Record<string, string> = {};

    if (!form.code.trim()) {
      next.code = `${t.questionnaires.code} ${t.common.required}`;
    } else if (!/^[A-Za-z0-9\-_.]+$/.test(form.code.trim())) {
      next.code = t.questionnaires.code + ' — A-Z a-z 0-9 - _ .';
    }

    if (!form.title.trim()) {
      next.title = `${t.questionnaires.title_} ${t.common.required}`;
    }

    form.sections.forEach((section) => {
      if (!section.title.trim()) {
        next[`section-${section.key}`] = `${t.questionnaires.sectionTitle} ${t.common.required}`;
      }
      if (section.items.length === 0) {
        next[`section-items-${section.key}`] = t.questionnaires.emptySection;
      }
      section.items.forEach((item) => {
        if (!item.questionId) {
          next[`item-${item.key}`] = `${t.questionnaires.selectQuestion} ${t.common.required}`;
        }
      });
    });

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const localizations: QuestionnaireLocalization[] = [
      { language: Language.Fa, title: form.title.trim(), description: form.description.trim() || null }
    ];

    if (form.titleEn.trim()) {
      localizations.push({
        language: Language.En,
        title: form.titleEn.trim(),
        description: null
      });
    }

    const sections: SaveSectionRequest[] = form.sections.map((section) => {
      const sectionLocalizations: SectionLocalization[] = [
        { language: Language.Fa, title: section.title.trim() }
      ];

      if (section.titleEn.trim()) {
        sectionLocalizations.push({ language: Language.En, title: section.titleEn.trim() });
      }

      const items: SaveItemRequest[] = section.items.map((item) => ({
        id: item.id,
        questionId: item.questionId,
        isRequired: item.isRequired,
        titleOverride: item.titleOverride.trim() || null,
        branchingRules: item.branchingRules
      }));

      return {
        id: section.id,
        isOptional: section.isOptional,
        localizations: sectionLocalizations,
        items
      };
    });

    const request: SaveQuestionnaireRequest = {
      code: form.code.trim(),
      localizations,
      sections
    };

    try {
      if (isEdit && questionnaireId) {
        await questionnairesApi.update(culture, questionnaireId, request);
      } else {
        await questionnairesApi.create(culture, request);
      }

      await queryClient.invalidateQueries({ queryKey: queryKeys.questionnaires });
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

  const hasSections = form.sections.length > 0;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.questionnaires.editQuestionnaire : t.questionnaires.newQuestionnaire}
      description={t.questionnaires.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="questionnaire-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="questionnaire-form" onSubmit={handleSubmit} className="flex flex-col gap-5" noValidate>
        {formError && (
          <div
            className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label={t.questionnaires.code} htmlFor="questionnaireCode" required error={errors.code}>
            <Input
              id="questionnaireCode"
              value={form.code}
              onChange={(event) => updateField('code', event.target.value)}
              disabled={isSaving}
              dir="ltr"
            />
          </FormField>

          <FormField label={t.questionnaires.title_} htmlFor="questionnaireTitle" required error={errors.title}>
            <Input
              id="questionnaireTitle"
              value={form.title}
              onChange={(event) => updateField('title', event.target.value)}
              disabled={isSaving}
            />
          </FormField>

          <FormField label={`${t.questionnaires.title_} (EN)`} htmlFor="questionnaireTitleEn">
            <Input
              id="questionnaireTitleEn"
              value={form.titleEn}
              onChange={(event) => updateField('titleEn', event.target.value)}
              disabled={isSaving}
              dir="ltr"
            />
          </FormField>

          <FormField
            label={t.questionnaires.descriptionLabel}
            htmlFor="questionnaireDescription"
            className="col-span-full"
          >
            <Input
              id="questionnaireDescription"
              value={form.description}
              onChange={(event) => updateField('description', event.target.value)}
              disabled={isSaving}
            />
          </FormField>
        </div>

        <div className="flex items-center justify-between gap-2 border-t pt-4">
          <div className="flex flex-col">
            <h3 className="text-sm font-semibold">{t.questionnaires.sectionsLabel}</h3>
            <p className="text-xs text-muted-foreground">{t.questionnaires.publishHint}</p>
          </div>
          <Button type="button" variant="outline" size="sm" onClick={addSection} disabled={isSaving}>
            <Plus className="size-4" />
            {t.questionnaires.addSection}
          </Button>
        </div>

        {hasSections ? (
          <div className="flex flex-col gap-4">
            {form.sections.map((section, sectionIndex) => (
              <div key={section.key} className="rounded-lg border bg-muted/20 p-4">
                <div className="mb-3 flex items-center gap-2">
                  <GripVertical className="size-4 shrink-0 text-muted-foreground" />
                  <span className="text-xs font-medium text-muted-foreground">
                    {t.questionnaires.sections} {sectionIndex + 1}
                  </span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="ms-auto size-7 text-destructive"
                    onClick={() => removeSection(section.key)}
                    disabled={isSaving}
                    aria-label={t.questionnaires.removeSection}
                  >
                    <Trash2 className="size-3.5" />
                  </Button>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <FormField
                    label={t.questionnaires.sectionTitle}
                    htmlFor={`section-title-${section.key}`}
                    required
                    error={errors[`section-${section.key}`]}
                  >
                    <Input
                      id={`section-title-${section.key}`}
                      value={section.title}
                      onChange={(event) => updateSection(section.key, { title: event.target.value })}
                      disabled={isSaving}
                    />
                  </FormField>

                  <FormField
                    label={t.questionnaires.sectionTitleEn}
                    htmlFor={`section-title-en-${section.key}`}
                  >
                    <Input
                      id={`section-title-en-${section.key}`}
                      value={section.titleEn}
                      onChange={(event) => updateSection(section.key, { titleEn: event.target.value })}
                      disabled={isSaving}
                      dir="ltr"
                    />
                  </FormField>
                </div>

                <label className="mt-3 flex cursor-pointer items-center gap-2 text-sm">
                  <Checkbox
                    checked={section.isOptional}
                    onCheckedChange={(checked) => updateSection(section.key, { isOptional: checked })}
                    disabled={isSaving}
                  />
                  {t.questionnaires.isOptionalSection}
                </label>

                <div className="mt-4 border-t pt-3">
                  <div className="mb-2 flex items-center justify-between gap-2">
                    <span className="text-xs font-medium text-muted-foreground">
                      {t.questionnaires.itemsLabel} ({section.items.length})
                    </span>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => addItem(section.key)}
                      disabled={isSaving}
                    >
                      <Plus className="size-4" />
                      {t.questionnaires.addItem}
                    </Button>
                  </div>

                  {errors[`section-items-${section.key}`] && section.items.length === 0 && (
                    <p className="mb-2 text-xs text-destructive" role="alert">
                      {errors[`section-items-${section.key}`]}
                    </p>
                  )}

                  {section.items.length === 0 ? (
                    <p className="text-xs text-muted-foreground">{t.questionnaires.emptySection}</p>
                  ) : (
                    <div className="flex flex-col gap-3">
                      {section.items.map((item, itemIndex) => {
                        const question = item.questionId ? questionLookup.get(item.questionId) : undefined;

                        return (
                          <div key={item.key} className="rounded-md border bg-card p-3">
                            <div className="flex items-center gap-2">
                              <span className="text-xs font-medium text-muted-foreground">
                                {t.questionnaires.questions} {itemIndex + 1}
                              </span>
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                className="ms-auto size-7 text-destructive"
                                onClick={() => removeItem(section.key, item.key)}
                                disabled={isSaving}
                                aria-label={t.questionnaires.removeItem}
                              >
                                <Trash2 className="size-3.5" />
                              </Button>
                            </div>

                            <FormField
                              label={t.questionnaires.selectQuestion}
                              htmlFor={`item-question-${item.key}`}
                              required
                              error={errors[`item-${item.key}`]}
                              className="mt-2"
                            >
                              <div className="flex items-end gap-2">
                                <Select
                                  id={`item-question-${item.key}`}
                                  value={item.questionId}
                                  onChange={(event) =>
                                    updateItem(section.key, item.key, { questionId: event.target.value })
                                  }
                                  options={questionOptions}
                                  placeholder={t.questionnaires.searchQuestions}
                                  disabled={isSaving}
                                />
                                <Button
                                  type="button"
                                  variant="outline"
                                  size="icon"
                                  className="size-9 shrink-0"
                                  onClick={() => setNewQuestionFor({ sectionKey: section.key, itemKey: item.key })}
                                  disabled={isSaving}
                                  aria-label={t.questionnaires.newQuestion}
                                  title={t.questionnaires.newQuestion}
                                >
                                  <Plus className="size-4" />
                                </Button>
                              </div>
                            </FormField>

                            {question && (
                              <div className="mt-2 flex flex-wrap items-center gap-1.5">
                                <span className="rounded-sm border bg-muted px-1.5 py-0.5 text-xs">
                                  {questionTypeLabel(t.questionnaires, question.type)}
                                </span>
                                {question.type === QuestionType.Rating && (
                                  <span className="rounded-sm border bg-muted px-1.5 py-0.5 text-xs">
                                    {t.questionnaires.scaleMax}: {question.scaleMax}
                                  </span>
                                )}
                                {question.options.length > 0 ? (
                                  question.options.map((option) => (
                                    <span
                                      key={option.id}
                                      className="rounded-sm border bg-muted px-1.5 py-0.5 text-xs"
                                      dir="ltr"
                                    >
                                      {option.code} — {option.text}
                                    </span>
                                  ))
                                ) : (
                                  <span className="text-xs text-muted-foreground">
                                    {t.questionnaires.noAnswerOptions}
                                  </span>
                                )}
                              </div>
                            )}

                            <div className="mt-3 grid gap-3 sm:grid-cols-[1fr_auto]">
                              <FormField
                                label={t.questionnaires.titleOverride}
                                htmlFor={`item-override-${item.key}`}
                                hint={t.questionnaires.titleOverrideHint}
                              >
                                <Input
                                  id={`item-override-${item.key}`}
                                  value={item.titleOverride}
                                  onChange={(event) =>
                                    updateItem(section.key, item.key, { titleOverride: event.target.value })
                                  }
                                  disabled={isSaving}
                                />
                              </FormField>

                              <label className="flex h-9 cursor-pointer items-center gap-2 whitespace-nowrap text-sm">
                                <Checkbox
                                  checked={item.isRequired}
                                  onCheckedChange={(checked) =>
                                    updateItem(section.key, item.key, { isRequired: checked })
                                  }
                                  disabled={isSaving}
                                />
                                {t.questionnaires.isRequired}
                              </label>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed p-6 text-center">
            <p className="text-sm font-medium">{t.questionnaires.noSections}</p>
            <p className="text-xs text-muted-foreground">{t.questionnaires.noSectionsDescription}</p>
            <Button type="button" variant="outline" size="sm" onClick={addSection} disabled={isSaving}>
              <Plus className="size-4" />
              {t.questionnaires.addSection}
            </Button>
          </div>
        )}
      </form>

      {newQuestionFor && (
        <QuestionDialog
          open={!!newQuestionFor}
          onClose={() => setNewQuestionFor(null)}
          onCreated={(questionId) => {
            updateItem(newQuestionFor.sectionKey, newQuestionFor.itemKey, { questionId });
            setNewQuestionFor(null);
          }}
        />
      )}
    </Dialog>
  );
}

interface ItemForm {
  /** کلید محلی برای React و آیتم‌های جدید (قبل از اینکه شناسه‌ی سرور بگیرند). */
  key: string;
  /** شناسه‌ی آیتم موجود (در ویرایش؛ خالی برای آیتم جدید). */
  id?: string | null;
  questionId: string;
  isRequired: boolean;
  titleOverride: string;
  /** قوانین انشعابِ آیتم موجود — دست‌نخورده بازگردانده می‌شوند. */
  branchingRules: SaveBranchingRuleRequest[];
}

interface SectionForm {
  key: string;
  id?: string | null;
  title: string;
  titleEn: string;
  isOptional: boolean;
  items: ItemForm[];
}

interface QuestionnaireForm {
  code: string;
  title: string;
  titleEn: string;
  description: string;
  sections: SectionForm[];
}

function createKey(): string {
  return (globalThis.crypto?.randomUUID?.() ?? `key-${Math.random().toString(36).slice(2)}`);
}

function createEmptyForm(): QuestionnaireForm {
  return {
    code: '',
    title: '',
    titleEn: '',
    description: '',
    sections: []
  };
}

/** تبدیل پرسشنامه‌ی دریافت‌شده از سرور به حالت فرم (شناسه‌ها حفظ می‌شوند). */
function toForm(existing: Questionnaire): QuestionnaireForm {
  const persian = existing.localizations.find((loc) => loc.language === Language.Fa);
  const english = existing.localizations.find((loc) => loc.language === Language.En);

  return {
    code: existing.code,
    title: persian?.title ?? existing.title,
    titleEn: english?.title ?? '',
    description: persian?.description ?? '',
    sections: existing.sections.map((section) => {
      const sectionPersian = section.localizations.find((loc) => loc.language === Language.Fa);
      const sectionEnglish = section.localizations.find((loc) => loc.language === Language.En);

      return {
        key: section.id,
        id: section.id,
        title: sectionPersian?.title ?? section.title,
        titleEn: sectionEnglish?.title ?? '',
        isOptional: section.isOptional,
        items: section.items.map((item) => ({
          key: item.id,
          id: item.id,
          questionId: item.questionId,
          isRequired: item.isRequired,
          titleOverride: item.titleOverride ?? '',
          branchingRules: item.branchingRules.map(toSaveRule)
        }))
      };
    })
  };
}

function toSaveRule(rule: BranchingRule): SaveBranchingRuleRequest {
  return {
    id: rule.id,
    targetItemId: rule.targetItemId,
    condition: rule.condition,
    expectedValue: rule.expectedValue
  };
}
