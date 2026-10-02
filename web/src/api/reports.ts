import { apiDelete, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import type { PagedResult } from './surveys';

/**
 * قرارداد API ماژول گزارش‌گیری.
 *
 * **حریم خصوصی:** خروجی گزارش‌ها فقط شامل تجمع‌های تحلیلی است. هیچ شناسه‌ی
 * پاسخ‌گویی (کاربر، کارمند یا نام) در فایل‌های تولیدشده قرار نمی‌گیرد —
 * حتی برای نظرسنجی‌های غیرناشناس.
 */

export enum ReportType {
  SurveyAnalytics = 1,
  DashboardSummary = 2,
  BenchmarkComparison = 3
}

export enum ReportFormat {
  Pdf = 1,
  Excel = 2
}

export enum ReportSchedule {
  OneTime = 0,
  Daily = 1,
  Weekly = 2,
  Monthly = 3
}

export enum ReportStatus {
  Draft = 0,
  Active = 1,
  Archived = 2
}

export enum ReportExecutionStatus {
  Pending = 0,
  Running = 1,
  Succeeded = 2,
  Failed = 3
}

export interface SaveReportRequest {
  name: string;
  description?: string | null;
  type: ReportType;
  format: ReportFormat;
  schedule: ReportSchedule;
  /** آیا تعریف بلافاصله فعال شود؟ false یعنی ذخیره به‌صورت پیش‌نویس. */
  activateImmediately: boolean;
  /** شناسه‌ی نظرسنجی (الزامی برای SurveyAnalytics و BenchmarkComparison). */
  surveyId?: string | null;
  from?: string | null;
  to?: string | null;
  orgUnitId?: string | null;
  includeDescendants: boolean;
  retentionCount: number;
}

export interface ReportSearchRequest {
  searchText?: string | null;
  type?: ReportType | null;
  status?: ReportStatus | null;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ExecutionSearchRequest {
  reportDefinitionId?: string | null;
  status?: ReportExecutionStatus | null;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ReportDefinition {
  id: string;
  name: string;
  description?: string | null;
  type: ReportType;
  format: ReportFormat;
  schedule: ReportSchedule;
  status: ReportStatus;

  surveyId?: string | null;
  surveyCode?: string | null;
  surveyTitle?: string | null;

  from?: string | null;
  to?: string | null;

  orgUnitId?: string | null;
  orgUnitPath?: string | null;
  includeDescendants: boolean;

  ownerUserId?: string | null;
  ownerUserName?: string | null;

  lastExecutedAt?: string | null;
  nextRunAt?: string | null;
  retentionCount: number;
  createdAt: string;
  updatedAt?: string | null;

  /** زمان آخرین اجرای موفق (برای نمایش در فهرست). */
  lastSuccessAt?: string | null;

  /** آیا حداقل یک اجرای موفق برای این تعریف ثبت شده است؟ */
  hasSuccessfulExecution: boolean;
}

export interface ReportExecution {
  id: string;
  reportDefinitionId: string;
  reportName: string;
  reportType: ReportType;
  format: ReportFormat;
  status: ReportExecutionStatus;

  queuedAt: string;
  startedAt?: string | null;
  completedAt?: string | null;

  triggeredBy?: string | null;
  triggeredByName?: string | null;

  /** آیا خروجی این اجرا برای دانلود آماده است؟ */
  hasArtifact: boolean;

  fileName?: string | null;
  fileSizeBytes?: number | null;
  rowCount?: number | null;
  errorMessage?: string | null;
}

/** نوع قالب‌بندی یک ستون گزارش (با سمت سرور هم‌خوان است). */
export enum ReportColumnType {
  Text = 0,
  Number = 1,
  Percent = 2,
  Date = 3
}

export interface ReportColumn {
  title: string;
  columnType: ReportColumnType;
}

export interface ReportSection {
  title: string;
  columns: ReportColumn[];
  /** هر ردیف به‌اندازه‌ی ستون‌ها مقدار دارد. */
  rows: (string | number | null)[][];
  footnote?: string | null;
}

/**
 * داده‌ی نمایش‌گرای یک گزارش: مجموعه‌ای از بخش‌های جدولی.
 * دقیقاً همان داده‌ای که فایل خروجی از آن رندر می‌شود.
 */
export interface ReportDataBundle {
  title: string;
  subtitle: string;
  type: ReportType;
  generatedAt: string;
  generatedBy: string;
  sections: ReportSection[];
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

export const reportsApi = {
  search: (culture: Culture, request: ReportSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.searchText) params.searchText = request.searchText;
    if (request.type !== null && request.type !== undefined) {
      params.type = String(request.type);
    }
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    if (request.includeArchived) params.includeArchived = 'true';
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<ReportDefinition>>(
      culture,
      appendSearchParams('/reports', params),
      { signal }
    );
  },

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<ReportDefinition>(culture, `/reports/${id}`, { signal }),

  /**
   * داده‌ی نمایش‌گرای یک تعریف گزارش (بخش‌ها/ستون‌ها/ردیف‌ها) برای
   * نمایش به‌صورت جدول روی صفحه. همان داده‌ای که فایل خروجی از آن
   * رندر می‌شود.
   */
  getData: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<ReportDataBundle>(culture, `/reports/${id}/data`, { signal }),

  create: (culture: Culture, request: SaveReportRequest) =>
    apiRequest<ReportDefinition>(culture, '/reports', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SaveReportRequest) =>
    apiPut<ReportDefinition>(culture, `/reports/${id}`, request),

  activate: (culture: Culture, id: string) =>
    apiRequest<ReportDefinition>(culture, `/reports/${id}/activate`, { method: 'POST' }),

  archive: (culture: Culture, id: string) => apiDelete<void>(culture, `/reports/${id}`),

  execute: (culture: Culture, id: string) =>
    apiRequest<ReportExecution>(culture, `/reports/${id}/execute`, { method: 'POST' }),

  searchExecutions: (culture: Culture, request: ExecutionSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.reportDefinitionId) params.reportDefinitionId = request.reportDefinitionId;
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    if (request.includeArchived) params.includeArchived = 'true';
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<ReportExecution>>(
      culture,
      appendSearchParams('/reports/executions', params),
      { signal }
    );
  },

  getExecution: (culture: Culture, executionId: string, signal?: AbortSignal) =>
    apiRequest<ReportExecution>(culture, `/reports/executions/${executionId}`, { signal }),

  /**
   * دانلود فایل خروجی یک اجرای موفق. یک URL blob برمی‌گرداند که فراخوان
   * باید آن را revoke کند. نوع محتوا بر اساس قالب گزارش تنظیم می‌شود.
   */
  downloadArtifact: async (culture: Culture, executionId: string): Promise<Blob> => {
    const response = await fetch(`/api/${culture}/reports/executions/${executionId}/artifact`, {
      credentials: 'same-origin'
    });

    if (!response.ok) {
      throw new Error(`دانلود خروجی گزارش با وضعیت ${response.status} شکست خورد.`);
    }

    return response.blob();
  }
};
