import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  reportsApi,
  ReportExecutionStatus,
  type ExecutionSearchRequest,
  type SaveReportRequest
} from '@/api/reports';import { useLanguage } from '@/i18n/LanguageProvider';

/**
 * کلیدهای کوئری گزارش‌گیری تا invalidation بین صفحات یکپارچه باشد.
 */
export const reportsQueryKeys = {
  reportSearch: (
    searchText: string | null,
    type: number | null,
    status: number | null,
    includeArchived: boolean,
    page: number
  ) => ['reports', 'search', searchText, type, status, includeArchived, page] as const,
  reports: ['reports'] as const,
  report: (id: string | null) => ['reports', 'detail', id] as const,
  reportData: (id: string | null) => ['reports', 'data', id] as const,
  executionSearch: (
    reportDefinitionId: string | null,
    status: ReportExecutionStatus | null,
    page: number
  ) => ['reports', 'executions', reportDefinitionId, status, page] as const
};

function useInvalidateReports() {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: ['reports'] });
  };
}

export function useReportSearch(params: {
  searchText: string | null;
  type: number | null;
  status: number | null;
  includeArchived: boolean;
  page: number;
}) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: reportsQueryKeys.reportSearch(
      params.searchText,
      params.type,
      params.status,
      params.includeArchived,
      params.page
    ),
    queryFn: ({ signal }) =>
      reportsApi.search(
        culture,
        {
          searchText: params.searchText,
          type: params.type,
          status: params.status,
          includeArchived: params.includeArchived,
          page: params.page,
          pageSize: 20
        },
        signal
      )
  });
}

export function useReport(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: reportsQueryKeys.report(id),
    queryFn: ({ signal }) => reportsApi.getById(culture, id!, signal),
    enabled: !!id
  });
}

/**
 * داده‌ی نمایش‌گرای یک تعریف گزارش برای رندر جدول نتایج روی صفحه.
 * فقط زمانی اجرا می‌شود که شناسه‌ای تنظیم شده باشد.
 */
export function useReportData(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: reportsQueryKeys.reportData(id),
    queryFn: ({ signal }) => reportsApi.getData(culture, id!, signal),
    enabled: !!id
  });
}

export function useCreateReport() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateReports();

  return useMutation({
    mutationFn: (request: SaveReportRequest) => reportsApi.create(culture, request),
    onSuccess: () => invalidate()
  });
}

export function useUpdateReport() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateReports();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveReportRequest }) =>
      reportsApi.update(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useActivateReport() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateReports();

  return useMutation({
    mutationFn: (id: string) => reportsApi.activate(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useArchiveReport() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateReports();

  return useMutation({
    mutationFn: (id: string) => reportsApi.archive(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useExecuteReport() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateReports();

  return useMutation({
    mutationFn: (id: string) => reportsApi.execute(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useExecutionSearch(request: ExecutionSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: reportsQueryKeys.executionSearch(
      request.reportDefinitionId ?? null,
      request.status ?? null,
      request.page ?? 1
    ),
    queryFn: ({ signal }) => reportsApi.searchExecutions(culture, request, signal)
  });
}

/**
 * دانلود فایل خروجی یک اجرای موفق و باز کردن آن در مرورگر.
 * blob URL ساخته‌شده پس از بارگذاری آزاد می‌شود.
 */
export function useDownloadArtifact() {
  const { culture } = useLanguage();

  return useMutation({
    mutationFn: async (execution: { id: string; fileName: string }) => {
      const blob = await reportsApi.downloadArtifact(culture, execution.id);
      const url = URL.createObjectURL(blob);

      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = execution.fileName;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);

      // آزادسازی منبع پس از شروع دانلود.
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
  });
}
