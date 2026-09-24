import { useState } from 'react';
import { Users } from 'lucide-react';

import { DistributionStatus } from '@/api/campaigns';
import { ApiError } from '@/api/client';
import { useCampaignDistributions } from '@/api/hooks';
import { useLanguage } from '@/i18n/LanguageProvider';
import { Dialog } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { Pagination } from '@/routes/identity/UsersPage';
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';

interface CampaignDistributionsDialogProps {
  open: boolean;
  onClose: () => void;
  campaignId: string;
  campaignTitle: string;
  totalDistributions: number;
}

/**
 * دیالوگ پیگیری گیرندگان یک کمپین: وضعیت ارسال، پاسخ و یادآورهای هر گیرنده.
 * فقط پس از اجرای کمپین ردیف‌هایی برای نمایش وجود دارد.
 */
export function CampaignDistributionsDialog({
  open,
  onClose,
  campaignId,
  campaignTitle,
  totalDistributions
}: CampaignDistributionsDialogProps) {
  const { t, culture } = useLanguage();

  const [status, setStatus] = useState<number | null>(null);
  const [page, setPage] = useState(1);

  const { data, isLoading, isError, error, refetch } = useCampaignDistributions({
    campaignId,
    status,
    page
  });

  const distributions = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`${t.campaigns.distributionsTitle} — ${campaignTitle}`}
      description={t.campaigns.distributionsDescription}
      size="lg"
    >
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => {
            setStatus(event.target.value === 'all' ? null : Number(event.target.value));
            setPage(1);
          }}
          options={statusOptions(t.campaigns)}
          aria-label={t.campaigns.status}
        />

        <span className="text-sm text-muted-foreground">
          {t.campaigns.totalDistributions}: {totalDistributions}
        </span>
      </div>

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={6} />
        ) : distributions.length === 0 ? (
          <EmptyState
            title={t.campaigns.noDistributions}
            description={t.campaigns.noDistributionsDescription}
            icon={<Users className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.campaigns.recipient}</TableHead>
                <TableHead>{t.campaigns.email}</TableHead>
                <TableHead>{t.campaigns.status}</TableHead>
                <TableHead>{t.campaigns.sentAt}</TableHead>
                <TableHead>{t.campaigns.respondedAt}</TableHead>
                <TableHead>{t.campaigns.reminderCount}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {distributions.map((distribution) => (
                <TableRow key={distribution.id}>
                  <TableCell className="font-medium">
                    {distribution.employeeName ?? '—'}
                  </TableCell>
                  <TableCell dir="ltr" className="text-muted-foreground">
                    {distribution.workEmail ?? '—'}
                  </TableCell>
                  <TableCell>
                    <DistributionStatusBadge status={distribution.status} />
                    {distribution.failureReason && (
                      <span className="ms-2 text-xs text-destructive">{distribution.failureReason}</span>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {distribution.sentAt ? formatDateTime(distribution.sentAt, culture) : '—'}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {distribution.respondedAt ? formatDateTime(distribution.respondedAt, culture) : '—'}
                  </TableCell>
                  <TableCell dir="ltr">{distribution.reminderCount}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {distributions.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}
    </Dialog>
  );
}

function statusOptions(t: Dictionary['campaigns']) {
  return [
    { value: 'all', label: t.all },
    { value: String(DistributionStatus.Pending), label: t.distPending },
    { value: String(DistributionStatus.Sent), label: t.distSent },
    { value: String(DistributionStatus.Responded), label: t.distResponded },
    { value: String(DistributionStatus.Failed), label: t.distFailed }
  ];
}

function DistributionStatusBadge({ status }: { status: DistributionStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case DistributionStatus.Pending:
      return <Badge variant="outline">{t.campaigns.distPending}</Badge>;
    case DistributionStatus.Sent:
      return <Badge variant="warning">{t.campaigns.distSent}</Badge>;
    case DistributionStatus.Responded:
      return <Badge variant="success">{t.campaigns.distResponded}</Badge>;
    default:
      return <Badge variant="destructive">{t.campaigns.distFailed}</Badge>;
  }
}
