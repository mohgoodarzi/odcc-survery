import { useEffect, useState, type FormEvent } from 'react';

import {
  IntegrationAuthType,
  IntegrationType,
  type IntegrationEndpointDto,
  type SaveIntegrationEndpointRequest
} from '@/api/integrations';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useCreateIntegrationEndpoint,
  useUpdateIntegrationEndpoint
} from '@/api/integrationsHooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import type { Dictionary } from '@/i18n/types';

interface IntegrationEndpointDialogProps {
  open: boolean;
  onClose: () => void;
  endpointId?: string | null;
  existing?: IntegrationEndpointDto | null;
}

/**
 * دیالوگ ایجاد/ویرایش اندپوینت یکپارچه‌سازی. مقدار راز هرگز وارد نمی‌شود —
 * فقط نام منطقی آن که در پیکربندی سرور lookup می‌شود.
 */
export function IntegrationEndpointDialog({
  open,
  onClose,
  endpointId,
  existing
}: IntegrationEndpointDialogProps) {
  const { t } = useLanguage();
  const isEdit = !!endpointId;

  const createMutation = useCreateIntegrationEndpoint();
  const updateMutation = useUpdateIntegrationEndpoint();
  const isSaving = createMutation.isPending || updateMutation.isPending;

  const [form, setForm] = useState<EndpointFormState>(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setForm(existing ? toForm(existing) : createEmptyForm());
    setErrors({});
    setFormError(undefined);
  }, [open, existing]);

  function updateField<K extends keyof EndpointFormState>(field: K, value: EndpointFormState[K]) {
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

    if (!form.name.trim()) next.name = t.integrations.endpointName + ' ' + t.common.required;
    if (!form.code.trim()) next.code = t.integrations.code + ' ' + t.common.required;
    if (!form.url.trim()) next.url = t.integrations.url + ' ' + t.common.required;

    if (form.timeoutSeconds < 1) next.timeoutSeconds = t.integrations.timeoutSeconds + ' ≥ 1';
    if (form.maxRetries < 0) next.maxRetries = t.integrations.maxRetries + ' ≥ 0';

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);

    const request: SaveIntegrationEndpointRequest = {
      name: form.name.trim(),
      code: form.code.trim(),
      description: form.description.trim() || null,
      type: form.type,
      url: form.url.trim(),
      httpMethod: form.httpMethod,
      authType: form.authType,
      secretRef: form.secretRef.trim() || null,
      authHeaderName: form.authHeaderName.trim() || null,
      timeoutSeconds: form.timeoutSeconds,
      maxRetries: form.maxRetries,
      subscribedEvents: form.subscribedEventsRaw
        .split('\n')
        .map((line) => line.trim())
        .filter(Boolean),
      activateImmediately: form.activateImmediately
    };

    try {
      if (isEdit && endpointId) {
        await updateMutation.mutateAsync({ id: endpointId, request });
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

  const needsSecret = form.authType !== IntegrationAuthType.None;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.integrations.editEndpoint : t.integrations.newEndpoint}
      description={t.integrations.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="endpoint-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="endpoint-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField label={t.integrations.endpointName} htmlFor="endpointName" required error={errors.name}>
          <Input
            id="endpointName"
            value={form.name}
            onChange={(event) => updateField('name', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.integrations.code} htmlFor="endpointCode" required error={errors.code}>
          <Input
            id="endpointCode"
            value={form.code}
            onChange={(event) => updateField('code', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.integrations.endpointDescription}
          htmlFor="endpointDescription"
          className="sm:col-span-2"
        >
          <Input
            id="endpointDescription"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.integrations.type} htmlFor="endpointType">
          <Select
            id="endpointType"
            value={String(form.type)}
            onChange={(event) => updateField('type', Number(event.target.value) as IntegrationType)}
            options={integrationTypeOptions(t)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.integrations.httpMethod} htmlFor="endpointMethod">
          <Select
            id="endpointMethod"
            value={form.httpMethod}
            onChange={(event) => updateField('httpMethod', event.target.value)}
            options={httpMethodOptions()}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.integrations.url} htmlFor="endpointUrl" required error={errors.url} className="sm:col-span-2">
          <Input
            id="endpointUrl"
            value={form.url}
            onChange={(event) => updateField('url', event.target.value)}
            disabled={isSaving}
            dir="ltr"
            placeholder="https://example.com/webhook"
          />
        </FormField>

        <FormField label={t.integrations.authType} htmlFor="endpointAuthType">
          <Select
            id="endpointAuthType"
            value={String(form.authType)}
            onChange={(event) =>
              updateField('authType', Number(event.target.value) as IntegrationAuthType)
            }
            options={authTypeOptions(t)}
            disabled={isSaving}
          />
        </FormField>

        <FormField
          label={t.integrations.secretRef}
          htmlFor="endpointSecretRef"
          hint={t.integrations.secretNote}
        >
          <Input
            id="endpointSecretRef"
            value={form.secretRef}
            onChange={(event) => updateField('secretRef', event.target.value)}
            disabled={isSaving || !needsSecret}
            dir="ltr"
            placeholder={needsSecret ? 'Integrations__Secrets__MyEndpoint' : t.integrations.authNone}
          />
        </FormField>

        <FormField label={t.integrations.authHeaderName} htmlFor="endpointAuthHeader">
          <Input
            id="endpointAuthHeader"
            value={form.authHeaderName}
            onChange={(event) => updateField('authHeaderName', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.integrations.timeoutSeconds} htmlFor="endpointTimeout" error={errors.timeoutSeconds}>
          <Input
            id="endpointTimeout"
            type="number"
            min={1}
            value={form.timeoutSeconds}
            onChange={(event) => updateField('timeoutSeconds', Number(event.target.value))}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.integrations.maxRetries} htmlFor="endpointRetries" error={errors.maxRetries}>
          <Input
            id="endpointRetries"
            type="number"
            min={0}
            value={form.maxRetries}
            onChange={(event) => updateField('maxRetries', Number(event.target.value))}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.integrations.subscribedEvents}
          htmlFor="endpointEvents"
          hint={t.integrations.subscribedEventsHint}
          className="sm:col-span-2"
        >
          <textarea
            id="endpointEvents"
            value={form.subscribedEventsRaw}
            onChange={(event) => updateField('subscribedEventsRaw', event.target.value)}
            disabled={isSaving}
            rows={3}
            className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
            dir="ltr"
          />
        </FormField>

        <label className="col-span-full flex cursor-pointer items-center gap-3 rounded-md border p-3">
          <Checkbox
            checked={form.activateImmediately}
            onCheckedChange={(checked) => updateField('activateImmediately', checked)}
            disabled={isSaving}
          />
          <span className="flex flex-col gap-0.5">
            <span className="text-sm font-medium">{t.integrations.activateImmediately}</span>
            <span className="text-xs text-muted-foreground">{t.integrations.activateImmediatelyHint}</span>
          </span>
        </label>

        <p className="col-span-full text-xs text-muted-foreground">{t.integrations.privacyNote}</p>
      </form>
    </Dialog>
  );
}

