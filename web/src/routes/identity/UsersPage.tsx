import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { UserPlus, Search, MoreHorizontal, KeyRound, ShieldCheck, Power, UserCog } from 'lucide-react';

import { usersApi, type UserSummary } from '@/api/users';
import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useClickOutside } from '@/lib/use-click-outside';
import { useUsersSearch, queryKeys } from '@/api/hooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { formatDate } from '@/i18n/format';
import { UserDialog } from './UserDialog';
import { ResetPasswordDialog } from './ResetPasswordDialog';
import { AssignRolesDialog } from './AssignRolesDialog';

/**
 * صفحه‌ی مدیریت کاربران: جستجوی صفحه‌بندی‌شده، ایجاد/ویرایش، فعال‌سازی،
 * بازنشانی رمز عبور و انتصاب نقش‌ها.
 */
export function UsersPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();
  const queryClient = useQueryClient();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<boolean | null>(null);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserSummary | null>(null);
  const [resetUser, setResetUser] = useState<UserSummary | null>(null);
  const [rolesUser, setRolesUser] = useState<UserSummary | null>(null);
  const [activeMenu, setActiveMenu] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | undefined>();

  const { data, isLoading, isError, error, refetch } = useUsersSearch({
    searchText,
    isActive: statusFilter,
    page
  });

  const canCreate = hasPermission(Permissions.Identity.UsersCreate);
  const canEdit = hasPermission(Permissions.Identity.UsersEdit);
  const canDeactivate = hasPermission(Permissions.Identity.UsersDeactivate);
  const canResetPassword = hasPermission(Permissions.Identity.UsersResetPassword);

  const toggleActiveMutation = useMutation({
    mutationFn: (user: UserSummary) =>
      usersApi.setActive(culture, { userId: user.id, isActive: !user.isActive }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
      void refetch();
    },
    onError: (mutationError) => {
      setActionError(
        mutationError instanceof ApiError
          ? t.errors.fromCode(mutationError.code)
          : t.errors.generic
      );
    }
  });

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  function handleStatusFilter(value: string) {
    setStatusFilter(value === 'all' ? null : value === 'true');
    setPage(1);
  }

  function handleToggleActive(user: UserSummary) {
    setActiveMenu(null);
    setActionError(undefined);
    toggleActiveMutation.mutate(user);
  }

  const users = data?.items ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.users.title}
        description={t.users.description}
        actions={
          canCreate ? (
            <Button onClick={() => setCreateOpen(true)}>
              <UserPlus className="size-4" />
              {t.users.newUser}
            </Button>
          ) : undefined
        }
      />

      {actionError && (
        <div
          className="mb-4 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
          role="alert"
        >
          {actionError}
        </div>
      )}

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="relative max-w-xs flex-1">
          <Search className="absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="search"
            placeholder={t.users.searchPlaceholder}
            value={searchText ?? ''}
            onChange={(event) => handleSearch(event.target.value)}
            className="ps-9"
          />
        </div>

        <Select
          value={statusFilter === null ? 'all' : String(statusFilter)}
          onChange={(event) => handleStatusFilter(event.target.value)}
          options={[
            { value: 'all', label: t.users.all },
            { value: 'true', label: t.users.active },
            { value: 'false', label: t.users.inactive }
          ]}
          className="w-40"
        />

        {(searchText || statusFilter !== null) && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              setSearchText(null);
              setStatusFilter(null);
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
        ) : users.length === 0 ? (
          <EmptyState title={t.users.noUsers} description={t.users.noUsersDescription} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.users.userName}</TableHead>
                <TableHead>{t.users.firstName}</TableHead>
                <TableHead>{t.users.email}</TableHead>
                <TableHead>{t.users.roles}</TableHead>
                <TableHead>{t.users.status}</TableHead>
                <TableHead>{t.users.createdAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {users.map((user) => (
                <TableRow key={user.id}>
                  <TableCell dir="ltr" className="font-medium">
                    {user.userName}
                  </TableCell>
                  <TableCell>{user.displayName}</TableCell>
                  <TableCell dir="ltr" className="text-muted-foreground">
                    {user.email}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1">
                      {user.roles.length > 0 ? (
                        user.roles.map((role) => (
                          <Badge key={role} variant="outline">
                            {role}
                          </Badge>
                        ))
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    {user.isActive ? (
                      <Badge variant="success">{t.users.active}</Badge>
                    ) : (
                      <Badge variant="destructive">{t.users.inactive}</Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(user.createdAt, culture)}
                  </TableCell>
                  <TableCell>
                    <RowActions
                      user={user}
                      isOpen={activeMenu === user.id}
                      onToggleMenu={() =>
                        setActiveMenu(activeMenu === user.id ? null : user.id)
                      }
                      canEdit={canEdit}
                      canDeactivate={canDeactivate}
                      canResetPassword={canResetPassword}
                      onEdit={() => {
                        setActiveMenu(null);
                        setEditingUser(user);
                      }}
                      onToggleActive={() => handleToggleActive(user)}
                      onResetPassword={() => {
                        setActiveMenu(null);
                        setResetUser(user);
                      }}
                      onAssignRoles={() => {
                        setActiveMenu(null);
                        setRolesUser(user);
                      }}
                      t={t}
                    />
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

      {createOpen && <UserDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingUser && (
        <UserDialog
          open={!!editingUser}
          onClose={() => setEditingUser(null)}
          user={editingUser}
        />
      )}

      {resetUser && <ResetPasswordDialog user={resetUser} onClose={() => setResetUser(null)} />}

      {rolesUser && <AssignRolesDialog user={rolesUser} onClose={() => setRolesUser(null)} />}
    </AppLayout>
  );
}

interface RowActionsProps {
  user: UserSummary;
  isOpen: boolean;
  onToggleMenu: () => void;
  canEdit: boolean;
  canDeactivate: boolean;
  canResetPassword: boolean;
  onEdit: () => void;
  onToggleActive: () => void;
  onResetPassword: () => void;
  onAssignRoles: () => void;
  t: ReturnType<typeof useLanguage>['t'];
}

function RowActions({
  user,
  isOpen,
  onToggleMenu,
  canEdit,
  canDeactivate,
  canResetPassword,
  onEdit,
  onToggleActive,
  onResetPassword,
  onAssignRoles,
  t
}: RowActionsProps) {
  const containerRef = useRef<HTMLDivElement>(null);

  useClickOutside(containerRef, () => onToggleMenu(), isOpen);

  return (
    <div className="relative flex justify-end" ref={containerRef}>
      <Button
        variant="ghost"
        size="icon"
        onClick={onToggleMenu}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        aria-label={t.common.actions}
      >
        <MoreHorizontal className="size-4" />
      </Button>

      {isOpen && (
        <div
          className="absolute end-0 top-full z-40 mt-2 w-48 rounded-md border bg-card text-card-foreground shadow-md"
          role="menu"
        >
          <div className="flex flex-col p-1">
            {canEdit && (
              <MenuItem icon={<UserCog className="size-4" />} label={t.common.edit} onClick={onEdit} />
            )}

            {canEdit && (
              <MenuItem
                icon={<ShieldCheck className="size-4" />}
                label={t.users.assignRoles}
                onClick={onAssignRoles}
              />
            )}

            {canDeactivate && (
              <MenuItem
                icon={<Power className="size-4" />}
                label={user.isActive ? t.users.deactivate : t.users.activate}
                onClick={onToggleActive}
              />
            )}

            {canResetPassword && (
              <MenuItem
                icon={<KeyRound className="size-4" />}
                label={t.users.resetPassword}
                onClick={onResetPassword}
              />
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function MenuItem({ icon, label, onClick }: { icon: React.ReactNode; label: string; onClick: () => void }) {
  return (
    <button
      type="button"
      role="menuitem"
      className="flex items-center gap-2 rounded-sm px-3 py-2 text-sm transition-colors hover:bg-accent"
      onClick={onClick}
    >
      {icon}
      {label}
    </button>
  );
}

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, totalPages, totalCount, onPageChange }: PaginationProps) {
  const { t } = useLanguage();

  return (
    <div className="mt-4 flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
      <span>
        {t.common.total}: {totalCount} {t.common.results}
      </span>

      <div className="flex items-center gap-2">
        <span>
          {t.common.page} {page} {t.common.of} {totalPages}
        </span>

        <Button
          variant="outline"
          size="sm"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          {t.common.previous}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          {t.common.next}
        </Button>
      </div>
    </div>
  );
}
