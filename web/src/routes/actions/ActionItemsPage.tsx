import { useState } from 'react';
import { ClipboardList, Search, AlertTriangle } from 'lucide-react';

import { ActionItemStatus, EscalationLevel } from '@/api/actions';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useActionItemSearch } from '@/api/actionsHooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { Pagination } from '@/routes/identity/UsersPage';
import { formatDateTime } from '@/i18n/format';
import type { Dictionary } from '@/i18n/types';
import { ActionItemDetailDialog } from './ActionItemDetailDialog';

/**
 * صفحه‌ی آیتم‌های اقدام: فهرست کارها با فیلتر «کارهای من»، وضعیت و
 * سررسیده‌شده‌ها. کاربر می‌تواند با کلیک روی هر ردیف، جزئیات آن را ببیند و
 * (در صورت مجوز) وضعیتش را تغییر دهد.
 */
export function ActionItemsPage() {
  const { t, culture } = useLanguage();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [assignedToMe, setAssignedToMe] = useState(true);
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  const request = {
    searchText,
    status,
    assignedToMe,
    overdueOnly,
    page,
    pageSize: 20
  };

  const { data, isLoading, isError, error, refetch } = useActionItemSearch(request);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleStatusChange(value: string) {
    setStatus(value === 'all' ? null : Number(value));
    setPage(1);
  }

  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.actions.itemTitle}
        description={t.actions.description}
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute start-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={searchText ?? ''}
            onChange={(event) => handleSearch(event.target.value)}
            placeholder={t.actions.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => handleStatusChange(event.target.value)}
          options={statusOptions(t)}
          aria-label={t.actions.status}
        />

        <label className="flex cursor-pointer items-center gap-2 rounded-md border p-2.5 text-sm">
          <input
            type="checkbox"
            checked={assignedToMe}
            onChange={(event) => {
              setAssignedToMe(event.target.checked);
              setPage(1);
            }}
            className="size-4 accent-primary"
          />
          {t.actions.assignedToMe}
        </label>

        <label className="flex cursor-pointer items-center gap-2 rounded-md border p-2.5 text-sm">
          <input
            type="checkbox"
            checked={overdueOnly}
            onChange={(event) => {
              setOverdueOnly(event.target.checked);
              setPage(1);
            }}
            className="size-4 accent-primary"
          />
          {t.actions.overdueOnly}
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
        ) : items.length === 0 ? (
          <EmptyState
            title={t.actions.noItems}
            description={t.actions.noItemsDescription}
            icon={<ClipboardList className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.actions.itemTitle}</TableHead>
                <TableHead>{t.actions.planTitle}</TableHead>
                <TableHead>{t.actions.status}</TableHead>
                <TableHead>{t.actions.assignee}</TableHead>
                <TableHead>{t.actions.dueDate}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>
                    <button
                      type="button"
                      className="flex flex-col gap-0.5 text-start"
                      onClick={() => setSelectedItemId(item.id)}
                    >
                      <span className="font-medium hover:underline">{item.title}</span>
                      {item.description && (
                        <span className="line-clamp-1 text-xs text-muted-foreground">
                          {item.description}
                        </span>
                      )}
                    </button>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {item.planTitle ?? '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1">
                      <ItemStatusBadge status={item.status} />
                      {item.isOverdue && (
                        <Badge variant="destructive">
                          <AlertTriangle className="size-3" />
                          {t.actions.statsOverdueItems}
                        </Badge>
                      )}
                      {item.escalationLevel !== EscalationLevel.None && (
                        <Badge variant="warning">
                          {escalationLabel(t.actions, item.escalationLevel)}
                        </Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {item.assigneeUserName ?? '—'}
                    {item.isAssignedToMe && (
                      <span className="ms-1 text-xs text-primary">({t.actions.assignedToMe})</span>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {item.dueDate ? formatDateTime(item.dueDate, culture) : '—'}
                  </TableCell>
                  <TableCell className="text-end">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedItemId(item.id)}
                    >
                      {t.common.expand}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {items.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      <ActionItemDetailDialog
        open={!!selectedItemId}
        onClose={() => setSelectedItemId(null)}
        itemId={selectedItemId}
      />
    </AppLayout>
  );
}

function statusOptions(t: Dictionary) {
  return [
    { value: 'all', label: t.common.all },
    { value: String(ActionItemStatus.Open), label: t.actions.statusOpen },
    { value: String(ActionItemStatus.InProgress), label: t.actions.statusInProgress },
    { value: String(ActionItemStatus.Done), label: t.actions.statusDone },
    { value: String(ActionItemStatus.Cancelled), label: t.actions.statusItemCancelled }
  ];
}

function ItemStatusBadge({ status }: { status: ActionItemStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case ActionItemStatus.Open:
      return <Badge variant="outline">{t.actions.statusOpen}</Badge>;
    case ActionItemStatus.InProgress:
      return <Badge variant="default">{t.actions.statusInProgress}</Badge>;
    case ActionItemStatus.Done:
      return <Badge variant="success">{t.actions.statusDone}</Badge>;
    default:
      return <Badge variant="outline">{t.actions.statusItemCancelled}</Badge>;
  }
}

function escalationLabel(t: Dictionary['actions'], level: EscalationLevel): string {
  switch (level) {
    case EscalationLevel.Reminder: return t.escalationReminder;
    case EscalationLevel.EscalatedToOwner: return t.escalationOwner;
    case EscalationLevel.EscalatedToManagement: return t.escalationManagement;
    default: return t.escalationNone;
  }
}
