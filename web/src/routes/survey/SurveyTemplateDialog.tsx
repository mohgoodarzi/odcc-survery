import { useEffect, useState, type FormEvent } from 'react';

import {
  Language,
  surveyTemplatesApi,
  type SaveSurveyTemplateRequest,
  type SurveyTemplateLocalization
} from '@/api/surveys';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  queryKeys,
  useCreateSurveyTemplate,
  useQuestionnaires,
  useSurveyTemplate,
  useUpdateSurveyTemplate
} from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import { useQueryClient } from '@tanstack/react-query';

interface SurveyTemplateDialogProps {
  open: boolean;
  onClose: () => void;
  templateId?: string | null;
}

/**
 * دیالوگ ایجاد/ویرایش قالب نظرسنجی.
 */
export function SurveyTemplateDialog({ open, onClose, templateId }: SurveyTemplateDialogProps) {
  const { t, culture } = useLanguage();
  const isEdit = !!templateId;

  const { data: questionnaires } = useQuestionnaires();
  const { data: existing } = useSurveyTemplate(templateId ?? null);
  const queryClient = useQueryClient();

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  const createMutation = useCreateSurveyTemplate();
  const updateMutation = useUpdateSurveyTemplate();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

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

    if (!form.code.trim()) next.code = t.surveys.code + ' ' + t.common.required;
    if (!form.title.trim()) next.title = t.surveys.title_ + ' ' + t.common.required;
    if (!form.questionnaireId) next.questionnaireId = t.surveys.questionnaire + ' ' + t.common.required;

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const localizations: SurveyTemplateLocalization[] = [
      { language: Language.Fa, title: form.title.trim(), description: form.description.trim() || null }
    ];

    if (form.titleEn.trim()) {
      localizations.push({ language: Language.En, title: form.titleEn.trim(), description: null });
    }

    const request: SaveSurveyTemplateRequest = {
      code: form.code.trim(),
      questionnaireId: form.questionnaireId,
      localizations,
      isAnonymous: form.isAnonymous,
      allowEditResponse: form.allowEditResponse,
      showProgressBar: form.showProgressBar,
      singleResponsePerUser: form.singleResponsePerUser,
      estimatedMinutes: Number(form.estimatedMinutes) || 5
    };

    try {
      if (isEdit && templateId) {
        await surveyTemplatesApi.update(culture, templateId, request);
      } else {
        await surveyTemplatesApi.create(culture, request);
      }

      await queryClient.invalidateQueries({ queryKey: queryKeys.surveyTemplates });
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

  const questionnaireOptions = (questionnaires?.items ?? []).map((questionnaire) => ({
    value: questionnaire.id,
    label: `${questionnaire.title} (${questionnaire.code})`
  }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.surveys.editTemplate : t.surveys.newTemplate}
      description={t.surveys.templatesDescription}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="template-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="template-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.surveys.code} htmlFor="templateCode" required error={errors.code}>
          <Input
            id="templateCode"
            value={form.code}
            onChange={(event) => updateField('code', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.surveys.questionnaire}
          htmlFor="templateQuestionnaire"
          required
          error={errors.questionnaireId}
        >
          <Select
            id="templateQuestionnaire"
            value={form.questionnaireId}
            onChange={(event) => updateField('questionnaireId', event.target.value)}
            options={questionnaireOptions}
            placeholder={t.surveys.selectQuestionnaire}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.surveys.title_} htmlFor="templateTitle" required error={errors.title}>
          <Input
            id="templateTitle"
            value={form.title}
            onChange={(event) => updateField('title', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={`${t.surveys.title_} (EN)`} htmlFor="templateTitleEn">
          <Input
            id="templateTitleEn"
            value={form.titleEn}
            onChange={(event) => updateField('titleEn', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.surveys.descriptionLabel}
          htmlFor="templateDescription"
          className="col-span-full"
        >
          <Input
            id="templateDescription"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.surveys.estimatedMinutes} htmlFor="templateEstimatedMinutes">
          <Input
            id="templateEstimatedMinutes"
            type="number"
            min="1"
            max="240"
            value={form.estimatedMinutes}
            onChange={(event) => updateField('estimatedMinutes', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <SettingRow
          checked={form.isAnonymous}
          onChange={(checked) => updateField('isAnonymous', checked)}
          label={t.surveys.isAnonymous}
          hint={t.surveys.isAnonymousHint}
          disabled={isSaving}
        />

        <SettingRow
          checked={form.allowEditResponse}
          onChange={(checked) => updateField('allowEditResponse', checked)}
          label={t.surveys.allowEditResponse}
          disabled={isSaving}
        />

        <SettingRow
          checked={form.showProgressBar}
          onChange={(checked) => updateField('showProgressBar', checked)}
          label={t.surveys.showProgressBar}
          disabled={isSaving}
        />

        <SettingRow
          checked={form.singleResponsePerUser}
          onChange={(checked) => updateField('singleResponsePerUser', checked)}
          label={t.surveys.singleResponsePerUser}
          disabled={isSaving}
        />
      </form>
    </Dialog>
  );
}

interface SettingRowProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  hint?: string;
  disabled?: boolean;
}

function SettingRow({ checked, onChange, label, hint, disabled }: SettingRowProps) {
  return (
    <label className="flex cursor-pointer items-center gap-3 rounded-md border p-3">
      <Checkbox checked={checked} onCheckedChange={onChange} disabled={disabled} />
      <span className="flex flex-col gap-0.5">
        <span className="text-sm font-medium">{label}</span>
        {hint && <span className="text-xs text-muted-foreground">{hint}</span>}
      </span>
    </label>
  );
}

interface TemplateFormState {
  code: string;
  questionnaireId: string;
  title: string;
  titleEn: string;
  description: string;
  estimatedMinutes: string;
  isAnonymous: boolean;
  allowEditResponse: boolean;
  showProgressBar: boolean;
  singleResponsePerUser: boolean;
}

function createEmptyForm(): TemplateFormState {
  return {
    code: '',
    questionnaireId: '',
    title: '',
    titleEn: '',
    description: '',
    estimatedMinutes: '5',
    isAnonymous: false,
    allowEditResponse: true,
    showProgressBar: true,
    singleResponsePerUser: true
  };
}

function toForm(existing: {
  code: string;
  questionnaireId: string;
  title: string;
  isAnonymous: boolean;
  allowEditResponse: boolean;
  showProgressBar: boolean;
  singleResponsePerUser: boolean;
  estimatedMinutes: number;
  localizations: SurveyTemplateLocalization[];
}): TemplateFormState {
  const persian = existing.localizations.find((loc) => loc.language === Language.Fa);
  const english = existing.localizations.find((loc) => loc.language === Language.En);

  return {
    code: existing.code,
    questionnaireId: existing.questionnaireId,
    title: persian?.title ?? existing.title,
    titleEn: english?.title ?? '',
    description: persian?.description ?? '',
    estimatedMinutes: String(existing.estimatedMinutes),
    isAnonymous: existing.isAnonymous,
    allowEditResponse: existing.allowEditResponse,
    showProgressBar: existing.showProgressBar,
    singleResponsePerUser: existing.singleResponsePerUser
  };
}
