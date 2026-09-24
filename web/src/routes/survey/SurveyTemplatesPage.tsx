import { useState } from 'react';
import { LayoutTemplate, Plus, Pencil, Trash2, Search, Copy } from 'lucide-react';

import { ApiError } from '@/api/client';
import { Language, SurveyTemplateStatus, type CreateSurveyFromTemplateRequest } from '@/api/surveys';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useArchiveSurveyTemplate,
  useCreateSurveyFromTemplate,
  useDeleteSurveyTemplate,
  useSurveyTemplatesSearch
} from '@/api/hooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { Pagination } from '@/routes/identity/UsersPage';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { formatDate } from '@/i18n/format';
import { SurveyTemplateDialog } from './SurveyTemplateDialog';

/**
 * صفحه‌ی مدیریت قالب‌های نظرسنجی: مجموعه‌های قابل‌استفاده‌ی مجدد که
 * می‌توان از آن‌ها به‌سرعت نظرسنجی جدید ساخت.
 */
export function SurveyTemplatesPage() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const [searchText, setSearchText] = useState<string | null>(null);
  const [page, setPage] = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [archivingId, setArchivingId] = useState<string | null>(null);
  const [instantiatingId, setInstantiatingId] = useState<string | null>(null);

  const {
    data,
    isLoading,
    isError,
    error,
    refetch
  } = useSurveyTemplatesSearch({ searchText, status: null, page });

  const deleteMutation = useDeleteSurveyTemplate();
  const archiveMutation = useArchiveSurveyTemplate();

  const canCreate = hasPermission(Permissions.Survey.Create);
  const canEdit = hasPermission(Permissions.Survey.Edit);
  const canDelete = hasPermission(Permissions.Survey.Delete);

  function handleSearch(value: string) {
    setSearchText(value || null);
    setPage(1);
  }

  const templates = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <AppLayout>
      <PageHeader
        title={t.surveys.templatesTitle}
        description={t.surveys.templatesDescription}
        actions={
          canCreate ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              {t.surveys.newTemplate}
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute start-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={searchText ?? ''}
            onChange={(event) => handleSearch(event.target.value)}
            placeholder={t.surveys.searchPlaceholder}
            className="ps-9"
          />
        </div>
      </div>

      <div className="rounded-lg border">
        {isError ? (
          <ErrorState
            message={error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic}
            onRetry={() => void refetch()}
          />
        ) : isLoading ? (
          <TableLoading columns={5} />
        ) : templates.length === 0 ? (
          <EmptyState
            title={t.surveys.noTemplates}
            description={t.surveys.noTemplatesDescription}
            icon={<LayoutTemplate className="size-10" />}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t.surveys.code}</TableHead>
                <TableHead>{t.surveys.title_}</TableHead>
                <TableHead>{t.surveys.questionnaire}</TableHead>
                <TableHead>{t.surveys.status}</TableHead>
                <TableHead>{t.surveys.createdAt}</TableHead>
                <TableHead className="text-end">{t.common.actions}</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {templates.map((template) => (
                <TableRow key={template.id}>
                  <TableCell dir="ltr" className="font-medium">
                    {template.code}
                  </TableCell>
                  <TableCell>{template.title}</TableCell>
                  <TableCell dir="ltr" className="text-muted-foreground">
                    {template.questionnaireCode}
                  </TableCell>
                  <TableCell>
                    {template.status === SurveyTemplateStatus.Active ? (
                      <Badge variant="success">{t.surveys.activeStatus}</Badge>
                    ) : (
                      <Badge variant="outline">{t.surveys.archived}</Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground" dir="ltr">
                    {formatDate(template.createdAt, culture)}
                  </TableCell>                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      {canCreate && template.status === SurveyTemplateStatus.Active && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setInstantiatingId(template.id)}
                          aria-label={t.surveys.instantiate}
                          title={t.surveys.instantiate}
                        >
                          <Copy className="size-4" />
                        </Button>
                      )}

                      {canEdit && template.status === SurveyTemplateStatus.Active && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setEditingId(template.id)}
                          aria-label={t.common.edit}
                        >
                          <Pencil className="size-4" />
                        </Button>
                      )}

                      {canEdit && template.status === SurveyTemplateStatus.Active && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setArchivingId(template.id)}
                          aria-label={t.surveys.archive}
                          title={t.surveys.archive}
                        >
                          <LayoutTemplate className="size-4" />
                        </Button>
                      )}

                      {canDelete && (
                        <Button
                          variant="ghost"
                          size="icon"
                          className="text-destructive"
                          onClick={() => setDeletingId(template.id)}
                          aria-label={t.common.delete}
                        >
                          <Trash2 className="size-4" />
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

      {templates.length > 0 && (
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={setPage} />
      )}

      {instantiatingId && (
        <InstantiateDialog
          templateId={instantiatingId}
          onClose={() => setInstantiatingId(null)}
          onDone={() => setInstantiatingId(null)}
        />
      )}

      {createOpen && (
        <SurveyTemplateDialog open={createOpen} onClose={() => setCreateOpen(false)} />
      )}

      {editingId && (
        <SurveyTemplateDialog
          open={!!editingId}
          onClose={() => setEditingId(null)}
          templateId={editingId}
        />
      )}

      <ConfirmDialog
        open={!!deletingId}
        onClose={() => setDeletingId(null)}
        title={t.surveys.deleteConfirmTitle}
        description={t.surveys.deleteConfirmDescription}
        confirmLabel={t.common.delete}
        isPending={deleteMutation.isPending}
        error={
          deleteMutation.error instanceof ApiError
            ? t.errors.fromCode(deleteMutation.error.code)
            : deleteMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!deletingId) return;
          deleteMutation.mutate(deletingId, { onSuccess: () => setDeletingId(null) });
        }}
      />

      <ConfirmDialog
        open={!!archivingId}
        onClose={() => setArchivingId(null)}
        title={t.surveys.archiveConfirmTitle}
        description={t.surveys.archiveConfirmDescription}
        confirmLabel={t.surveys.archive}
        isPending={archiveMutation.isPending}
        error={
          archiveMutation.error instanceof ApiError
            ? t.errors.fromCode(archiveMutation.error.code)
            : archiveMutation.error
              ? t.errors.generic
              : undefined
        }
        onConfirm={() => {
          if (!archivingId) return;
          archiveMutation.mutate(archivingId, { onSuccess: () => setArchivingId(null) });
        }}
      />
    </AppLayout>
  );
}

