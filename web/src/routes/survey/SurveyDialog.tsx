import { useEffect, useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';

import { surveysApi, Language, SurveyStatus, type SaveSurveyRequest, type Survey, type SurveyLocalization } from '@/api/surveys';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { queryKeys, useCreateSurvey, useQuestionnaire, useQuestionnaires, useSurvey, useUpdateSurvey } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { DatePicker } from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import { isoToLocalDate } from '@/lib/datetime';

interface SurveyDialogProps {
  open: boolean;
  onClose: () => void;
  surveyId?: string | null;
  /** پس از ذخیره‌ی موفق، با پیام مناسب برای نمایش روی صفحه فراخوانی می‌شود. */
  onSaved?: (message: string) => void;
}

/**
 * دیالوگ ایجاد/ویرایش نظرسنجی.
 *
 * فهرست نظرسنجی‌ها در کوئری «surveys» نگهداری می‌شود و دیالوگ برای
 * پیش‌بندی ویرایش، مدخل نظرسنجی جاری را از همان کش می‌خواند.
 *
 * **فیلدهای قفل‌شده:** پس از انتشار، فیلدهای ساختاری (کد، پرسشنامه، ناشناس
 * بودن، تک‌پاسخی و تاریخ شروع) غیرفعال می‌شوند تا یکپارچگی پاسخ‌های
 * جمع‌آوری‌شده و قرارداد حریم خصوصی تغییر نکند. فقط متن‌های نمایشی، تاریخ
 * پایان و تنظیمات نمایش قابل ویرایش می‌مانند.
 */
export function SurveyDialog({ open, onClose, surveyId, onSaved }: SurveyDialogProps) {
  const { t, culture } = useLanguage();
  const isEdit = !!surveyId;

  const { data: questionnaires } = useQuestionnaires();

  // خواندن نظرسنجی کامل برای پیش‌بندی فرم ویرایش (خلاصه فهرست همه‌ی فیلدها را ندارد).
  const { data: existing } = useSurvey(surveyId ?? null);

  // پرسشنامه‌ی فعلیِ نظرسنجی ممکن است دیگر «فعال» نباشد (مثلاً بایگانی شده
  // باشد). عنوان آن بارگذاری می‌شود تا بتوانیم همان گزینه را به فهرست
  // گزینه‌ها اضافه کنیم و فیلد پرسشنامه خالی نمایش داده نشود.
  const { data: currentQuestionnaire } = useQuestionnaire(existing?.questionnaireId ?? null);

  const queryClient = useQueryClient();

  const isPublished = isEdit && existing !== undefined && existing.status !== SurveyStatus.Draft;

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  const createMutation = useCreateSurvey();
  const updateMutation = useUpdateSurvey();
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

    const localizations: SurveyLocalization[] = [
      {
        language: Language.Fa,
        title: form.title.trim(),
        description: form.description.trim() || null,
        welcomeMessage: form.welcomeMessage.trim() || null,
        thankYouMessage: form.thankYouMessage.trim() || null
      }
    ];

    if (form.titleEn.trim()) {
      localizations.push({
        language: Language.En,
        title: form.titleEn.trim(),
        description: form.descriptionEn.trim() || null,
        welcomeMessage: null,
        thankYouMessage: null
      });
    }

    const request: SaveSurveyRequest = {
      code: form.code.trim(),
      questionnaireId: form.questionnaireId,
      localizations,
      isAnonymous: form.isAnonymous,
      allowEditResponse: form.allowEditResponse,
      showProgressBar: form.showProgressBar,
      singleResponsePerUser: form.singleResponsePerUser,
      startDate: form.startDate || null,
      endDate: form.endDate || null,
      estimatedMinutes: Number(form.estimatedMinutes) || 5
    };

    try {
      if (isEdit && surveyId) {
        await surveysApi.update(culture, surveyId, request);
        onSaved?.(t.surveys.surveyUpdated);
      } else {
        await surveysApi.create(culture, request);
        onSaved?.(t.surveys.surveyCreated);
      }

      await queryClient.invalidateQueries({ queryKey: queryKeys.surveys });
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

  // فهرست گزینه‌ها فقط پرسشنامه‌های «فعال» را می‌آورد تا انتخاب یک پرسشنامه‌ی
  // غیرقابل‌استفاده ممکن نباشد. اما پرسشنامه‌ی فعلیِ نظرسنجی باید همیشه در
  // فهرست باشد، حتی اگر بایگانی شده باشد؛ در غیر این صورت فیلد پر نمی‌شود و
  // به نظر می‌رسد نظرسنجی پرسشنامه‌ای ندارد.
  if (currentQuestionnaire && !questionnaireOptions.some((option) => option.value === currentQuestionnaire.id)) {
    questionnaireOptions.push({
      value: currentQuestionnaire.id,
      label: `${currentQuestionnaire.title} (${currentQuestionnaire.code})`
    });
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.surveys.editSurvey : t.surveys.newSurvey}
      description={t.surveys.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="survey-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="survey-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        {isPublished && (
          <div className="col-span-full rounded-md border border-primary/30 bg-primary/5 p-3 text-xs text-foreground">
            {t.surveys.publishedSurveyEditHint}
          </div>
        )}

        <FormField
          label={t.surveys.code}
          htmlFor="surveyCode"
          required
          error={errors.code}
          hint={isPublished ? t.surveys.lockedFieldHint : undefined}
        >
          <Input
            id="surveyCode"
            value={form.code}
            onChange={(event) => updateField('code', event.target.value)}
            disabled={isSaving || isPublished}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.surveys.questionnaire}
          htmlFor="surveyQuestionnaire"
          required
          error={errors.questionnaireId}
          hint={
            isPublished
              ? t.surveys.lockedFieldHint
              : questionnaireOptions.length === 0
                ? t.surveys.noActiveQuestionnaires
                : undefined
          }
        >
          <Select
            id="surveyQuestionnaire"
            value={form.questionnaireId}
            onChange={(event) => updateField('questionnaireId', event.target.value)}
            options={questionnaireOptions}
            placeholder={t.surveys.selectQuestionnaire}
            disabled={isSaving || isPublished}
          />
        </FormField>

        <FormField label={t.surveys.title_} htmlFor="surveyTitle" required error={errors.title}>
          <Input
            id="surveyTitle"
            value={form.title}
            onChange={(event) => updateField('title', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={`${t.surveys.title_} (EN)`} htmlFor="surveyTitleEn">
          <Input
            id="surveyTitleEn"
            value={form.titleEn}
            onChange={(event) => updateField('titleEn', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.surveys.descriptionLabel}
          htmlFor="surveyDescription"
          className="col-span-full"
        >
          <Input
            id="surveyDescription"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.surveys.estimatedMinutes} htmlFor="surveyEstimatedMinutes">
          <Input
            id="surveyEstimatedMinutes"
            type="number"
            min="1"
            max="240"
            value={form.estimatedMinutes}
            onChange={(event) => updateField('estimatedMinutes', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.surveys.startDate}
          htmlFor="surveyStartDate"
          hint={isPublished ? t.surveys.lockedFieldHint : undefined}
        >
          <DatePicker
            id="surveyStartDate"
            value={form.startDate}
            onChange={(value) => updateField('startDate', value)}
            disabled={isSaving || isPublished}
          />
        </FormField>

        <FormField label={t.surveys.endDate} htmlFor="surveyEndDate">
          <DatePicker
            id="surveyEndDate"
            value={form.endDate}
            onChange={(value) => updateField('endDate', value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.surveys.welcomeMessage} htmlFor="surveyWelcome" className="col-span-full">
          <Input
            id="surveyWelcome"
            value={form.welcomeMessage}
            onChange={(event) => updateField('welcomeMessage', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.surveys.thankYouMessage} htmlFor="surveyThankYou" className="col-span-full">
          <Input
            id="surveyThankYou"
            value={form.thankYouMessage}
            onChange={(event) => updateField('thankYouMessage', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <SettingsCheckbox
          checked={form.isAnonymous}
          onChange={(checked) => updateField('isAnonymous', checked)}
          label={t.surveys.isAnonymous}
          hint={isPublished ? t.surveys.lockedFieldHint : t.surveys.isAnonymousHint}
          disabled={isSaving || isPublished}
        />

        <SettingsCheckbox
          checked={form.allowEditResponse}
          onChange={(checked) => updateField('allowEditResponse', checked)}
          label={t.surveys.allowEditResponse}
          disabled={isSaving}
        />

        <SettingsCheckbox
          checked={form.showProgressBar}
          onChange={(checked) => updateField('showProgressBar', checked)}
          label={t.surveys.showProgressBar}
          disabled={isSaving}
        />

        <SettingsCheckbox
          checked={form.singleResponsePerUser}
          onChange={(checked) => updateField('singleResponsePerUser', checked)}
          label={t.surveys.singleResponsePerUser}
          hint={isPublished ? t.surveys.lockedFieldHint : undefined}
          disabled={isSaving || isPublished}
        />
      </form>
    </Dialog>
  );
}

interface SettingsCheckboxProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  hint?: string;
  disabled?: boolean;
}

function SettingsCheckbox({ checked, onChange, label, hint, disabled }: SettingsCheckboxProps) {
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

interface SurveyFormState {
  code: string;
  questionnaireId: string;
  title: string;
  titleEn: string;
  description: string;
  descriptionEn: string;
  estimatedMinutes: string;
  startDate: string;
  endDate: string;
  welcomeMessage: string;
  thankYouMessage: string;
  isAnonymous: boolean;
  allowEditResponse: boolean;
  showProgressBar: boolean;
  singleResponsePerUser: boolean;
}

function createEmptyForm(): SurveyFormState {
  return {
    code: '',
    questionnaireId: '',
    title: '',
    titleEn: '',
    description: '',
    descriptionEn: '',
    estimatedMinutes: '5',
    startDate: '',
    endDate: '',
    welcomeMessage: '',
    thankYouMessage: '',
    isAnonymous: false,
    allowEditResponse: true,
    showProgressBar: true,
    singleResponsePerUser: true
  };
}

function toForm(existing: Survey): SurveyFormState {
  const persian = existing.localizations.find((loc) => loc.language === Language.Fa);
  const english = existing.localizations.find((loc) => loc.language === Language.En);

  return {
    code: existing.code,
    questionnaireId: existing.questionnaireId,
    title: persian?.title ?? existing.title,
    titleEn: english?.title ?? '',
    description: persian?.description ?? '',
    descriptionEn: english?.description ?? '',
    estimatedMinutes: String(existing.estimatedMinutes),
    startDate: isoToLocalDate(existing.startDate),
    endDate: isoToLocalDate(existing.endDate),
    welcomeMessage: persian?.welcomeMessage ?? '',
    thankYouMessage: persian?.thankYouMessage ?? '',
    isAnonymous: existing.isAnonymous,
    allowEditResponse: existing.allowEditResponse,
    showProgressBar: existing.showProgressBar,
    singleResponsePerUser: existing.singleResponsePerUser
  };
}
