import { useState } from 'react';
import { Send, Search, RotateCw } from 'lucide-react';

import { DeliveryStatus, type WebhookDeliveryDto } from '@/api/integrations';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useRetryWebhookDelivery,
  useWebhookDeliverySearch
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
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';

/**
 * صفحه‌ی تاریخچه‌ی تحویل وب‌هوک‌های خروجی: فهرست صفحه‌بندی‌شده با فیلتر بر اساس
 * وضعیت و اندپوینت، نمایش payload و تلاش مجدد تحویل‌های ناموفق.
 */
export function WebhookDeliveriesPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [eventType, setEventType] = useState<string | null>(null);
  const [retryableOnly, setRetryableOnly] = useState(false);
  const [page, setPage] = useState(1);

  const [retryTarget, setRetryTarget] = useState<string | null>(null);
  const [payloadDelivery, setPayloadDelivery] = useState<WebhookDeliveryDto | null>(null);

  const request = { searchText, status, eventType, retryableOnly, page, pageSize: 20 };

  const { data, isLoading, isError, error, refetch } = useWebhookDeliverySearch(request);

  const retryMutation = useRetryWebhookDelivery();

  const canRetry = hasPermission(Permissions.Integrations.Retry);

  const deliveries = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader title={t.integrations.deliveries} description={t.integrations.description} />

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
          value={status === null ? 'all' : String(status)}
          onChange={(event) => {
            setStatus(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={deliveryStatusOptions(t)}
          aria-label={t.integrations.status}
        />

        <Input
          value={eventType ?? ''}
          onChange={(event) => {
            setEventType(event.target.value || null);
            setPage(1);
          }}
          placeholder={t.integrations.eventType}
          dir="ltr"
          className="w-48"
        />

        <label className="flex cursor-pointer items-center gap-2 rounded-md border p-2.5 text-sm">
          <input
            type="checkbox"
            checked={retryableOnly}
            onChange={(event) => {
              setRetryableOnly(event.target.checked);
              setPage(1);
            }}
            className="size-4 accent-primary"
          />
          {t.integrations.retryableOnly}
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
        ) : deliveries.length === 0 ? (
          <EmptyState
            title={t.integrations.noDeliveries}
            description={t.integrations.noDeliveriesDescription}
            icon={<Send className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.integrations.eventType}</TableHead>
                <TableHead>{t.integrations.endpoint}</TableHead>
                <TableHead>{t.integrations.status}</TableHead>
                <TableHead>{t.integrations.attemptCount}</TableHead>
                <TableHead>{t.integrations.lastAttemptAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {deliveries.map((delivery) => (
                <TableRow key={delivery.id}>
                  <TableCell>
                    <button
                      type="button"
                      className="flex flex-col gap-0.5 text-start"
                      onClick={() => setPayloadDelivery(delivery)}
                    >
                      <span className="font-medium" dir="ltr">
                        {delivery.eventType}
                      </span>
                      <span className="text-xs text-muted-foreground underline" dir="ltr">
                        {delivery.eventId}
                      </span>
                    </button>
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {delivery.endpointCode}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      <DeliveryStatusBadge status={delivery.status} />
                      {delivery.lastError && (
                        <span className="max-w-xs truncate text-xs text-destructive" title={delivery.lastError} dir="ltr">
                          {delivery.lastError}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {delivery.attemptCount}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {delivery.lastAttemptAt ? formatDateTime(delivery.lastAttemptAt, culture) : '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canRetry && delivery.status !== DeliveryStatus.Succeeded && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setRetryTarget(delivery.id)}
                          aria-label={t.integrations.retry}
                          title={t.integrations.retry}
                        >
                          <RotateCw className="size-4" />
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {deliveries.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {payloadDelivery && (
        <Dialog
          open
          onClose={() => setPayloadDelivery(null)}
          title={t.integrations.payload}
          description={`${payloadDelivery.endpointCode} · ${payloadDelivery.eventType}`}
        >
          <div className="flex flex-col gap-3">
            <div className="grid grid-cols-2 gap-2 text-sm">
              <div>
                <span className="text-muted-foreground">{t.integrations.eventId}</span>
                <p className="font-mono text-xs" dir="ltr">
                  {payloadDelivery.eventId}
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">{t.integrations.responseStatusCode}</span>
                <p className="font-medium" dir="ltr">
                  {payloadDelivery.responseStatusCode ?? '—'}
                </p>
              </div>
            </div>

            <pre
              className="max-h-80 overflow-auto rounded-md border bg-muted/30 p-3 font-mono text-xs"
              dir="ltr"
            >
              {payloadDelivery.payloadJson ?? '{}'}
            </pre>
          </div>
        </Dialog>
      )}

      <ConfirmDialog
        open={!!retryTarget}
        onClose={() => setRetryTarget(null)}
        title={t.integrations.retryConfirmTitle}
        description={t.integrations.retryConfirmDescription}
        confirmLabel={t.integrations.retry}
        destructive={false}
        isPending={retryMutation.isPending}
        error={
          retryMutation.error instanceof ApiError
            ? t.errors.fromCode(retryMutation.error.code)
            : retryMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!retryTarget) return;
          retryMutation.mutate(retryTarget, { onSuccess: () => setRetryTarget(null) });
        }}
      />
    </AppLayout>
  );
}

function deliveryStatusOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(DeliveryStatus.Pending), label: t.integrations.statusPending },
    { value: String(DeliveryStatus.Succeeded), label: t.integrations.statusSucceeded },
    { value: String(DeliveryStatus.Failed), label: t.integrations.statusFailed }
  ];
}

function DeliveryStatusBadge({ status }: { status: DeliveryStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case DeliveryStatus.Succeeded:
      return <Badge variant="success">{t.integrations.statusSucceeded}</Badge>;
    case DeliveryStatus.Failed:
      return <Badge variant="destructive">{t.integrations.statusFailed}</Badge>;
    default:
      return <Badge variant="warning">{t.integrations.statusPending}</Badge>;
  }
}
