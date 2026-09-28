import { apiDelete, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import type { PagedResult } from './users';

/**
 * قرارداد API ماژول مدیریت اقدامات و پیگیری.
 *
 * **حریم خصوصی:** برنامه‌ها و آیتم‌های اقدام هرگز شناسه‌ی پاسخ‌گوی یک
 * نظرسنجی را ذخیره یا منتقل نمی‌کنند — حتی برای نظرسنجی‌های غیرناشناس.
 * فقط شاخص‌های تجمعی (NPS/CSAT/CES) و متادیتای عمومی (عنوان، مسئول، مهلت).
 */

export enum ActionPlanStatus {
  Draft = 0,
  Active = 1,
  Completed = 2,
  Cancelled = 3,
  Archived = 4
}

export enum ActionItemStatus {
  Open = 1,
  InProgress = 2,
  Done = 3,
  Cancelled = 4
}

export enum ActionPriority {
  Low = 1,
  Medium = 2,
  High = 3,
  Critical = 4
}

export enum ActionSource {
  Manual = 0,
  AnalyticsAlert = 1,
  SurveyFinding = 2
}

export enum EscalationLevel {
  None = 0,
  Reminder = 1,
  EscalatedToOwner = 2,
  EscalatedToManagement = 3
}

export enum EffectivenessRating {
  NotAssessed = 0,
  Effective = 1,
  PartiallyEffective = 2,
  Ineffective = 3
}

export interface ActionPlanSearchRequest {
  searchText?: string | null;
  status?: ActionPlanStatus | null;
  priority?: ActionPriority | null;
  source?: ActionSource | null;
  surveyId?: string | null;
  orgUnitId?: string | null;
  includeDescendants?: boolean;
  mineOnly?: boolean;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ActionItemSearchRequest {
  searchText?: string | null;
  status?: ActionItemStatus | null;
  priority?: ActionPriority | null;
  planId?: string | null;
  surveyId?: string | null;
  orgUnitId?: string | null;
  includeDescendants?: boolean;
  assignedToMe?: boolean;
  overdueOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SaveActionItemRequest {
  title: string;
  description?: string | null;
  assigneeUserId?: string | null;
  priority: ActionPriority;
  displayOrder: number;
  dueDate?: string | null;
  remindAt?: string | null;
}

export interface SaveActionPlanRequest {
  title: string;
  description?: string | null;
  surveyId?: string | null;
  campaignId?: string | null;
  orgUnitId?: string | null;
  ownerUserId?: string | null;
  priority: ActionPriority;
  dueDate?: string | null;
  activateImmediately: boolean;
  items?: SaveActionItemRequest[] | null;
}

export interface TransitionActionItemRequest {
  newStatus: ActionItemStatus;
}

export interface AssessEffectivenessRequest {
  rating: EffectivenessRating;
  note?: string | null;
}

export interface RecordOutcomeRequest {
  value?: number | null;
}

export interface AddActionCommentRequest {
  body: string;
}

export interface ActionPlanDto {
  id: string;
  title: string;
  description?: string | null;
  source: ActionSource;
  status: ActionPlanStatus;
  priority: ActionPriority;

  surveyId?: string | null;
  surveyCode?: string | null;
  surveyTitle?: string | null;
  campaignId?: string | null;

  triggerMetricType?: number | null;
  triggerMetricValue?: number | null;
  outcomeMetricValue?: number | null;
  outcomeMeasuredAt?: string | null;

  orgUnitId?: string | null;
  orgUnitPath?: string | null;

  ownerUserId?: string | null;
  ownerUserName?: string | null;
  createdByUserId?: string | null;
  createdByUserName?: string | null;

  dueDate?: string | null;
  completedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;

  totalItemCount: number;
  completedItemCount: number;
  progressPercentage: number;
  effectivenessScore?: number | null;
  hasOverdueItems: boolean;
}

export interface ActionItemDto {
  id: string;
  actionPlanId: string;

  planTitle?: string | null;
  planStatus?: ActionPlanStatus | null;
  planPriority?: ActionPriority | null;

  title: string;
  description?: string | null;
  assigneeUserId?: string | null;
  assigneeUserName?: string | null;
  priority: ActionPriority;
  status: ActionItemStatus;
  displayOrder: number;
  dueDate?: string | null;
  remindAt?: string | null;
  escalationLevel: EscalationLevel;
  escalatedAt?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;

  effectiveness: EffectivenessRating;
  effectivenessNote?: string | null;
  effectivenessAssessedAt?: string | null;

  commentCount: number;
  evidenceCount: number;

  orgUnitId?: string | null;
  orgUnitPath?: string | null;

  isAssignedToMe: boolean;
  isOverdue: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ActionCommentDto {
  id: string;
  actionItemId: string;
  authorUserId?: string | null;
  authorUserName?: string | null;
  body: string;
  createdAt: string;
}

export interface ActionEvidenceDto {
  id: string;
  actionItemId: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedByUserId?: string | null;
  uploadedByUserName?: string | null;
  uploadedAt: string;
}

export interface ActionStatsDto {
  totalOpenPlans: number;
  totalActivePlans: number;
  totalCompletedPlans: number;
  myOpenItems: number;
  myOverdueItems: number;
  openItems: number;
  overdueItems: number;
  escalatedItems: number;
  completedThisPeriod: number;
  averagePlanProgress: number;
  effectivenessScore?: number | null;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

function buildPlanParams(request: ActionPlanSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.status !== null && request.status !== undefined) params.status = String(request.status);
  if (request.priority !== null && request.priority !== undefined) params.priority = String(request.priority);
  if (request.source !== null && request.source !== undefined) params.source = String(request.source);
  if (request.surveyId) params.surveyId = request.surveyId;
  if (request.orgUnitId) params.orgUnitId = request.orgUnitId;
  if (request.includeDescendants) params.includeDescendants = 'true';
  if (request.mineOnly) params.mineOnly = 'true';
  if (request.includeArchived) params.includeArchived = 'true';
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 20);
  return params;
}

function buildItemParams(request: ActionItemSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.status !== null && request.status !== undefined) params.status = String(request.status);
  if (request.priority !== null && request.priority !== undefined) params.priority = String(request.priority);
  if (request.planId) params.planId = request.planId;
  if (request.surveyId) params.surveyId = request.surveyId;
  if (request.orgUnitId) params.orgUnitId = request.orgUnitId;
  if (request.includeDescendants) params.includeDescendants = 'true';
  if (request.assignedToMe) params.assignedToMe = 'true';
  if (request.overdueOnly) params.overdueOnly = 'true';
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 20);
  return params;
}

export const actionsApi = {
  searchPlans: (culture: Culture, request: ActionPlanSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<ActionPlanDto>>(
      culture,
      appendSearchParams('/actions/plans', buildPlanParams(request)),
      { signal }
    ),

  getStats: (culture: Culture, signal?: AbortSignal) =>
    apiRequest<ActionStatsDto>(culture, '/actions/plans/stats', { signal }),

  getPlan: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<ActionPlanDto>(culture, `/actions/plans/${id}`, { signal }),

  createPlan: (culture: Culture, request: SaveActionPlanRequest) =>
    apiRequest<ActionPlanDto>(culture, '/actions/plans', { method: 'POST', body: request }),

  updatePlan: (culture: Culture, id: string, request: SaveActionPlanRequest) =>
    apiPut<ActionPlanDto>(culture, `/actions/plans/${id}`, request),

  activatePlan: (culture: Culture, id: string) =>
    apiRequest<ActionPlanDto>(culture, `/actions/plans/${id}/activate`, { method: 'POST' }),

  completePlan: (culture: Culture, id: string) =>
    apiRequest<ActionPlanDto>(culture, `/actions/plans/${id}/complete`, { method: 'POST' }),

  cancelPlan: (culture: Culture, id: string) =>
    apiRequest<ActionPlanDto>(culture, `/actions/plans/${id}/cancel`, { method: 'POST' }),

  archivePlan: (culture: Culture, id: string) => apiDelete<void>(culture, `/actions/plans/${id}`),

  recordOutcome: (culture: Culture, id: string, request: RecordOutcomeRequest) =>
    apiRequest<ActionPlanDto>(culture, `/actions/plans/${id}/outcome`, { method: 'POST', body: request }),

  createItem: (culture: Culture, planId: string, request: SaveActionItemRequest) =>
    apiRequest<ActionItemDto>(culture, `/actions/plans/${planId}/items`, { method: 'POST', body: request }),

  searchItems: (culture: Culture, request: ActionItemSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<ActionItemDto>>(
      culture,
      appendSearchParams('/actions/items', buildItemParams(request)),
      { signal }
    ),

  getItem: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<ActionItemDto>(culture, `/actions/items/${id}`, { signal }),

  updateItem: (culture: Culture, id: string, request: SaveActionItemRequest) =>
    apiPut<ActionItemDto>(culture, `/actions/items/${id}`, request),

  transitionItem: (culture: Culture, id: string, request: TransitionActionItemRequest) =>
    apiRequest<ActionItemDto>(culture, `/actions/items/${id}/transition`, { method: 'POST', body: request }),

  assessEffectiveness: (culture: Culture, id: string, request: AssessEffectivenessRequest) =>
    apiRequest<ActionItemDto>(culture, `/actions/items/${id}/effectiveness`, { method: 'POST', body: request }),

  listComments: (culture: Culture, itemId: string, signal?: AbortSignal) =>
    apiRequest<ActionCommentDto[]>(culture, `/actions/items/${itemId}/comments`, { signal }),

  addComment: (culture: Culture, itemId: string, request: AddActionCommentRequest) =>
    apiRequest<ActionCommentDto>(culture, `/actions/items/${itemId}/comments`, { method: 'POST', body: request }),

  listEvidence: (culture: Culture, itemId: string, signal?: AbortSignal) =>
    apiRequest<ActionEvidenceDto[]>(culture, `/actions/items/${itemId}/evidence`, { signal }),

  /**
   * آپلود پیوست با multipart/form-data. محتوا هرگز در پایگاه داده ذخیره
   * نمی‌شود — فقط متادیتای آن.
   */
  uploadEvidence: async (culture: Culture, itemId: string, file: File): Promise<ActionEvidenceDto> => {
    const formData = new FormData();
    formData.append('File', file);

    const response = await fetch(`/api/${culture}/actions/items/${itemId}/evidence`, {
      method: 'POST',
      credentials: 'same-origin',
      body: formData
    });

    if (!response.ok) {
      throw new Error(`آپلود پیوست با وضعیت ${response.status} شکست خورد.`);
    }

    return (await response.json()) as ActionEvidenceDto;
  },

  /**
   * دانلود محتوای یک پیوست. یک blob URL برمی‌گرداند که فراخوان باید آن را
   * revoke کند.
   */
  downloadEvidence: async (culture: Culture, evidenceId: string): Promise<Blob> => {
    const response = await fetch(`/api/${culture}/actions/evidence/${evidenceId}`, {
      credentials: 'same-origin'
    });

    if (!response.ok) {
      throw new Error(`دانلود پیوست با وضعیت ${response.status} شکست خورد.`);
    }

    return response.blob();
  },

  deleteEvidence: (culture: Culture, evidenceId: string) =>
    apiDelete<void>(culture, `/actions/evidence/${evidenceId}`)
};
