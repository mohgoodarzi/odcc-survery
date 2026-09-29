import { apiDelete, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import type { PagedResult } from './users';

/**
 * قرارداد API ماژول گردش کار: ماشین وضعیت قابل پیکربندی برای چرخه‌ی عمر
 * موجودیت‌ها (نظرسنجی، کمپین، برنامه‌ی اقدام، ...) به‌همراه درخواست‌های تأیید.
 *
 * **مرز مجوزها:** تعاریف و نمونه‌ها با `workflows.view` قابل مشاهده‌اند؛
 * مدیریت با `workflows.manage` و تصمیم‌گیری درباره‌ی تأییدها با `workflows.approve`
 * (یا مجوز مشخص‌شده‌ی روی گذار).
 */

export enum WorkflowEntityType {
  Survey = 0,
  Campaign = 1,
  ActionPlan = 2,
  ReportDefinition = 3,
  Custom = 99
}

export enum WorkflowStatus {
  Draft = 0,
  Active = 1,
  Archived = 2
}

export enum WorkflowInstanceState {
  Running = 0,
  Completed = 1,
  Cancelled = 2,
  Failed = 3
}

export enum ApprovalStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  Cancelled = 3,
  Expired = 4
}

// --- تعاریف: درخواست‌ها -----------------------------------------------------------

export interface WorkflowSearchRequest {
  searchText?: string | null;
  status?: WorkflowStatus | null;
  entityType?: WorkflowEntityType | null;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface WorkflowStateRequest {
  code: string;
  name: string;
  isInitial: boolean;
  isFinal: boolean;
  displayOrder: number;
}

export interface WorkflowTransitionRequest {
  code: string;
  name: string;
  fromStateCode: string;
  toStateCode: string;
  requiresApproval: boolean;
  /** الزامی اگر requiresApproval باشد. */
  approverPermission?: string | null;
  displayOrder: number;
}

export interface SaveWorkflowRequest {
  name: string;
  code: string;
  description?: string | null;
  entityType: WorkflowEntityType;
  states: WorkflowStateRequest[];
  transitions?: WorkflowTransitionRequest[] | null;
  activateImmediately: boolean;
}

// --- نمونه‌ها: درخواست‌ها -----------------------------------------------------------

export interface WorkflowInstanceSearchRequest {
  searchText?: string | null;
  status?: WorkflowInstanceState | null;
  workflowId?: string | null;
  entityType?: WorkflowEntityType | null;
  entityId?: string | null;
  runningOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface StartWorkflowInstanceRequest {
  workflowCode: string;
  entityType: WorkflowEntityType;
  entityId: string;
  contextJson?: string | null;
}

export interface TransitionWorkflowInstanceRequest {
  transitionCode: string;
  note?: string | null;
  approvalExpiresAtUtc?: string | null;
}

// --- تأییدها: درخواست‌ها -----------------------------------------------------------

export interface WorkflowApprovalSearchRequest {
  status?: ApprovalStatus | null;
  instanceId?: string | null;
  /** فقط درخواست‌های قابل تصمیم‌گیری توسط کاربر جاری. */
  approvableByMe?: boolean;
  pendingOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface DecideWorkflowApprovalRequest {
  note?: string | null;
}

// --- خروجی‌ها -----------------------------------------------------------------------

export interface WorkflowStateDto {
  id: string;
  code: string;
  name: string;
  isInitial: boolean;
  isFinal: boolean;
  displayOrder: number;
}

export interface WorkflowTransitionDto {
  id: string;
  code: string;
  name: string;
  fromStateCode: string;
  toStateCode: string;
  requiresApproval: boolean;
  approverPermission?: string | null;
  displayOrder: number;
}

export interface WorkflowDto {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  entityType: WorkflowEntityType;
  status: WorkflowStatus;
  version: number;
  createdByUserId?: string | null;
  createdByUserName?: string | null;
  createdAt: string;
  states: WorkflowStateDto[];
  transitions: WorkflowTransitionDto[];
  /** نقشه‌ی گذارهای مجاز از هر وضعیت: «از کد» → کدهای گذار. */
  availableTransitions: Record<string, string[]>;
}

export interface WorkflowApprovalRequestDto {
  id: string;
  instanceId: string;
  transitionId: string;
  transitionCode: string;
  fromStateCode: string;
  toStateCode: string;
  approverPermission?: string | null;
  status: ApprovalStatus;
  requestedAt: string;
  requestedById?: string | null;
  decidedAt?: string | null;
  decidedById?: string | null;
  decidedByUserName?: string | null;
  decisionNote?: string | null;
  expiresAt?: string | null;
  canCurrentUserDecide: boolean;
}

export interface WorkflowInstanceDto {
  id: string;
  workflowId: string;
  workflowCode: string;
  workflowVersion: number;
  entityType: WorkflowEntityType;
  entityId: string;
  currentStateCode: string;
  status: WorkflowInstanceState;
  startedAt: string;
  completedAt?: string | null;
  cancelledAt?: string | null;
  startedByUserId?: string | null;
  startedByUserName?: string | null;
  contextJson?: string | null;
  transitionCount: number;
  /** گذارهای مجاز از وضعیت جاری. */
  nextTransitions: WorkflowTransitionDto[];
  pendingApproval?: WorkflowApprovalRequestDto | null;
}

export interface WorkflowStatsDto {
  totalWorkflows: number;
  activeWorkflows: number;
  runningInstances: number;
  completedInstances: number;
  pendingApprovals: number;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

function buildWorkflowParams(request: WorkflowSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.status !== null && request.status !== undefined) params.status = String(request.status);
  if (request.entityType !== null && request.entityType !== undefined) params.entityType = String(request.entityType);
  if (request.includeArchived) params.includeArchived = 'true';
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 20);
  return params;
}

function buildInstanceParams(request: WorkflowInstanceSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.status !== null && request.status !== undefined) params.status = String(request.status);
  if (request.workflowId) params.workflowId = request.workflowId;
  if (request.entityType !== null && request.entityType !== undefined) params.entityType = String(request.entityType);
  if (request.entityId) params.entityId = request.entityId;
  if (request.runningOnly) params.runningOnly = 'true';
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 20);
  return params;
}

