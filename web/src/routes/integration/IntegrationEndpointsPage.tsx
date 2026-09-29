import { useState } from 'react';
import { Plug, Plus, Pencil, Search, Archive, Activity, Pause, FlaskConical } from 'lucide-react';

import { IntegrationType, type IntegrationEndpointDto } from '@/api/integrations';
import { buildInboundWebhookPath } from '@/api/integrations';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useActivateIntegrationEndpoint,
  useArchiveIntegrationEndpoint,
  useDeactivateIntegrationEndpoint,
  useIntegrationEndpointSearch,
  useTestIntegrationEndpoint
} from '@/api/integrationsHooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { Pagination } from '@/routes/identity/UsersPage';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';
import {
  authTypeOptions,
  integrationTypeOptions,
  IntegrationEndpointDialog
} from './IntegrationEndpointDialog';

/**
 * صفحه‌ی مدیریت اندپوینت‌های یکپارچه‌سازی: فهرست صفحه‌بندی‌شده با فیلتر بر اساس
 * نوع و وضعیت فعالیت؛ ایجاد/ویرایش، فعال/غیرفعال‌سازی، آزمون اتصال و بایگانی.
 */
export function IntegrationEndpointsPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [type, setType] = useState<number | null>(null);
  const [isActive, setIsActive] = useState<boolean | null>(null);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<IntegrationEndpointDto | null>(null);
  const [activateTarget, setActivateTarget] = useState<string | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<string | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<string | null>(null);
  const [testTarget, setTestTarget] = useState<IntegrationEndpointDto | null>(null);
  const [testPayload, setTestPayload] = useState('{}');

  const request = { searchText, type, isActive, includeArchived, page, pageSize: 20 };

  const { data, isLoading, isError, error, refetch } = useIntegrationEndpointSearch(request);

  const activateMutation = useActivateIntegrationEndpoint();
  const deactivateMutation = useDeactivateIntegrationEndpoint();
  const archiveMutation = useArchiveIntegrationEndpoint();
  const testMutation = useTestIntegrationEndpoint();

  const canManage = hasPermission(Permissions.Integrations.Manage);

  const endpoints = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.integrations.title}
        description={t.integrations.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.integrations.newEndpoint}
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute start-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={searchText ?? ''}
            onChange={(event) => {
              setSearchText(event.target.value || null);
              setPage(1);
            }}
            placeholder={t.integrations.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={type === null ? 'all' : String(type)}
          onChange={(event) => {
            setType(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={integrationTypeOptions(t)}
          aria-label={t.integrations.type}
        />

        <Select
          value={isActive === null ? 'all' : String(isActive)}
          onChange={(event) => {
            setIsActive(event.target.value === 'all' ? null : event.target.value === 'true');
            setPage(1);
          }}
          options={activeFilterOptions(t)}
          aria-label={t.integrations.isActive}
        />

        <label className="flex cursor-pointer items-center gap-2 rounded-md border p-2.5 text-sm">
          <input
            type="checkbox"
            checked={includeArchived}
            onChange={(event) => {
              setIncludeArchived(event.target.checked);
              setPage(1);
            }}
            className="size-4 accent-primary"
          />
          {t.integrations.includeArchived}
        </label>
      </div>

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={6} />
        ) : endpoints.length === 0 ? (
          <EmptyState
            title={t.integrations.noEndpoints}
            description={t.integrations.noEndpointsDescription}
            icon={<Plug className="size-10" />}
            action={
              canManage
                ? { label: t.integrations.newEndpoint, onClick: () => setCreateOpen(true) }
                : undefined
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.integrations.name}</TableHead>
                <TableHead>{t.integrations.type}</TableHead>
                <TableHead>{t.integrations.authType}</TableHead>
                <TableHead>{t.integrations.lastDeliveryAt}</TableHead>
                <TableHead>{t.integrations.status}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {endpoints.map((endpoint) => (
                <TableRow key={endpoint.id}>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <span className="font-medium">{endpoint.name}</span>
                      <span className="text-xs text-muted-foreground" dir="ltr">
                        {endpoint.code} · {endpoint.httpMethod} {endpoint.url}
                      </span>
                      {endpoint.type === IntegrationType.InboundWebhook && (
                        <span className="text-xs text-muted-foreground" dir="ltr">
                          {buildInboundWebhookPath(endpoint.code)}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {integrationTypeLabel(t, endpoint.type)}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    <div className="flex flex-col gap-0.5">
                      <span>{authTypeLabel(t, endpoint.authType)}</span>
                      {endpoint.secretRef && (
                        <Badge variant={endpoint.secretConfigured ? 'success' : 'destructive'}>
                          {endpoint.secretConfigured
                            ? t.integrations.secretConfigured
                            : t.integrations.secretMissing}
                        </Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {endpoint.lastDeliveryAt ? formatDateTime(endpoint.lastDeliveryAt, culture) : '—'}
                  </TableCell>
                  <TableCell>
                    {endpoint.isActive ? (
                      <Badge variant="success">{t.common.active}</Badge>
                    ) : (
                      <Badge variant="outline">{t.common.inactive}</Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canManage && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => {
                              setTestTarget(endpoint);
                              setTestPayload('{}');
                            }}
                            aria-label={t.integrations.testConnection}
                            title={t.integrations.testConnection}
                          >
                            <FlaskConical className="size-4" />
                          </Button>

                          {endpoint.isActive ? (
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setDeactivateTarget(endpoint.id)}
                              aria-label={t.integrations.deactivate}
                              title={t.integrations.deactivate}
                            >
                              <Pause className="size-4" />
                            </Button>
                          ) : (
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setActivateTarget(endpoint.id)}
                              aria-label={t.integrations.activate}
                              title={t.integrations.activate}
                            >
                              <Activity className="size-4" />
                            </Button>
                          )}

                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setEditing(endpoint)}
                            aria-label={t.common.edit}
                          >
                            <Pencil className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setArchiveTarget(endpoint.id)}
                            aria-label={t.integrations.archive}
                          >
                            <Archive className="size-4" />
                          </Button>
                        </>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {endpoints.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      <p className="mt-4 flex items-center gap-2 text-xs text-muted-foreground">
        <Plug className="size-4" />
        {t.integrations.secretNote}
      </p>

      {createOpen && (
        <IntegrationEndpointDialog open={createOpen} onClose={() => setCreateOpen(false)} />
      )}

      {editing && (
        <IntegrationEndpointDialog
          open={!!editing}
          onClose={() => setEditing(null)}
          endpointId={editing.id}
          existing={editing}
        />
      )}

      {/* آزمون اتصال */}
      {testTarget && (
        <Dialog
          open
          onClose={() => setTestTarget(null)}
          title={t.integrations.testConnection}
          description={`${testTarget.name} · ${testTarget.url}`}
          footer={
            <>
              <Button
                variant="outline"
                onClick={() => setTestTarget(null)}
                disabled={testMutation.isPending}
              >
                {t.common.close}
              </Button>
              <Button
                onClick={() =>
                  testMutation.mutate({ id: testTarget.id, request: { payloadJson: testPayload } })
                }
                disabled={testMutation.isPending}
              >
                {testMutation.isPending ? t.integrations.testing : t.integrations.testConnection}
              </Button>
            </>
          }
        >
          <div className="flex flex-col gap-4">
            {testMutation.isError && (
              <div
                className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
                role="alert"
              >
                {testMutation.error instanceof ApiError
                  ? t.errors.fromCode(testMutation.error.code)
                  : t.errors.generic}
              </div>
            )}

            <FormField
              label={t.integrations.testPayload}
              htmlFor="testPayload"
              hint={t.integrations.testPayloadHint}
            >
              <textarea
                id="testPayload"
                value={testPayload}
                onChange={(event) => setTestPayload(event.target.value)}
                disabled={testMutation.isPending}
                rows={5}
                className="flex w-full rounded-md border border-input bg-background px-3 py-2 font-mono text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
                dir="ltr"
              />
            </FormField>

            {testMutation.data && (
              <div className="rounded-md border p-3 text-sm">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-muted-foreground">{t.integrations.status}</span>
                  <Badge variant={testMutation.data.success ? 'success' : 'destructive'}>
                    {testMutation.data.success
                      ? t.integrations.testResultSuccess
                      : t.integrations.testResultFailure}
                  </Badge>
                </div>
                <div className="mt-1 flex items-center justify-between gap-2" dir="ltr">
                  <span className="text-muted-foreground">{t.integrations.responseStatusCode}</span>
                  <span className="font-medium">{testMutation.data.statusCode ?? '—'}</span>
                </div>
                <div className="mt-1 flex items-center justify-between gap-2" dir="ltr">
                  <span className="text-muted-foreground">ms</span>
                  <span className="font-medium">{testMutation.data.elapsedMilliseconds}</span>
                </div>
                {testMutation.data.error && (
                  <p className="mt-2 text-xs text-destructive" dir="ltr">
                    {testMutation.data.error}
                  </p>
                )}
              </div>
            )}
          </div>
        </Dialog>
      )}

      <ConfirmDialog
        open={!!activateTarget}
        onClose={() => setActivateTarget(null)}
        title={t.integrations.activate}
        description={t.integrations.activateImmediatelyHint}
        confirmLabel={t.integrations.activate}
        destructive={false}
        isPending={activateMutation.isPending}
        error={
          activateMutation.error instanceof ApiError
            ? t.errors.fromCode(activateMutation.error.code)
            : activateMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!activateTarget) return;
          activateMutation.mutate(activateTarget, { onSuccess: () => setActivateTarget(null) });
        }}
      />

      <ConfirmDialog
        open={!!deactivateTarget}
        onClose={() => setDeactivateTarget(null)}
        title={t.integrations.deactivateConfirmTitle}
        description={t.integrations.deactivateConfirmDescription}
        confirmLabel={t.integrations.deactivate}
        destructive={false}
        isPending={deactivateMutation.isPending}
        error={
          deactivateMutation.error instanceof ApiError
            ? t.errors.fromCode(deactivateMutation.error.code)
            : deactivateMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!deactivateTarget) return;
          deactivateMutation.mutate(deactivateTarget, { onSuccess: () => setDeactivateTarget(null) });
        }}
      />

      <ConfirmDialog
        open={!!archiveTarget}
        onClose={() => setArchiveTarget(null)}
        title={t.integrations.archiveConfirmTitle}
        description={t.integrations.archiveConfirmDescription}
        confirmLabel={t.integrations.archive}
        destructive
        isPending={archiveMutation.isPending}
        error={
          archiveMutation.error instanceof ApiError
            ? t.errors.fromCode(archiveMutation.error.code)
            : archiveMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!archiveTarget) return;
          archiveMutation.mutate(archiveTarget, { onSuccess: () => setArchiveTarget(null) });
        }}
      />
    </AppLayout>
  );
}

function activeFilterOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: 'true', label: t.common.active },
    { value: 'false', label: t.common.inactive }
  ];
}

function integrationTypeLabel(t: Dictionary, type: number): string {
  return integrationTypeOptions(t).find((option) => Number(option.value) === type)?.label
    ?? t.integrations.typeOutboundWebhook;
}

function authTypeLabel(t: Dictionary, type: number): string {
  return authTypeOptions(t).find((option) => Number(option.value) === type)?.label
    ?? t.integrations.authNone;
}
