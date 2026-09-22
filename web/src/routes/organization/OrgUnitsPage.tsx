import { useState } from 'react';
import { Building2, Plus, Pencil, Trash2, ChevronLeft, ChevronDown } from 'lucide-react';

import { ApiError } from '@/api/client';
import { OrgUnitType } from '@/api/organization';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useOrgUnitTree, useDeleteOrgUnit } from '@/api/hooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { OrgUnitDialog } from './OrgUnitDialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import type { OrgUnitTreeNode } from '@/api/organization';

/**
 * صفحه‌ی درخت واحدهای سازمانی. ساختار سلسله‌مراتبی با باز/بسته کردن شاخه‌ها.
 */
export function OrgUnitsPage() {
  const { t } = useLanguage();
  const { hasPermission } = useAuth();

  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const { data: tree, isLoading, isError, error, refetch } = useOrgUnitTree();
  const deleteMutation = useDeleteOrgUnit();

  const canManage = hasPermission(Permissions.Organization.UnitsManage);

  function toggleExpanded(id: string) {
    setExpanded((previous) => {
      const next = new Set(previous);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function handleDelete() {
    if (!deletingId) return;
    deleteMutation.mutate(deletingId, {
      onSuccess: () => setDeletingId(null)
    });
  }

  return (
    <AppLayout>
      <PageHeader
        title={t.orgUnits.title}
        description={t.orgUnits.description}
        actions={
          canManage ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.orgUnits.newUnit}
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
          <TableLoading columns={3} />
        ) : !tree || tree.length === 0 ? (
          <EmptyState
            title={t.orgUnits.noUnits}
            description={t.orgUnits.noUnitsDescription}
            icon={<Building2 className="size-10" />}
          />
        ) : (
          <div className="p-2">
            {tree.map((node) => (
              <OrgUnitNode
                key={node.id}
                node={node}
                level={0}
                expanded={expanded}
                onToggleExpanded={toggleExpanded}
                canManage={canManage}
                onEdit={(id) => setEditingId(id)}
                onDelete={(id) => setDeletingId(id)}
              />
            ))}
          </div>
        )}
      </div>

      {createOpen && <OrgUnitDialog open={createOpen} onClose={() => setCreateOpen(false)} />}

      {editingId && (
        <OrgUnitDialog open={!!editingId} onClose={() => setEditingId(null)} unitId={editingId} />
      )}

      <ConfirmDialog
        open={!!deletingId}
        onClose={() => setDeletingId(null)}
        title={t.orgUnits.deleteConfirmTitle}
        description={t.orgUnits.deleteConfirmDescription}
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

interface OrgUnitNodeProps {
  node: OrgUnitTreeNode;
  level: number;
  expanded: Set<string>;
  onToggleExpanded: (id: string) => void;
  canManage: boolean;
  onEdit: (id: string) => void;
  onDelete: (id: string) => void;
}

function OrgUnitNode({
  node,
  level,
  expanded,
  onToggleExpanded,
  canManage,
  onEdit,
  onDelete
}: OrgUnitNodeProps) {
  const { t } = useLanguage();
  const hasChildren = node.children.length > 0;
  const isExpanded = expanded.has(node.id);

  return (
    <div className="flex flex-col">
      <div
        className="flex items-center gap-2 rounded-md px-2 py-2 transition-colors hover:bg-accent/40"
        style={{ paddingInlineStart: `${level * 20 + 8}px` }}
      >
        <button
          type="button"
          className="flex size-6 shrink-0 items-center justify-center rounded text-muted-foreground transition-colors hover:bg-accent"
          onClick={() => hasChildren && onToggleExpanded(node.id)}
          aria-expanded={isExpanded}
          aria-label={hasChildren ? (isExpanded ? t.common.collapse : t.common.expand) : undefined}
          disabled={!hasChildren}
        >
          {hasChildren ? (
            isExpanded ? (
              <ChevronDown className="size-4" />
            ) : (
              <ChevronLeft className="size-4 rtl:rotate-180" />
            )
          ) : (
            <span className="size-1.5 rounded-full bg-muted-foreground/50" aria-hidden />
          )}
        </button>

        <Badge variant="outline" className="shrink-0 text-xs" dir="ltr">
          {node.code}
        </Badge>

        <span className="flex-1 truncate text-sm font-medium">{node.name}</span>

        <Badge variant="outline" className="shrink-0 text-xs">
          {orgUnitTypeLabel(t, node.type)}
        </Badge>

        {!node.isActive && (
          <Badge variant="destructive" className="shrink-0 text-xs">
            {t.common.inactive}
          </Badge>
        )}

        {canManage && (
          <div className="flex shrink-0 items-center gap-1">
            <Button
              variant="ghost"
              size="icon"
              className="size-8"
              onClick={() => onEdit(node.id)}
              aria-label={t.common.edit}
            >
              <Pencil className="size-4" />
            </Button>

            <Button
              variant="ghost"
              size="icon"
              className="size-8 text-destructive"
              onClick={() => onDelete(node.id)}
              aria-label={t.common.delete}
              disabled={node.level === 0}
            >
              <Trash2 className="size-4" />
            </Button>
          </div>
        )}
      </div>

      {hasChildren && isExpanded && (
        <div className="flex flex-col">
          {node.children.map((child) => (
            <OrgUnitNode
              key={child.id}
              node={child}
              level={level + 1}
              expanded={expanded}
              onToggleExpanded={onToggleExpanded}
              canManage={canManage}
              onEdit={onEdit}
              onDelete={onDelete}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function orgUnitTypeLabel(t: ReturnType<typeof useLanguage>['t'], type: OrgUnitType): string {
  switch (type) {
    case OrgUnitType.Company:
      return t.orgUnits.company;
    case OrgUnitType.Division:
      return t.orgUnits.division;
    case OrgUnitType.Department:
      return t.orgUnits.department;
    case OrgUnitType.Team:
      return t.orgUnits.team;
    case OrgUnitType.Unit:
      return t.orgUnits.unit;
    default:
      return t.orgUnits.unit;
  }
}