interface InstantiateDialogProps {
  templateId: string;
  onClose: () => void;
  onDone: () => void;
}

/**
 * دیالوگ ساخت نظرسنجی از روی یک قالب: فقط کد یکتای نظرسنجی جدید پرسیده می‌شود
 * و بقیه‌ی تنظیمات از قالب کپی می‌شوند.
 */
function InstantiateDialog({ templateId, onClose, onDone }: InstantiateDialogProps) {
  const { t } = useLanguage();
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | undefined>();

  const instantiateMutation = useCreateSurveyFromTemplate();
  const isSaving = instantiateMutation.isPending;

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!code.trim()) {
      setError(t.surveys.code + ' ' + t.common.required);
      return;
    }

    setError(undefined);

    const request: CreateSurveyFromTemplateRequest = {
      templateId,
      code: code.trim(),
      localizations: [
        { language: Language.Fa, title: code.trim() }
      ]
    };

    try {
      await instantiateMutation.mutateAsync(request);
      onDone();
    } catch (mutationError) {
      setError(
        mutationError instanceof ApiError
          ? t.errors.fromCode(mutationError.code)
          : t.errors.generic
      );
    }
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={t.surveys.instantiateTitle}
      description={t.surveys.instantiateDescription}
      size="sm"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="instantiate-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.surveys.instantiate}
          </Button>
        </>
      }
    >
      <form id="instantiate-form" onSubmit={handleSubmit} className="grid gap-4" noValidate>
        {error && (
          <div
            className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {error}
          </div>
        )}

        <FormField label={t.surveys.newSurveyCode} htmlFor="instantiateCode" required error={error}>
          <Input
            id="instantiateCode"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>
      </form>
    </Dialog>
  );
}