function buildApprovalParams(request: WorkflowApprovalSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.status !== null && request.status !== undefined) params.status = String(request.status);
  if (request.instanceId) params.instanceId = request.instanceId;
  if (request.approvableByMe) params.approvableByMe = 'true';
  if (request.pendingOnly) params.pendingOnly = 'true';
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 20);
  return params;
}

export const workflowsApi = {
  search: (culture: Culture, request: WorkflowSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<WorkflowDto>>(
      culture,
      appendSearchParams('/workflows', buildWorkflowParams(request)),
      { signal }
    ),

  getStats: (culture: Culture, signal?: AbortSignal) =>
    apiRequest<WorkflowStatsDto>(culture, '/workflows/stats', { signal }),

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<WorkflowDto>(culture, `/workflows/${id}`, { signal }),

  create: (culture: Culture, request: SaveWorkflowRequest) =>
    apiRequest<WorkflowDto>(culture, '/workflows', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SaveWorkflowRequest) =>
    apiPut<WorkflowDto>(culture, `/workflows/${id}`, request),

  activate: (culture: Culture, id: string) =>
    apiRequest<WorkflowDto>(culture, `/workflows/${id}/activate`, { method: 'POST' }),

  archive: (culture: Culture, id: string) => apiDelete<void>(culture, `/workflows/${id}`),

  // --- نمونه‌ها -----------------------------------------------------------------

  searchInstances: (culture: Culture, request: WorkflowInstanceSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<WorkflowInstanceDto>>(
      culture,
      appendSearchParams('/workflow-instances', buildInstanceParams(request)),
      { signal }
    ),

  getInstance: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<WorkflowInstanceDto>(culture, `/workflow-instances/${id}`, { signal }),

  startInstance: (culture: Culture, request: StartWorkflowInstanceRequest) =>
    apiRequest<WorkflowInstanceDto>(culture, '/workflow-instances', { method: 'POST', body: request }),

  transitionInstance: (culture: Culture, id: string, request: TransitionWorkflowInstanceRequest) =>
    apiRequest<WorkflowInstanceDto>(culture, `/workflow-instances/${id}/transition`, {
      method: 'POST',
      body: request
    }),

  cancelInstance: (culture: Culture, id: string) =>
    apiRequest<WorkflowInstanceDto>(culture, `/workflow-instances/${id}/cancel`, { method: 'POST' }),

  // --- تأییدها -------------------------------------------------------------------

  searchApprovals: (culture: Culture, request: WorkflowApprovalSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<WorkflowApprovalRequestDto>>(
      culture,
      appendSearchParams('/workflow-approvals', buildApprovalParams(request)),
      { signal }
    ),

  getApproval: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<WorkflowApprovalRequestDto>(culture, `/workflow-approvals/${id}`, { signal }),

  approve: (culture: Culture, id: string, request: DecideWorkflowApprovalRequest) =>
    apiRequest<WorkflowApprovalRequestDto>(culture, `/workflow-approvals/${id}/approve`, {
      method: 'POST',
      body: request
    }),

  reject: (culture: Culture, id: string, request: DecideWorkflowApprovalRequest) =>
    apiRequest<WorkflowApprovalRequestDto>(culture, `/workflow-approvals/${id}/reject`, {
      method: 'POST',
      body: request
    })
};
