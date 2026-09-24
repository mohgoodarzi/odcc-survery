import { apiDelete, apiPost, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import { Language, type PagedResult } from './surveys';

export { Language };

export enum CampaignStatus {
  Draft = 1,
  Scheduled = 2,
  Running = 3,
  Completed = 4,
  Archived = 5
}

export enum TargetAudienceType {
  AllCompany = 1,
  OrgUnits = 2,
  Employees = 3
}

export enum DistributionChannel {
  Email = 1,
  Sms = 2,
  InApp = 3,
  PublicLink = 4
}

export enum DistributionStatus {
  Pending = 1,
  Sent = 2,
  Failed = 3,
  Responded = 4
}

export enum ReminderStatus {
  Scheduled = 1,
  Sent = 2,
  Cancelled = 3
}

export interface CampaignLocalization {
  language: Language;
  title: string;
  description?: string | null;
}

export interface ReminderLocalization {
  language: Language;
  subject: string;
  body?: string | null;
}

export interface Reminder {
  id: string;
  sendAt: string;
  status: ReminderStatus;
  sentAt: string | null;
  localizations: ReminderLocalization[];
  subject: string;
}

export interface SaveReminderRequest {
  id?: string | null;
  sendAt: string;
  localizations: ReminderLocalization[];
}

export interface CampaignTargetUnit {
  id: string;
  orgUnitId: string;
  orgUnitName?: string | null;
  includeDescendants: boolean;
}

export interface CampaignTargetMember {
  id: string;
  employeeId: string;
  employeeName?: string | null;
}

export interface DistributionStatusCount {
  status: DistributionStatus;
  count: number;
}

export interface CampaignSummary {
  id: string;
  code: string;
  status: CampaignStatus;
  title: string;
  surveyCode: string;
  audienceType: TargetAudienceType;
  channel: DistributionChannel;
  scheduledAt: string | null;
  startedAt: string | null;
  totalDistributions: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface Campaign {
  id: string;
  code: string;
  status: CampaignStatus;
  surveyId: string;
  surveyCode: string;
  audienceType: TargetAudienceType;
  includeInactiveEmployees: boolean;
  channel: DistributionChannel;
  scheduledAt: string | null;
  endsAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  archivedAt: string | null;
  title: string;
  description: string | null;
  localizations: CampaignLocalization[];
  targetUnits: CampaignTargetUnit[];
  targetMembers: CampaignTargetMember[];
  reminders: Reminder[];
  isAudienceConfigured: boolean;
  canLaunch: boolean;
  totalDistributions: number;
  distributionCounts: DistributionStatusCount[];
  createdAt: string;
  updatedAt: string | null;
}

export interface Distribution {
  id: string;
  campaignId: string;
  employeeId: string;
  employeeName: string | null;
  workEmail: string | null;
  status: DistributionStatus;
  sentAt: string | null;
  respondedAt: string | null;
  failureReason: string | null;
  reminderCount: number;
}

export interface SaveCampaignRequest {
  code: string;
  surveyId: string;
  audienceType: TargetAudienceType;
  includeInactiveEmployees: boolean;
  channel: DistributionChannel;
  scheduledAt?: string | null;
  endsAt?: string | null;
  localizations: CampaignLocalization[];
  targetOrgUnitIds: string[];
  targetEmployeeIds: string[];
  reminders: SaveReminderRequest[];
}

export interface CampaignSearchRequest {
  searchText?: string | null;
  status?: CampaignStatus | null;
  surveyId?: string | null;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface DistributionSearchRequest {
  status?: DistributionStatus | null;
  page?: number;
  pageSize?: number;
}

export interface CampaignLaunchResult {
  campaignId: string;
  code: string;
  status: CampaignStatus;
  resolvedRecipientCount: number;
  createdDistributionCount: number;
}

export interface ReminderProcessResult {
  remindersProcessed: number;
  recipientsNotified: number;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

export const campaignsApi = {
  search: (culture: Culture, request: CampaignSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.searchText) params.searchText = request.searchText;
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    if (request.surveyId) params.surveyId = request.surveyId;
    if (request.includeArchived) params.includeArchived = 'true';
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<CampaignSummary>>(
      culture,
      appendSearchParams('/campaigns', params),
      { signal }
    );
  },

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<Campaign>(culture, `/campaigns/${id}`, { signal }),

  create: (culture: Culture, request: SaveCampaignRequest) =>
    apiRequest<Campaign>(culture, '/campaigns', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SaveCampaignRequest) =>
    apiPut<Campaign>(culture, `/campaigns/${id}`, request),

  schedule: (culture: Culture, id: string, scheduledAt: string) =>
    apiPost<Campaign>(culture, `/campaigns/${id}/schedule`, { scheduledAt }),

  launch: (culture: Culture, id: string) =>
    apiPost<CampaignLaunchResult>(culture, `/campaigns/${id}/launch`),

  complete: (culture: Culture, id: string) =>
    apiPost<Campaign>(culture, `/campaigns/${id}/complete`),

  archive: (culture: Culture, id: string) =>
    apiPost<Campaign>(culture, `/campaigns/${id}/archive`),

  delete: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/campaigns/${id}`),

  distributions: (culture: Culture, id: string, request: DistributionSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 50);

    return apiRequest<PagedResult<Distribution>>(
      culture,
      appendSearchParams(`/campaigns/${id}/distributions`, params),
      { signal }
    );
  },

  processDueReminders: (culture: Culture) =>
    apiPost<ReminderProcessResult>(culture, '/campaigns/reminders:process-due'),

  cancelReminder: (culture: Culture, id: string, reminderId: string) =>
    apiPost<void>(culture, `/campaigns/${id}/reminders/${reminderId}:cancel`)
};
