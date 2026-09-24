import { apiRequest } from './client';
import type { Culture } from '@/i18n/types';

export enum QuestionnaireStatus {
  Draft = 1,
  Published = 2,
  Archived = 3
}

export interface QuestionnaireSummary {
  id: string;
  code: string;
  status: QuestionnaireStatus;
  version: number;
  title: string;
  sectionCount: number;
  itemCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface QuestionnaireSearchRequest {
  searchText?: string | null;
  status?: QuestionnaireStatus | null;
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

export const questionnairesApi = {
  search: (culture: Culture, request: QuestionnaireSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.searchText) params.searchText = request.searchText;
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 50);

    return apiRequest<PagedResult<QuestionnaireSummary>>(
      culture,
      appendSearchParams('/questionnaires', params),
      { signal }
    );
  },

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<QuestionnaireSummary>(culture, `/questionnaires/${id}`, { signal }),

  publish: (culture: Culture, id: string) =>
    apiRequest<QuestionnaireSummary>(culture, `/questionnaires/${id}/publish`, { method: 'POST' })
};
