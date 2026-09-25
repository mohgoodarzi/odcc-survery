import { useState } from 'react';
import { Search, Trash2 } from 'lucide-react';

import { ApiError } from '@/api/client';
import { ResponseStatus } from '@/api/responses';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useDeleteResponse, useResponsesSearch } from '@/api/hooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { Pagination } from '@/routes/identity/UsersPage';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { formatDateTime } from '@/i18n/format';

/**
 * صفحه‌ی مدیریت پاسخ‌ها: جستجوی صفحه‌بندی‌شده‌ی نشست‌های ثبت‌شده.
 * دسترسی نیازمند مجوز مشاهده‌ی پاسخ‌هاست؛ مرز امنیتی واقعی سمت سرور است.
 */
export function ResponsesPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [page, setPage] = useState(1);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const { data, isLoading, isError, error, refetch } = useResponsesSearch({
    searchText,
    surveyId: null,
    status,
    page
  });

  const deleteMutation = useDeleteResponse();
  const canDelete = hasPermission(Permissions.Response.Export);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleStatusChange(value: string) {
    setStatus(value === 'all' ? null : Number(value));
    setPage(1);
  }

  const sessions = data?.items ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader title={t.responses.managementTitle} description={t.responses.managementDescription} />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute start-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={searchText ?? ''}
            onChange={(event) => handleSearch(event.target.value)}
            placeholder={t.responses.searchPlaceholder}
            className="ps-9"
          />
        </div>

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => handleStatusChange(event.target.value)}
          options={[
            { value: 'all', label: t.responses.all },
            { value: String(ResponseStatus.InProgress), label: t.responses.statusInProgress },
            { value: String(ResponseStatus.Submitted), label: t.responses.statusSubmitted }
          ]}
          aria-label={t.responses.status}
        />
      </div>

      {isError ? (
        <ErrorState message={(error as ApiError)?.problem.detail} onRetry={() => void refetch()} />
      ) : isLoading ? (
        <TableLoading columns={6} />
      ) : sessions.length === 0 ? (
        <EmptyState
          title={t.responses.noSessions}
          description={t.responses.noSessionsDescription}
        />
      ) : (
        <div className="flex flex-col gap-4">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.responses.surveyCode}</TableHead>
                <TableHead>{t.responses.campaignCode}</TableHead>
                <TableHead>{t.responses.respondent}</TableHead>
                <TableHead>{t.responses.status}</TableHead>
                <TableHead>{t.responses.answerCount}</TableHead>
                <TableHead>{t.responses.startedAt}</TableHead>
                <TableHead>{t.responses.submittedAt}</TableHead>
                {canDelete && <TableHead>{t.common.actions}</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {sessions.map((session) => (
                <TableRow key={session.id}>
                  <TableCell className="font-mono text-xs" dir="ltr">{session.surveyCode}</TableCell>
                  <TableCell className="font-mono text-xs" dir="ltr">
                    {session.campaignCode ?? '—'}
                  </TableCell>
                  <TableCell>
                    {session.isAnonymous ? t.responses.anonymousRespondent : session.respondentDisplayName}
                  </TableCell>
                  <TableCell>
                    {session.status === ResponseStatus.Submitted ? (
                      <Badge variant="success">{t.responses.statusSubmitted}</Badge>
                    ) : (
                      <Badge variant="outline">{t.responses.statusInProgress}</Badge>
                    )}
                  </TableCell>
                  <TableCell>{session.answerCount}</TableCell>
                  <TableCell className="text-xs">{formatDateTime(session.startedAt, culture)}</TableCell>
                  <TableCell className="text-xs">
                    {session.submittedAt ? formatDateTime(session.submittedAt, culture) : '—'}
                  </TableCell>
                  {canDelete && (
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t.responses.deleteSession}
                        onClick={() => setDeletingId(session.id)}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <Pagination
            page={page}
            totalPages={totalPages}
            totalCount={data?.totalCount ?? 0}
            onPageChange={setPage}
          />
        </div>
      )}

      <ConfirmDialog
        open={!!deletingId}
        onClose={() => setDeletingId(null)}
        title={t.responses.deleteConfirmTitle}
        description={t.responses.deleteConfirmDescription}
        confirmLabel={t.common.delete}
        onConfirm={() => {
          if (!deletingId) return;
          deleteMutation.mutate(deletingId, {
            onSuccess: () => setDeletingId(null)
          });
        }}
        isPending={deleteMutation.isPending}
        error={(deleteMutation.error as ApiError)?.problem.detail}
      />
    </AppLayout>
  );
}
