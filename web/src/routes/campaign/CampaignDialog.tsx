import { useEffect, useState, type FormEvent } from 'react';
import { Plus, Trash2 } from 'lucide-react';

import {
  campaignsApi,
  DistributionChannel,
  Language,
  TargetAudienceType,
  type Campaign,
  type CampaignLocalization,
  type ReminderLocalization,
  type SaveCampaignRequest,
  type SaveReminderRequest
} from '@/api/campaigns';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  queryKeys,
  useCampaign,
  useCampaignableSurveys,
  useCreateCampaign,
  useEmployeeOptions,
  useOrgUnits,
  useUpdateCampaign
} from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import { useQueryClient } from '@tanstack/react-query';
import type { Dictionary } from '@/i18n/types';

interface CampaignDialogProps {
  open: boolean;
  onClose: () => void;
  campaignId?: string | null;
}

/**
 * دیالوگ ایجاد/ویرایش کمپین: کد، نظرسنجی، ترجمه‌ها، جمعیت هدف، کانال،
 * زمان‌بندی و یادآورها.
 *
 * فقط کمپین‌های پیش‌نویس قابل ویرایش هستند (قاعده‌ی سرویس)؛ دیالوگ ویرایش
 * صرفاً برای وضعیت پیش‌نویس باز می‌شود.
 */
