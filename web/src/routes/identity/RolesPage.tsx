import { useRef, useState } from 'react';
import { ShieldPlus, MoreHorizontal, Pencil, Lock } from 'lucide-react';

import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useRoles } from '@/api/hooks';
import { useClickOutside } from '@/lib/use-click-outside';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { RoleDialog } from './RoleDialog';

/**
 * صفحه‌ی مدیریت نقش‌ها و مجوزها.
 */
export function RolesPage() {
  const { t } = useLanguage();
  const { hasPermission } = useAuth();

  const [createOpen, setCreateOpen] = useState(false);
  const [editingRoleId, setEditingRoleId] = useState<string | null>(null);
  const [activeMenu, setActiveMenu] = useState<string | null>(null);

  const { data: roles, isLoading, isError, error, refetch } = useRoles();

  const canManage = hasPermission(Permissions.Identity.RolesManage);

  return (
    <AppLayout>
      <PageHeader
        title={t.roles.title}
        description={t.roles.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <ShieldPlus className="size-4" />
              {t.roles.newRole}
            </Button>
          ) : undefined
        }
      />

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={4} />
        ) : !roles || roles.length === 0 ? (
          <EmptyState title={t.roles.noRoles} description={t.roles.noRolesDescription} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.roles.roleName}</TableHead>
                <TableHead>{t.roles.displayName}</TableHead>
                <TableHead>{t.roles.description}</TableHead>
                <TableHead>{t.roles.permissions}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {roles.map((role) => (
                <TableRow key={role.id}>
                  <TableCell className="font-medium" dir="ltr">
                    {role.name}
                  </TableCell>
                  <TableCell>
                    <span className="flex items-center gap-2">
                      {role.displayName ?? role.name}
                      {role.isSystem && (
                        <Badge variant="warning" className="gap-1">
                          <Lock className="size-3" />
                          {t.roles.system}
                        </Badge>
                      )}
                    </span>
                  </TableCell>
                  <TableCell className="max-w-md text-sm text-muted-foreground">
                    {role.description ?? '—'}
                  </TableCell>
                  <TableCell>
                    {role.permissions.length > 0 ? (
                      <div className="flex max-w-md flex-wrap gap-1">
                        {role.permissions.slice(0, 5).map((permission) => (
                          <Badge key={permission} variant="outline" className="text-xs" dir="ltr">
                            {permission}
                          </Badge>
                        ))}
                        {role.permissions.length > 5 && (
                          <Badge variant="outline" className="text-xs">
                            +{role.permissions.length - 5}
                          </Badge>
                        )}
                      </div>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <RoleRowActions
                      isOpen={activeMenu === role.id}
                      onToggleMenu={() => setActiveMenu(activeMenu === role.id ? null : role.id)}
                      onEdit={() => {
                        setActiveMenu(null);
                        setEditingRoleId(role.id);
                      }}
                      canManage={canManage}
                      t={t}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {createOpen && <RoleDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingRoleId && (
        <RoleDialog
          open={!!editingRoleId}
          onClose={() => setEditingRoleId(null)}
          roleId={editingRoleId}
        />
      )}
    </AppLayout>
  );
}

interface RoleRowActionsProps {
  isOpen: boolean;
  onToggleMenu: () => void;
  onEdit: () => void;
  canManage: boolean;
  t: ReturnType<typeof useLanguage>['t'];
}

/**
 * منوی عملیات هر ردیف نقش. هر ردیف ref خود را دارد تا کلیک بیرون از منوی
 * بازشده فقط همان منو را ببندد.
 */
function RoleRowActions({ isOpen, onToggleMenu, onEdit, canManage, t }: RoleRowActionsProps) {
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
        disabled={!canManage}
      >
        <MoreHorizontal className="size-4" />
      </Button>

      {isOpen && (
        <div
          className="absolute end-0 top-full z-40 mt-2 w-40 rounded-md border bg-card text-card-foreground shadow-md"
          role="menu"
        >
          <div className="flex flex-col p-1">
            <button
              type="button"
              role="menuitem"
              className="flex items-center gap-2 rounded-sm px-3 py-2 text-sm transition-colors hover:bg-accent"
              onClick={onEdit}
            >
              <Pencil className="size-4" />
              {t.common.edit}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
