import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { FileSpreadsheet, Upload } from 'lucide-react';

import { useLanguage } from '@/i18n/LanguageProvider';
import { useImportEmployees } from '@/api/hooks';
import { ApiError } from '@/api/client';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { EmployeeImportResult } from '@/api/organization';

interface EmployeeImportDialogProps {
  open: boolean;
  onClose: () => void;
}

/**
 * دیالوگ ورود گروهی کارمندان از یک فایل اکسل (.xlsx).
 *
 * فایل به سرور ارسال می‌شود، ردیف‌ها اعتبارسنجی شده و نتیجه‌ی ردیف‌به‌ردیف
 * نمایش داده می‌شود: ایجادشده / نادیده‌گرفته‌شده (کد پرسنلی تکراری) / نامعتبر.
 */
export function EmployeeImportDialog({ open, onClose }: EmployeeImportDialogProps) {
  const { t } = useLanguage();
  const importMutation = useImportEmployees();
  const inputRef = useRef<HTMLInputElement>(null);

  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<EmployeeImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const isPending = importMutation.isPending;

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0] ?? null;
    setFile(selected);
    setError(null);
  }

  function reset() {
    setFile(null);
    setResult(null);
    setError(null);
    importMutation.reset();
    if (inputRef.current) {
      inputRef.current.value = '';
    }
  }

  function handleClose() {
    reset();
    onClose();
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!file) {
      setError(t.employees.importNoFile);
      return;
    }

    setError(null);

    try {
      const importResult = await importMutation.mutateAsync(file);
      setResult(importResult);
    } catch (err) {
      setError(err instanceof ApiError ? t.errors.fromCode(err.code) : t.errors.generic);
    }
  }

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      title={t.employees.importTitle}
      description={t.employees.importDescription}
      size="lg"
    >
      {result ? (
        <ImportResultView result={result} onClose={handleClose} />
      ) : (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          {error && (
            <div
              className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
              role="alert"
            >
              {error}
            </div>
          )}

          <label
            className="flex cursor-pointer flex-col items-center gap-2 rounded-md border border-dashed border-input bg-muted/30 p-6 text-center transition-colors hover:bg-muted/50"
          >
            {file ? (
              <FileSpreadsheet className="size-8 text-primary" />
            ) : (
              <Upload className="size-8 text-muted-foreground" />
            )}
            <span className="text-sm font-medium">
              {file ? file.name : t.employees.importSelectFile}
            </span>
            <span className="text-xs text-muted-foreground">{t.employees.importButtonHint}</span>
            <input
              ref={inputRef}
              type="file"
              accept=".xlsx"
              className="sr-only"
              onChange={handleFileChange}
              disabled={isPending}
            />
          </label>

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={handleClose} disabled={isPending}>
              {t.common.cancel}
            </Button>
            <Button type="submit" disabled={isPending || !file}>
              {isPending && <Spinner className="size-4" />}
              {isPending ? t.employees.importUploading : t.employees.importButton}
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
}

function ImportResultView({
  result,
  onClose
}: {
  result: EmployeeImportResult;
  onClose: () => void;
}) {
  const { t } = useLanguage();

  const summary = t.employees.importResultSummary
    .replace('{total}', String(result.totalRows))
    .replace('{created}', String(result.createdCount))
    .replace('{skipped}', String(result.skippedCount))
    .replace('{failed}', String(result.failedCount));

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm">{summary}</p>

      <div className="max-h-72 overflow-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t.employees.importColumnRow}</TableHead>
              <TableHead>{t.employees.importColumnEmployeeCode}</TableHead>
              <TableHead>{t.employees.importColumnFullName}</TableHead>
              <TableHead>{t.employees.importColumnResult}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {result.rows.map((row) => (
              <TableRow key={row.rowNumber}>
                <TableCell className="w-16">{row.rowNumber}</TableCell>
                <TableCell className="font-mono" dir="ltr">
                  {row.employeeCode}
                </TableCell>
                <TableCell>{row.fullName}</TableCell>
                <TableCell>
                  <div className="flex flex-col gap-0.5">
                    <RowOutcomeBadge outcome={row.outcome} />
                    {row.message && (
                      <span className="text-xs text-muted-foreground">{row.message}</span>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="flex justify-end">
        <Button onClick={onClose}>{t.common.close}</Button>
      </div>
    </div>
  );
}

function RowOutcomeBadge({ outcome }: { outcome: 'created' | 'skipped' | 'failed' }) {
  const { t } = useLanguage();

  if (outcome === 'created') {
    return <Badge variant="success">{t.employees.importRowCreated}</Badge>;
  }

  if (outcome === 'skipped') {
    return <Badge variant="warning">{t.employees.importRowSkipped}</Badge>;
  }

  return <Badge variant="destructive">{t.employees.importRowFailed}</Badge>;
}
