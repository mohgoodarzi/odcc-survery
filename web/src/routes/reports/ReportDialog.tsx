import { useEffect, useState, type FormEvent } from 'react';

import {
  ReportFormat,
  ReportSchedule,
  ReportStatus,
  ReportType,
  type ReportDefinition,
  type SaveReportRequest
} from '@/api/reports';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useCampaignableSurveys, useOrgUnits } from '@/api/hooks';
import { useCreateReport, useReport, useUpdateReport } from '@/api/reportsHooks';
import { Dialog } from '@/components/ui/dialog';
import { DateTimePicker } from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import type { Dictionary } from '@/i18n/types';
import { isoToLocalDateTime, localDateTimeToIso } from '@/lib/datetime';

interface ReportDialogProps {
  open: boolean;
  onClose: () => void;
  reportId?: string | null;
}

/**
 * دیالوگ ایجاد/ویرایش تعریف گزارش: نام، نوع، قالب، زمان‌بندی، بازه‌ی زمانی،
 * محدودیت به واحد سازمانی و تعداد خروجی‌های نگه‌داشته‌شده.
 *
 * فقط تعاریف غیر بایگانی‌شده قابل ویرایش هستند (قاعده‌ی سرویس).
 */
export function ReportDialog({ open, onClose, reportId }: ReportDialogProps) {
  const { t } = useLanguage();
  const isEdit = !!reportId;

  const { data: surveys } = useCampaignableSurveys();
  const { data: orgUnits } = useOrgUnits();
  const { data: existing } = useReport(reportId ?? null);

  const createMutation = useCreateReport();
  const updateMutation = useUpdateReport();
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

  function updateField(field: keyof ReportFormState, value: unknown) {
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

    if (!form.name.trim()) next.name = t.reports.reportName + ' ' + t.common.required;

    if (form.type !== ReportType.DashboardSummary && !form.surveyId) {
      next.surveyId = t.reports.survey + ' ' + t.common.required;
    }

    if (form.retentionCount < 1) {
      next.retentionCount = t.reports.retentionCount + ' ≥ 1';
    }

    if (form.from && form.to && new Date(form.to) < new Date(form.from)) {
      next.to = t.reports.windowTo + ' > ' + t.reports.windowFrom;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const needsSurvey = form.type !== ReportType.DashboardSummary;

    const request: SaveReportRequest = {
      name: form.name.trim(),
      description: form.description.trim() || null,
      type: form.type,
      format: form.format,
      schedule: form.schedule,
      activateImmediately: form.activateImmediately,
      // فقط نظرسنجی برای انواعی که به آن وابسته‌اند ارسال می‌شود تا داده‌ی
      // کهله روی تعریف گزارش باقی نماند.
      surveyId: needsSurvey ? form.surveyId : null,
      from: localDateTimeToIso(form.from),
      to: localDateTimeToIso(form.to),
      orgUnitId: form.orgUnitId || null,
      includeDescendants: form.includeDescendants,
      retentionCount: form.retentionCount
    };

    try {
      if (isEdit && reportId) {
        await updateMutation.mutateAsync({ id: reportId, request });
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

  const needsSurvey = form.type !== ReportType.DashboardSummary;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.reports.editReport : t.reports.newReport}
      description={t.reports.privacyNote}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="report-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="report-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.reports.reportName} htmlFor="reportName" required error={errors.name}>
          <Input
            id="reportName"
            value={form.name}
            onChange={(event) => updateField('name', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.reports.reportDescription} htmlFor="reportDescription">
          <Input
            id="reportDescription"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.reports.reportType} htmlFor="reportType" required>
          <Select
            id="reportType"
            value={String(form.type)}
            onChange={(event) => updateField('type', Number(event.target.value))}
            options={typeOptions(t.reports)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.reports.reportFormat} htmlFor="reportFormat" required>
          <Select
            id="reportFormat"
            value={String(form.format)}
            onChange={(event) => updateField('format', Number(event.target.value))}
            options={formatOptions(t.reports)}
            disabled={isSaving}
          />
        </FormField>

        {needsSurvey && (
          <FormField
            label={t.reports.survey}
            htmlFor="reportSurvey"
            required
            error={errors.surveyId}
            className="sm:col-span-2"
          >
            <Select
              id="reportSurvey"
              value={form.surveyId}
              onChange={(event) => updateField('surveyId', event.target.value)}
              options={surveyOptions}
              placeholder={t.reports.selectSurvey}
              disabled={isSaving}
            />
          </FormField>
        )}

        <FormField label={t.reports.reportSchedule} htmlFor="reportSchedule" required>
          <Select
            id="reportSchedule"
            value={String(form.schedule)}
            onChange={(event) => updateField('schedule', Number(event.target.value))}
            options={scheduleOptions(t.reports)}
            disabled={isSaving}
          />
        </FormField>

        <FormField
          label={t.reports.retentionCount}
          htmlFor="reportRetentionCount"
          required
          error={errors.retentionCount}
          hint={t.reports.retentionCountHint}
        >
          <Input
            id="reportRetentionCount"
            type="number"
            min={1}
            max={100}
            value={String(form.retentionCount)}
            onChange={(event) => updateField('retentionCount', Number(event.target.value))}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.reports.windowFrom} htmlFor="reportFrom">
          <DateTimePicker
            id="reportFrom"
            value={form.from}
            onChange={(value) => updateField('from', value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.reports.windowTo} htmlFor="reportTo" error={errors.to}>
          <DateTimePicker
            id="reportTo"
            value={form.to}
            onChange={(value) => updateField('to', value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.reports.orgUnit} htmlFor="reportOrgUnit">
          <Select
            id="reportOrgUnit"
            value={form.orgUnitId}
            onChange={(event) => updateField('orgUnitId', event.target.value)}
            options={orgUnitOptions}
            placeholder={t.common.all}
            disabled={isSaving}
          />
        </FormField>

        <label className="flex cursor-pointer items-center gap-3 rounded-md border p-3">
          <Checkbox
            checked={form.includeDescendants}
            onCheckedChange={(checked) => updateField('includeDescendants', checked)}
            disabled={isSaving || !form.orgUnitId}
          />
          <span className="flex flex-col gap-0.5">
            <span className="text-sm font-medium">{t.reports.includeDescendants}</span>
          </span>
        </label>

        <label className="col-span-full flex cursor-pointer items-center gap-3 rounded-md border p-3">
          <Checkbox
            checked={form.activateImmediately}
            onCheckedChange={(checked) => updateField('activateImmediately', checked)}
            disabled={isSaving}
          />
          <span className="flex flex-col gap-0.5">
            <span className="text-sm font-medium">{t.reports.activateImmediately}</span>
            <span className="text-xs text-muted-foreground">{t.reports.activateImmediatelyHint}</span>
          </span>
        </label>

        <p className="col-span-full text-xs text-muted-foreground">{t.reports.privacyNote}</p>
      </form>
    </Dialog>
  );
}

interface ReportFormState {
  name: string;
  description: string;
  type: ReportType;
  format: ReportFormat;
  schedule: ReportSchedule;
  activateImmediately: boolean;
  surveyId: string;
  from: string;
  to: string;
  orgUnitId: string;
  includeDescendants: boolean;
  retentionCount: number;
}

function createEmptyForm(): ReportFormState {
  return {
    name: '',
    description: '',
    type: ReportType.SurveyAnalytics,
    format: ReportFormat.Pdf,
    schedule: ReportSchedule.OneTime,
    activateImmediately: true,
    surveyId: '',
    from: '',
    to: '',
    orgUnitId: '',
    includeDescendants: true,
    retentionCount: 10
  };
}

function toForm(existing: ReportDefinition): ReportFormState {
  return {
    name: existing.name,
    description: existing.description ?? '',
    type: existing.type,
    format: existing.format,
    schedule: existing.schedule,
    // پرچم فعال‌سازی بر اساس وضعیت فعلی تعریف پر می‌شود تا ویرایش،
    // گزارشی که از قبل فعال است را به پیش‌نویس تبدیل نکند.
    activateImmediately: existing.status === ReportStatus.Active,
    surveyId: existing.surveyId ?? '',
    from: isoToLocalDateTime(existing.from),
    to: isoToLocalDateTime(existing.to),
    orgUnitId: existing.orgUnitId ?? '',
    includeDescendants: existing.includeDescendants,
    retentionCount: existing.retentionCount
  };
}

function typeOptions(t: Dictionary['reports']) {
  return [
    { value: String(ReportType.SurveyAnalytics), label: t.typeSurveyAnalytics },
    { value: String(ReportType.DashboardSummary), label: t.typeDashboardSummary },
    { value: String(ReportType.BenchmarkComparison), label: t.typeBenchmarkComparison }
  ];
}

function formatOptions(t: Dictionary['reports']) {
  return [
    { value: String(ReportFormat.Pdf), label: t.formatPdf },
    { value: String(ReportFormat.Excel), label: t.formatExcel }
  ];
}

function scheduleOptions(t: Dictionary['reports']) {
  return [
    { value: String(ReportSchedule.OneTime), label: t.scheduleOneTime },
    { value: String(ReportSchedule.Daily), label: t.scheduleDaily },
    { value: String(ReportSchedule.Weekly), label: t.scheduleWeekly },
    { value: String(ReportSchedule.Monthly), label: t.scheduleMonthly }
  ];
}
