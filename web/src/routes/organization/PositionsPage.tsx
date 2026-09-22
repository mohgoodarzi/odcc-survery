import { useState } from 'react';
import { Briefcase, Plus, Pencil, Trash2 } from 'lucide-react';

import { ApiError } from '@/api/client';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useDeletePosition, usePositions } from '@/api/hooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { PositionDialog } from './PositionDialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';

/**
 * صفحه‌ی مدیریت موقعیت‌های شغلی.
 */
export function PositionsPage() {
  const { t } = useLanguage();
  const { hasPermission } = useAuth();

  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const { data: positions, isLoading, isError, error, refetch } = usePositions();
  const deleteMutation = useDeletePosition();

  const canManage = hasPermission(Permissions.Organization.PositionsManage);

  function handleDelete() {
    if (!deletingId) return;
    deleteMutation.mutate(deletingId, {
      onSuccess: () => setDeletingId(null)
    });
  }

  return (
    <AppLayout>
      <PageHeader
        title={t.positions.title}
        description={t.positions.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.positions.newPosition}
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
          <TableLoading columns={5} />
        ) : !positions || positions.length === 0 ? (
          <EmptyState
            title={t.positions.noPositions}
            description={t.positions.noPositionsDescription}
            icon={<Briefcase className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.positions.code}</TableHead>
                <TableHead>{t.positions.title_}</TableHead>
                <TableHead>{t.positions.orgUnit}</TableHead>
                <TableHead>{t.positions.reportsTo}</TableHead>
                <TableHead>{t.positions.grade}</TableHead>
                <TableHead>{t.positions.status}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {positions.map((position) => (
                <TableRow key={position.id}>
                  <TableCell dir="ltr" className="font-medium">
                    {position.code}
                  </TableCell>
                  <TableCell>{position.title}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {position.orgUnitName ?? '—'}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {position.reportsToTitle ?? '—'}
                  </TableCell>
                  <TableCell dir="ltr">{position.grade ?? '—'}</TableCell>
                  <TableCell>
                    {position.isActive ? (
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
                            onClick={() => setEditingId(position.id)}
                            aria-label={t.common.edit}
                          >
                            <Pencil className="size-4" />
                          </Button>

                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-destructive"
                            onClick={() => setDeletingId(position.id)}
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

      {createOpen && <PositionDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingId && (
        <PositionDialog open={!!editingId} onClose={() => setEditingId(null)} positionId={editingId} />
      )}

      <ConfirmDialog
        open={!!deletingId}
        onClose={() => setDeletingId(null)}
        title={t.positions.deleteConfirmTitle}
        description={t.positions.deleteConfirmDescription}
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