interface EndpointFormState {
  name: string;
  code: string;
  description: string;
  type: IntegrationType;
  url: string;
  httpMethod: string;
  authType: IntegrationAuthType;
  secretRef: string;
  authHeaderName: string;
  timeoutSeconds: number;
  maxRetries: number;
  subscribedEventsRaw: string;
  activateImmediately: boolean;
}

function createEmptyForm(): EndpointFormState {
  return {
    name: '',
    code: '',
    description: '',
    type: IntegrationType.OutboundWebhook,
    url: '',
    httpMethod: 'POST',
    authType: IntegrationAuthType.HmacSignature,
    secretRef: '',
    authHeaderName: '',
    timeoutSeconds: 30,
    maxRetries: 3,
    subscribedEventsRaw: '',
    activateImmediately: true
  };
}

function toForm(existing: IntegrationEndpointDto): EndpointFormState {
  return {
    name: existing.name,
    code: existing.code,
    description: existing.description ?? '',
    type: existing.type,
    url: existing.url,
    httpMethod: existing.httpMethod,
    authType: existing.authType,
    secretRef: existing.secretRef ?? '',
    authHeaderName: existing.authHeaderName ?? '',
    timeoutSeconds: existing.timeoutSeconds,
    maxRetries: existing.maxRetries,
    subscribedEventsRaw: existing.subscribedEvents.join('\n'),
    activateImmediately: existing.isActive
  };
}

export function integrationTypeOptions(t: Dictionary) {
  return [
    { value: String(IntegrationType.OutboundWebhook), label: t.integrations.typeOutboundWebhook },
    { value: String(IntegrationType.InboundWebhook), label: t.integrations.typeInboundWebhook },
    { value: String(IntegrationType.HrSync), label: t.integrations.typeHrSync },
    { value: String(IntegrationType.Sso), label: t.integrations.typeSso },
    { value: String(IntegrationType.AiProvider), label: t.integrations.typeAiProvider }
  ];
}

export function authTypeOptions(t: Dictionary) {
  return [
    { value: String(IntegrationAuthType.None), label: t.integrations.authNone },
    { value: String(IntegrationAuthType.HmacSignature), label: t.integrations.authHmac },
    { value: String(IntegrationAuthType.BearerToken), label: t.integrations.authBearer },
    { value: String(IntegrationAuthType.ApiKey), label: t.integrations.authApiKey },
    { value: String(IntegrationAuthType.Basic), label: t.integrations.authBasic }
  ];
}

function httpMethodOptions() {
  return ['POST', 'PUT', 'PATCH'].map((method) => ({ value: method, label: method }));
}
