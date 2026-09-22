import { useState } from 'react';
import { Users, Plus, Pencil, Trash2, Search } from 'lucide-react';

import { ApiError } from '@/api/client';
import { EmployeeStatus } from '@/api/organization';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useDeleteEmployee, useEmployeesSearch, useOrgUnits } from '@/api/hooks';
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
import { Checkbox } from '@/components/ui/checkbox';
import { Pagination } from '@/routes/identity/UsersPage';
import { EmployeeDialog } from './EmployeeDialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';

/**
 * صفحه‌ی مدیریت کارمندان: جستجوی صفحه‌بندی‌شده بر اساس واحد سازمانی و وضعیت.
 */
export function EmployeesPage() {
  const { t } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [orgUnitId, setOrgUnitId] = useState<string | null>(null);
  const [includeDescendants, setIncludeDescendants] = useState(false);
  const [status, setStatus] = useState<number | null>(null);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const { data: orgUnits } = useOrgUnits();

  const {
    data,
    isLoading,
    isError,
    error,
    refetch
  } = useEmployeesSearch({
    searchText,
    orgUnitId,
    includeDescendants,
    status,
    page
  });

  const deleteMutation = useDeleteEmployee();

  const canManage = hasPermission(Permissions.Organization.EmployeesManage);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleOrgUnitChange(value: string) {
    setOrgUnitId(value || null);
    setPage(1);
  }

  function handleStatusChange(value: string) {
    setStatus(value === 'all' ? null : Number(value));
    setPage(1);
  }

  function handleDelete() {
    if (!deletingId) return;
    deleteMutation.mutate(deletingId, {
      onSuccess: () => setDeletingId(null)
    });
  }

  const employees = data?.items ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  const orgUnitOptions = (orgUnits ?? []).map((unit) => ({
    value: unit.id,
    label: `${unit.name} (${unit.code})`
  }));

  return (
    <AppLayout>
      <PageHeader
        title={t.employees.title}
        description={t.employees.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.employees.newEmployee}
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="relative max-w-xs flex-1">
          <Search className="absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="search"
            placeholder={t.employees.searchPlaceholder}
            value={searchText ?? ''}
            onChange={(event) => handleSearch(event.target.value)}
            className="ps-9"
          />
        </div>

        <Select
          value={orgUnitId ?? ''}
          onChange={(event) => handleOrgUnitChange(event.target.value)}
          options={orgUnitOptions}
          placeholder={t.employees.selectOrgUnit}
          className="w-48"
        />

        <Select
          value={status === null ? 'all' : String(status)}
          onChange={(event) => handleStatusChange(event.target.value)}
          options={[
            { value: 'all', label: t.common.all },
            { value: String(EmployeeStatus.Active), label: t.employees.active },
            { value: String(EmployeeStatus.OnLeave), label: t.employees.onLeave },
            { value: String(EmployeeStatus.Suspended), label: t.employees.suspended },
            { value: String(EmployeeStatus.Terminated), label: t.employees.terminated }
          ]}
          className="w-40"
        />

        <label className="flex cursor-pointer items-center gap-2 text-sm text-muted-foreground">
          <Checkbox
            checked={includeDescendants}
            onCheckedChange={(checked) => {
              setIncludeDescendants(checked);
              setPage(1);
            }}
          />
          {t.employees.includeDescendants}
        </label>

        {(searchText || orgUnitId || status !== null) && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              setSearchText(null);
              setOrgUnitId(null);
              setStatus(null);
              setIncludeDescendants(false);
              setPage(1);
            }}
          >
            {t.common.clearFilters}
          </Button>
        )}
      </div>

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={6} />
        ) : employees.length === 0 ? (
          <EmptyState
            title={t.employees.noEmployees}
            description={t.employees.noEmployeesDescription}
            icon={<Users className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.employees.employeeCode}</TableHead>
                <TableHead>{t.employees.firstName}</TableHead>
                <TableHead>{t.employees.orgUnit}</TableHead>
                <TableHead>{t.employees.position}</TableHead>
                <TableHead>{t.employees.status}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {employees.map((employee) => (
                <TableRow key={employee.id}>
                  <TableCell dir="ltr" className="font-medium">
                    {employee.employeeCode}
                  </TableCell>
                  <TableCell>{employee.fullName}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {employee.orgUnitName ?? '—'}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {employee.positionTitle ?? '—'}
                  </TableCell>
                  <TableCell>
                    <EmployeeStatusBadge status={employee.status} />
                    {!employee.isCurrentlyEmployed && (
                      <Badge variant="outline" className="ms-1 text-xs">
                        {t.employees.notEmployed}
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canManage && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setEditingId(employee.id)}
                            aria-label={t.common.edit}
                          >
                            <Pencil className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setDeletingId(employee.id)}
                            aria-label={t.common.delete}
                          >
                            <Trash2 className="size-4" />
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

      {data && data.totalCount > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          totalCount={data.totalCount}
          onPageChange={setPage}
        />
      )}

      {createOpen && <EmployeeDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingId && (
        <EmployeeDialog open={!!editingId} onClose={() => setEditingId(null)} employeeId={editingId} />
      )}

      <ConfirmDialog
        open={!!deletingId}
        onClose={() => setDeletingId(null)}
        title={t.employees.deleteConfirmTitle}
        description={t.employees.deleteConfirmDescription}
        confirmLabel={t.common.delete}
        isPending={deleteMutation.isPending}
        error={
          deleteMutation.error instanceof ApiError
            ? t.errors.fromCode(deleteMutation.error.code)
            : deleteMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={handleDelete}
      />
    </AppLayout>
  );
}

function EmployeeStatusBadge({ status }: { status: EmployeeStatus }) {
  const { t } = useLanguage();

  switch (status) {
    case EmployeeStatus.Active:
      return <Badge variant="success">{t.employees.active}</Badge>;
    case EmployeeStatus.OnLeave:
      return <Badge variant="warning">{t.employees.onLeave}</Badge>;
    case EmployeeStatus.Suspended:
      return <Badge variant="destructive">{t.employees.suspended}</Badge>;
    case EmployeeStatus.Terminated:
      return <Badge variant="outline">{t.employees.terminated}</Badge>;
    default:
      return <Badge variant="outline">—</Badge>;
  }
}
