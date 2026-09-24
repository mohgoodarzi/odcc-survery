import { apiDelete, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';

export enum SurveyStatus {
  Draft = 1,
  Scheduled = 2,
  Active = 3,
  Paused = 4,
  Closed = 5,
  Archived = 6
}

export enum SurveyTemplateStatus {
  Active = 1,
  Archived = 2
}

export enum Language {
  Fa = 1,
  En = 2
}

export interface SurveyLocalization {
  language: Language;
  title: string;
  description?: string | null;
  welcomeMessage?: string | null;
  thankYouMessage?: string | null;
}

export interface SurveyTemplateLocalization {
  language: Language;
  title: string;
  description?: string | null;
}

export interface SurveySummary {
  id: string;
  code: string;
  status: SurveyStatus;
  title: string;
  questionnaireCode: string;
  isAnonymous: boolean;
  startDate: string | null;
  endDate: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface Survey {
  id: string;
  code: string;
  status: SurveyStatus;
  questionnaireId: string;
  questionnaireVersion: number;
  questionnaireCode: string;
  templateId: string | null;
  isAnonymous: boolean;
  allowEditResponse: boolean;
  showProgressBar: boolean;
  singleResponsePerUser: boolean;
  startDate: string | null;
  endDate: string | null;
  estimatedMinutes: number;
  title: string;
  description: string | null;
  welcomeMessage: string | null;
  thankYouMessage: string | null;
  localizations: SurveyLocalization[];
  isPublishable: boolean;
  acceptsResponses: boolean;
  publishedAt: string | null;
  activatedAt: string | null;
  closedAt: string | null;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface SurveyTemplateSummary {
  id: string;
  code: string;
  status: SurveyTemplateStatus;
  title: string;
  questionnaireCode: string;
  isAnonymous: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface SurveyTemplate {
  id: string;
  code: string;
  status: SurveyTemplateStatus;
  questionnaireId: string;
  questionnaireCode: string;
  isAnonymous: boolean;
  allowEditResponse: boolean;
  showProgressBar: boolean;
  singleResponsePerUser: boolean;
  estimatedMinutes: number;
  title: string;
  description: string | null;
  localizations: SurveyTemplateLocalization[];
  isUsable: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface SaveSurveyRequest {
  code: string;
  questionnaireId: string;
  localizations: SurveyLocalization[];
  isAnonymous: boolean;
  allowEditResponse: boolean;
  showProgressBar: boolean;
  singleResponsePerUser: boolean;
  startDate?: string | null;
  endDate?: string | null;
  estimatedMinutes: number;
}

export interface SaveSurveyTemplateRequest {
  code: string;
  questionnaireId: string;
  localizations: SurveyTemplateLocalization[];
  isAnonymous: boolean;
  allowEditResponse: boolean;
  showProgressBar: boolean;
  singleResponsePerUser: boolean;
  estimatedMinutes: number;
}

export interface CreateSurveyFromTemplateRequest {
  templateId: string;
  code: string;
  localizations?: SurveyLocalization[] | null;
  startDate?: string | null;
  endDate?: string | null;
}

export interface SurveySearchRequest {
  searchText?: string | null;
  status?: SurveyStatus | null;
  questionnaireId?: string | null;
  templateId?: string | null;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SurveyTemplateSearchRequest {
  searchText?: string | null;
  status?: SurveyTemplateStatus | null;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

export const surveysApi = {
  search: (culture: Culture, request: SurveySearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.searchText) params.searchText = request.searchText;
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    if (request.questionnaireId) params.questionnaireId = request.questionnaireId;
    if (request.templateId) params.templateId = request.templateId;
    if (request.includeArchived) params.includeArchived = 'true';
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<SurveySummary>>(
      culture,
      appendSearchParams('/surveys', params),
      { signal }
    );
  },

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<Survey>(culture, `/surveys/${id}`, { signal }),

  create: (culture: Culture, request: SaveSurveyRequest) =>
    apiRequest<Survey>(culture, '/surveys', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SaveSurveyRequest) =>
    apiPut<Survey>(culture, `/surveys/${id}`, request),

  publish: (culture: Culture, id: string) =>
    apiRequest<Survey>(culture, `/surveys/${id}/publish`, { method: 'POST' }),

  start: (culture: Culture, id: string) =>
    apiRequest<Survey>(culture, `/surveys/${id}/start`, { method: 'POST' }),

  pause: (culture: Culture, id: string) =>
    apiRequest<Survey>(culture, `/surveys/${id}/pause`, { method: 'POST' }),

  resume: (culture: Culture, id: string) =>
    apiRequest<Survey>(culture, `/surveys/${id}/resume`, { method: 'POST' }),

  close: (culture: Culture, id: string) =>
    apiRequest<Survey>(culture, `/surveys/${id}/close`, { method: 'POST' }),

  archive: (culture: Culture, id: string) =>
    apiRequest<Survey>(culture, `/surveys/${id}/archive`, { method: 'POST' }),

  delete: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/surveys/${id}`),

  createFromTemplate: (culture: Culture, request: CreateSurveyFromTemplateRequest) =>
    apiRequest<Survey>(culture, '/surveys/from-template', { method: 'POST', body: request })
};

export const surveyTemplatesApi = {
  search: (culture: Culture, request: SurveyTemplateSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.searchText) params.searchText = request.searchText;
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<SurveyTemplateSummary>>(
      culture,
      appendSearchParams('/survey-templates', params),
      { signal }
    );
  },

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<SurveyTemplate>(culture, `/survey-templates/${id}`, { signal }),

  create: (culture: Culture, request: SaveSurveyTemplateRequest) =>
    apiRequest<SurveyTemplate>(culture, '/survey-templates', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SaveSurveyTemplateRequest) =>
    apiPut<SurveyTemplate>(culture, `/survey-templates/${id}`, request),

  archive: (culture: Culture, id: string) =>
    apiRequest<SurveyTemplate>(culture, `/survey-templates/${id}/archive`, { method: 'POST' }),

  delete: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/survey-templates/${id}`)
};
