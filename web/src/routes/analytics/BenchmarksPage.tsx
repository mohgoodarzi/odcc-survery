import { useState } from 'react';
import { Plus, Trash2, Pencil } from 'lucide-react';

import { ApiError } from '@/api/client';
import { MetricType, type Benchmark, type SaveBenchmarkRequest } from '@/api/analytics';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useBenchmarks,
  useCreateBenchmark,
  useDeleteBenchmark,
  useMetricLabel,
  useUpdateBenchmark
} from '@/api/analyticsHooks';
import { useOrgUnitTree } from '@/api/hooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { Checkbox } from '@/components/ui/checkbox';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Dialog } from '@/components/ui/dialog';
import { formatDateTime } from '@/i18n/format';

/**
 * صفحه‌ی مدیریت بنچمارک‌ها: اهداف مرجع شاخص‌های تحلیلی.
 * دسترسی نیازمند مجوز مشاهده‌ی تحلیلات سطح شرکت است.
 */
export function BenchmarksPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();
  const metricLabel = useMetricLabel();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<Benchmark | null>(null);
  const [creating, setCreating] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const { data, isLoading, isError, error, refetch } = useBenchmarks(searchText, includeInactive);
  const createMutation = useCreateBenchmark();
  const updateMutation = useUpdateBenchmark();
  const deleteMutation = useDeleteBenchmark();

  const benchmarks = data?.items ?? [];
  const canManage = hasPermission(Permissions.Analytics.CompanyView);

  return (
    <AppLayout>
      <PageHeader
        title={t.analytics.benchmarks}
        description={t.analytics.benchmarksDescription}
        actions={canManage ? (
          <Button size="sm" onClick={() => setCreating(true)}>
            <Plus className="size-4" />
            {t.analytics.newBenchmark}
          </Button>
        ) : undefined}
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="flex-1 min-w-[200px]">
          <Input
            value={searchText ?? ''}
            onChange={(event) => setSearchText(event.target.value || null)}
            placeholder={t.common.search}
          />
        </div>
        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          <Checkbox
            checked={includeInactive}
            onCheckedChange={(checked) => setIncludeInactive(checked)}
          />
          {t.analytics.includeInactive}
        </label>
      </div>

      {isError ? (
        <ErrorState message={(error as ApiError)?.problem.detail} onRetry={() => void refetch()} />
      ) : isLoading ? (
        <TableLoading columns={5} />
      ) : benchmarks.length === 0 ? (
        <EmptyState
          title={t.analytics.noBenchmarks}
          description={t.analytics.noBenchmarksDescription}
          action={canManage ? { label: t.analytics.newBenchmark, onClick: () => setCreating(true) } : undefined}
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t.analytics.benchmarkName}</TableHead>
              <TableHead>{t.analytics.metric}</TableHead>
              <TableHead>{t.analytics.targetValue}</TableHead>
              <TableHead>{t.analytics.orgUnit}</TableHead>
              <TableHead>{t.common.active}</TableHead>
              <TableHead>{t.common.createdAt}</TableHead>
              {canManage && <TableHead>{t.common.actions}</TableHead>}
            </TableRow>
          </TableHeader>
          <TableBody>
            {benchmarks.map((benchmark) => (
              <TableRow key={benchmark.id}>
                <TableCell className="text-sm font-medium">{benchmark.name}</TableCell>
                <TableCell className="text-sm">{metricLabel(benchmark.metric)}</TableCell>
                <TableCell className="font-mono text-sm" dir="ltr">
                  {String(benchmark.targetValue)}
                </TableCell>
                <TableCell className="font-mono text-xs" dir="ltr">
                  {benchmark.isCompanyWide ? t.analytics.companyWide : (benchmark.orgUnitPath ?? '—')}
                </TableCell>
                <TableCell>
                  <Badge variant={benchmark.isActive ? 'success' : 'outline'}>
                    {benchmark.isActive ? t.common.active : t.common.inactive}
                  </Badge>
                </TableCell>
                <TableCell className="text-xs text-muted-foreground">
                  {formatDateTime(benchmark.createdAt, culture)}
                </TableCell>
                {canManage && (
                  <TableCell>
                    <div className="flex items-center gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t.common.edit}
                        onClick={() => setEditing(benchmark)}
                      >
                        <Pencil className="size-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t.analytics.deleteBenchmark}
                        onClick={() => setDeletingId(benchmark.id)}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {(creating || editing) && (
        <BenchmarkDialog
          benchmark={editing}
          open={creating || editing !== null}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={async (request) => {
            if (editing) {
              await updateMutation.mutateAsync({ id: editing.id, request });
            } else {
              await createMutation.mutateAsync(request);
            }
            setCreating(false);
            setEditing(null);
          }}
        />
      )}

      <ConfirmDialog
        open={deletingId !== null}
        title={t.analytics.deleteConfirmTitle}
        description={t.analytics.deleteConfirmDescription}
        confirmLabel={t.common.confirm}
        onConfirm={async () => {
          if (deletingId) {
            await deleteMutation.mutateAsync(deletingId);
          }
          setDeletingId(null);
        }}
        onClose={() => setDeletingId(null)}
      />
    </AppLayout>
  );
}

interface BenchmarkDialogProps {
  benchmark: Benchmark | null;
  open: boolean;
  onClose: () => void;
  onSubmit: (request: SaveBenchmarkRequest) => Promise<void>;
}

function BenchmarkDialog({ benchmark, open, onClose, onSubmit }: BenchmarkDialogProps) {
  const { t } = useLanguage();
  const { data: orgUnitTree } = useOrgUnitTree();

  const [name, setName] = useState(benchmark?.name ?? '');
  const [metric, setMetric] = useState<MetricType>(benchmark?.metric ?? MetricType.Nps);
  const [targetValue, setTargetValue] = useState(benchmark?.targetValue ?? 0);
  const [isCompanyWide, setIsCompanyWide] = useState(benchmark?.isCompanyWide ?? true);
  const [orgUnitId, setOrgUnitId] = useState<string | null>(benchmark?.orgUnitId ?? null);
  const [description, setDescription] = useState(benchmark?.description ?? '');
  const [submitting, setSubmitting] = useState(false);

  const orgUnits = flattenOrgUnits(orgUnitTree ?? []);

  async function handleSubmit() {
    setSubmitting(true);

    try {
      await onSubmit({
        name,
        metric,
        targetValue,
        isCompanyWide,
        orgUnitId: isCompanyWide ? null : orgUnitId,
        description: description || null
      });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={benchmark ? t.analytics.editBenchmark : t.analytics.newBenchmark}
    >
      <div className="flex flex-col gap-4">
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium">{t.analytics.benchmarkName}</span>
          <Input value={name} onChange={(event) => setName(event.target.value)} />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium">{t.analytics.metric}</span>
          <Select
            value={String(metric)}
            onChange={(event) => setMetric(Number(event.target.value) as MetricType)}
            options={Object.values(MetricType)
              .filter((value) => typeof value === 'number')
              .map((value) => ({
                value: String(value),
                label: metricLabelText(value as MetricType)
              }))}
          />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium">{t.analytics.targetValue}</span>
          <Input
            type="number"
            value={String(targetValue)}
            onChange={(event) => setTargetValue(Number(event.target.value))}
            dir="ltr"
          />
        </label>

        <label className="flex items-center gap-2 text-sm">
          <Checkbox
            checked={isCompanyWide}
            onCheckedChange={(checked) => setIsCompanyWide(checked)}
          />
          {t.analytics.companyWide}
        </label>

        {!isCompanyWide && (
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium">{t.analytics.orgUnit}</span>
            <Select
              value={orgUnitId ?? ''}
              onChange={(event) => setOrgUnitId(event.target.value || null)}
              options={orgUnits.map((unit) => ({
                value: unit.id,
                label: `${'۱'.repeat(unit.level + 1)} ${unit.name}`
              }))}
            />
          </label>
        )}

        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium">{t.common.actions}</span>
          <Input value={description} onChange={(event) => setDescription(event.target.value)} />
        </label>

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button onClick={handleSubmit} disabled={submitting || !name}>
            {submitting ? t.common.saving : t.common.save}
          </Button>
        </div>
      </div>
    </Dialog>
  );

  function metricLabelText(value: MetricType): string {
    switch (value) {
      case MetricType.Nps: return t.analytics.metricNps;
      case MetricType.Csat: return t.analytics.metricCsat;
      case MetricType.Ces: return t.analytics.metricCes;
      case MetricType.CompletionRate: return t.analytics.metricCompletionRate;
      case MetricType.ResponseRate: return t.analytics.metricResponseRate;
      default: return t.analytics.metricAverageRating;
    }
  }
}

interface FlatOrgUnit {
  id: string;
  name: string;
  level: number;
}

/**
 * تبدیل درخت واحدهای سازمانی به فهرست تخت با سطح.
 */
function flattenOrgUnits(
  nodes: ReadonlyArray<{ id: string; name: string; children?: unknown[] }> | undefined,
  level = 0,
  result: FlatOrgUnit[] = []
): FlatOrgUnit[] {
  if (!nodes) return result;

  for (const node of nodes) {
    result.push({ id: node.id, name: node.name, level });

    const children = node.children as
      | ReadonlyArray<{ id: string; name: string; children?: unknown[] }>
      | undefined;

    flattenOrgUnits(children, level + 1, result);
  }

  return result;
}