export function CampaignDialog({ open, onClose, campaignId }: CampaignDialogProps) {
  const { t, culture } = useLanguage();
  const isEdit = !!campaignId;

  const { data: surveys } = useCampaignableSurveys();
  const { data: orgUnits } = useOrgUnits();
  const { data: employees } = useEmployeeOptions(null);
  const { data: existing } = useCampaign(campaignId ?? null);

  const queryClient = useQueryClient();

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  const createMutation = useCreateCampaign();
  const updateMutation = useUpdateCampaign();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

  function updateField(field: keyof CampaignFormState, value: unknown) {
    setForm((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => {
      if (!(field in previous)) return previous;
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  function toggleOrgUnit(id: string, checked: boolean) {
    setForm((previous) => ({
      ...previous,
      targetOrgUnitIds: checked
        ? [...previous.targetOrgUnitIds, id]
        : previous.targetOrgUnitIds.filter((unitId) => unitId !== id)
    }));
  }

  function toggleEmployee(id: string, checked: boolean) {
    setForm((previous) => ({
      ...previous,
      targetEmployeeIds: checked
        ? [...previous.targetEmployeeIds, id]
        : previous.targetEmployeeIds.filter((employeeId) => employeeId !== id)
    }));
  }

  function addReminder() {
    setForm((previous) => ({
      ...previous,
      reminders: [
        ...previous.reminders,
        { key: crypto.randomUUID(), id: null, sendAt: '', subject: '', body: '' }
      ]
    }));
  }

  function updateReminder(index: number, field: keyof ReminderForm, value: string) {
    setForm((previous) => ({
      ...previous,
      reminders: previous.reminders.map((reminder, i) =>
        i === index ? { ...reminder, [field]: value } : reminder
      )
    }));
  }

  function removeReminder(index: number) {
    setForm((previous) => ({
      ...previous,
      reminders: previous.reminders.filter((_, i) => i !== index)
    }));
  }

  function validate(): boolean {
    const next: Record<string, string> = {};

    if (!form.code.trim()) next.code = t.campaigns.code + ' ' + t.common.required;
    if (!form.title.trim()) next.title = t.campaigns.title_ + ' ' + t.common.required;
    if (!form.surveyId) next.surveyId = t.campaigns.survey + ' ' + t.common.required;

    if (form.audienceType === TargetAudienceType.OrgUnits && form.targetOrgUnitIds.length === 0) {
      next.targetOrgUnits = t.campaigns.selectOrgUnits;
    }

    if (form.audienceType === TargetAudienceType.Employees && form.targetEmployeeIds.length === 0) {
      next.targetEmployees = t.campaigns.selectEmployees;
    }

    form.reminders.forEach((reminder, index) => {
      if (!reminder.sendAt) {
        next[`reminder-${index}-sendAt`] = t.campaigns.reminderSendAt + ' ' + t.common.required;
      }
      if (!reminder.subject.trim()) {
        next[`reminder-${index}-subject`] = t.campaigns.reminderSubject + ' ' + t.common.required;
      }
    });

    if (form.scheduledAt && form.endsAt && new Date(form.endsAt) <= new Date(form.scheduledAt)) {
      next.endsAt = t.campaigns.endsAt + ' > ' + t.campaigns.scheduledAt;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const localizations: CampaignLocalization[] = [
      {
        language: Language.Fa,
        title: form.title.trim(),
        description: form.description.trim() || null
      }
    ];

    if (form.titleEn.trim()) {
      localizations.push({
        language: Language.En,
        title: form.titleEn.trim(),
        description: form.descriptionEn.trim() || null
      });
    }

    const reminders: SaveReminderRequest[] = form.reminders.map((reminder) => {
      const reminderLocalizations: ReminderLocalization[] = [
        { language: Language.Fa, subject: reminder.subject.trim(), body: reminder.body.trim() || null }
      ];

      return {
        id: reminder.id ?? null,
        sendAt: toIso(reminder.sendAt)!,
        localizations: reminderLocalizations
      };
    });

    const request: SaveCampaignRequest = {
      code: form.code.trim(),
      surveyId: form.surveyId,
      audienceType: form.audienceType,
      includeInactiveEmployees: form.includeInactiveEmployees,
      channel: form.channel,
      scheduledAt: toIso(form.scheduledAt),
      endsAt: toIso(form.endsAt),
      localizations,
      // فقط اهدافِ مرتبط با نوع جمعیت هدف ارسال می‌شوند تا داده‌ی کهنه ارسال نشود.
      targetOrgUnitIds: form.audienceType === TargetAudienceType.OrgUnits ? form.targetOrgUnitIds : [],
      targetEmployeeIds: form.audienceType === TargetAudienceType.Employees ? form.targetEmployeeIds : [],
      reminders
    };

    try {
      if (isEdit && campaignId) {
        await campaignsApi.update(culture, campaignId, request);
      } else {
        await campaignsApi.create(culture, request);
      }

      await queryClient.invalidateQueries({ queryKey: queryKeys.campaigns });
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

      // کلیدهای سرور ممکن است با فیلدهای فرم تطابق کامل نداشته باشند،
      // بنابراین اولین پیام هم به‌صورت یک خطای کلی نمایش داده می‌شود.
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

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.campaigns.editCampaign : t.campaigns.newCampaign}
      description={t.campaigns.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="campaign-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="campaign-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.campaigns.code} htmlFor="campaignCode" required error={errors.code}>
          <Input
            id="campaignCode"
            value={form.code}
            onChange={(event) => updateField('code', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.campaigns.survey} htmlFor="campaignSurvey" required error={errors.surveyId}>
          <Select
            id="campaignSurvey"
            value={form.surveyId}
            onChange={(event) => updateField('surveyId', event.target.value)}
            options={surveyOptions}
            placeholder={t.campaigns.survey}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.campaigns.title_} htmlFor="campaignTitle" required error={errors.title}>
          <Input
            id="campaignTitle"
            value={form.title}
            onChange={(event) => updateField('title', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={`${t.campaigns.title_} (EN)`} htmlFor="campaignTitleEn">
          <Input
            id="campaignTitleEn"
            value={form.titleEn}
            onChange={(event) => updateField('titleEn', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.campaigns.descriptionLabel} htmlFor="campaignDescription" className="col-span-full">
          <Input
            id="campaignDescription"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.campaigns.audienceType} htmlFor="campaignAudienceType" required>
          <Select
            id="campaignAudienceType"
            value={String(form.audienceType)}
            onChange={(event) => updateField('audienceType', Number(event.target.value))}
            options={audienceOptions(t.campaigns)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.campaigns.channel} htmlFor="campaignChannel" required>
          <Select
            id="campaignChannel"
            value={String(form.channel)}
            onChange={(event) => updateField('channel', Number(event.target.value))}
            options={channelOptions(t.campaigns)}
            disabled={isSaving}
          />
        </FormField>

        {form.audienceType === TargetAudienceType.OrgUnits && (
          <FormField
            label={t.campaigns.targetOrgUnits}
            htmlFor="campaignTargetUnits"
            required
            error={errors.targetOrgUnits}
            className="col-span-full"
          >
            <div id="campaignTargetUnits" className="grid max-h-48 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">
              {(orgUnits ?? []).map((unit) => (
                <label key={unit.id} className="flex cursor-pointer items-center gap-2 text-sm">
                  <Checkbox
                    checked={form.targetOrgUnitIds.includes(unit.id)}
                    onCheckedChange={(checked) => toggleOrgUnit(unit.id, checked)}
                    disabled={isSaving}
                  />
                  <span>{unit.name}</span>
                  <span className="text-xs text-muted-foreground" dir="ltr">{unit.code}</span>
                </label>
              ))}
            </div>
            <label className="mt-2 flex cursor-pointer items-center gap-2 text-sm">
              <Checkbox
                checked={form.includeDescendants}
                onCheckedChange={(checked) => updateField('includeDescendants', checked)}
                disabled={isSaving}
              />
              {t.campaigns.includeDescendants}
            </label>
          </FormField>
        )}

        {form.audienceType === TargetAudienceType.Employees && (
          <FormField
            label={t.campaigns.targetEmployees}
            htmlFor="campaignTargetEmployees"
            required
            error={errors.targetEmployees}
            className="col-span-full"
          >
            <div id="campaignTargetEmployees" className="grid max-h-48 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">
              {(employees ?? []).map((employee) => (
                <label key={employee.id} className="flex cursor-pointer items-center gap-2 text-sm">
                  <Checkbox
                    checked={form.targetEmployeeIds.includes(employee.id)}
                    onCheckedChange={(checked) => toggleEmployee(employee.id, checked)}
                    disabled={isSaving}
                  />
                  <span>{employee.fullName}</span>
                  <span className="text-xs text-muted-foreground" dir="ltr">{employee.employeeCode}</span>
                </label>
              ))}
            </div>
          </FormField>
        )}

        <FormField label={t.campaigns.scheduledAt} htmlFor="campaignScheduledAt" hint={t.campaigns.scheduleHint}>
          <Input
            id="campaignScheduledAt"
            type="datetime-local"
            value={form.scheduledAt}
            onChange={(event) => updateField('scheduledAt', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.campaigns.endsAt} htmlFor="campaignEndsAt" error={errors.endsAt}>
          <Input
            id="campaignEndsAt"
            type="datetime-local"
            value={form.endsAt}
            onChange={(event) => updateField('endsAt', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <label
          className="col-span-full flex cursor-pointer items-center gap-3 rounded-md border p-3"
        >
          <Checkbox
            checked={form.includeInactiveEmployees}
            onCheckedChange={(checked) => updateField('includeInactiveEmployees', checked)}
            disabled={isSaving}
          />
          <span className="flex flex-col gap-0.5">
            <span className="text-sm font-medium">{t.campaigns.includeInactiveEmployees}</span>
            <span className="text-xs text-muted-foreground">{t.campaigns.includeInactiveEmployeesHint}</span>
          </span>
        </label>

        {/* یادآورها */}
        <div className="col-span-full flex flex-col gap-3">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-sm font-medium">{t.campaigns.reminders}</h3>
            <Button type="button" variant="outline" size="sm" onClick={addReminder} disabled={isSaving}>
              <Plus className="size-4" />
              {t.campaigns.addReminder}
            </Button>
          </div>

          <p className="text-xs text-muted-foreground">{t.campaigns.reminderHint}</p>

          {form.reminders.map((reminder, index) => (
            <div key={reminder.key} className="grid gap-3 rounded-md border p-3 sm:grid-cols-2">
              <FormField
                label={t.campaigns.reminderSendAt}
                htmlFor={`reminder-sendAt-${index}`}
                required
                error={errors[`reminder-${index}-sendAt`]}
              >
                <Input
                  id={`reminder-sendAt-${index}`}
                  type="datetime-local"
                  value={reminder.sendAt}
                  onChange={(event) => updateReminder(index, 'sendAt', event.target.value)}
                  disabled={isSaving}
                  dir="ltr"
                />
              </FormField>

              <FormField
                label={t.campaigns.reminderSubject}
                htmlFor={`reminder-subject-${index}`}
                required
                error={errors[`reminder-${index}-subject`]}
              >
                <Input
                  id={`reminder-subject-${index}`}
                  value={reminder.subject}
                  onChange={(event) => updateReminder(index, 'subject', event.target.value)}
                  disabled={isSaving}
                />
              </FormField>

              <FormField
                label={t.campaigns.reminderBody}
                htmlFor={`reminder-body-${index}`}
                className="sm:col-span-2"
              >
                <Input
                  id={`reminder-body-${index}`}
                  value={reminder.body}
                  onChange={(event) => updateReminder(index, 'body', event.target.value)}
                  disabled={isSaving}
                />
              </FormField>

              <div className="sm:col-span-2 flex justify-end">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="text-destructive"
                  onClick={() => removeReminder(index)}
                  disabled={isSaving}
                >
                  <Trash2 className="size-4" />
                  {t.campaigns.removeReminder}
                </Button>
              </div>
            </div>
          ))}
        </div>
      </form>
    </Dialog>
  );
}

interface ReminderForm {
  /** کلید محلی برای React؛ با شناسه‌ی سمت سرور متفاوت است. */
  key: string;
  /** شناسه‌ی یادآور موجود (در ویرایش؛ برای یادآور جدید خالی). */
  id: string | null;
  sendAt: string;
  subject: string;
  body: string;
}

interface CampaignFormState {
  code: string;
  surveyId: string;
  title: string;
  titleEn: string;
  description: string;
  descriptionEn: string;
  audienceType: TargetAudienceType;
  channel: DistributionChannel;
  includeInactiveEmployees: boolean;
  includeDescendants: boolean;
  scheduledAt: string;
  endsAt: string;
  targetOrgUnitIds: string[];
  targetEmployeeIds: string[];
  reminders: ReminderForm[];
}

function createEmptyForm(): CampaignFormState {
  return {
    code: '',
    surveyId: '',
    title: '',
    titleEn: '',
    description: '',
    descriptionEn: '',
    audienceType: TargetAudienceType.AllCompany,
    channel: DistributionChannel.Email,
    includeInactiveEmployees: false,
    includeDescendants: true,
    scheduledAt: '',
    endsAt: '',
    targetOrgUnitIds: [],
    targetEmployeeIds: [],
    reminders: []
  };
}

function toForm(existing: Campaign): CampaignFormState {
  const persian = existing.localizations.find((loc) => loc.language === Language.Fa);
  const english = existing.localizations.find((loc) => loc.language === Language.En);

  return {
    code: existing.code,
    surveyId: existing.surveyId,
    title: persian?.title ?? existing.title,
    titleEn: english?.title ?? '',
    description: persian?.description ?? '',
    descriptionEn: english?.description ?? '',
    audienceType: existing.audienceType,
    channel: existing.channel,
    includeInactiveEmployees: existing.includeInactiveEmployees,
    includeDescendants: existing.targetUnits.some((unit) => unit.includeDescendants),
    scheduledAt: toLocalInputValue(existing.scheduledAt),
    endsAt: toLocalInputValue(existing.endsAt),
    targetOrgUnitIds: existing.targetUnits.map((unit) => unit.orgUnitId),
    targetEmployeeIds: existing.targetMembers.map((member) => member.employeeId),
    reminders: existing.reminders.map((reminder) => {
      const reminderPersian = reminder.localizations.find((loc) => loc.language === Language.Fa);
      return {
        key: reminder.id,
        id: reminder.id,
        sendAt: toLocalInputValue(reminder.sendAt),
        subject: reminderPersian?.subject ?? reminder.subject,
        body: reminderPersian?.body ?? ''
      };
    })
  };
}

/**
 * تبدیل مقدار ورودی datetime-local (زمان محلی) به ISO 8601 با منطقه‌ی UTC.
 * کاربر زمان را به زمان محلی خود انتخاب می‌کند و سرور در UTC کار می‌کند.
 */
function toIso(localDateTime: string): string | null {
  if (!localDateTime) return null;
  const parsed = new Date(localDateTime);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

/** تبدیل ISO 8601 سرور به مقدار ورودی datetime-local (زمان محلی). */
function toLocalInputValue(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';

  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function audienceOptions(t: Dictionary['campaigns']) {
  return [
    { value: String(TargetAudienceType.AllCompany), label: t.audienceAllCompany },
    { value: String(TargetAudienceType.OrgUnits), label: t.audienceOrgUnits },
    { value: String(TargetAudienceType.Employees), label: t.audienceEmployees }
  ];
}

function channelOptions(t: Dictionary['campaigns']) {
  return [
    { value: String(DistributionChannel.Email), label: t.channelEmail },
    { value: String(DistributionChannel.Sms), label: t.channelSms },
    { value: String(DistributionChannel.InApp), label: t.channelInApp },
    { value: String(DistributionChannel.PublicLink), label: t.channelPublicLink }
  ];
}
