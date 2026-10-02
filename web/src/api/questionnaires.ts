import { apiDelete, apiPost, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import { Language, type PagedResult } from './surveys';

/**
 * وضعیت چرخه‌ی عمر پرسشنامه — قرینه‌ی `QuestionnaireStatus` سمت سرور.
 * `Published` نام قدیمیِ `Active` است برای جلوگیری از شکستن کدهای موجود.
 */
export enum QuestionnaireStatus {
  Draft = 1,
  Active = 2,
  Archived = 3,
  Published = 2
}

/**
 * شرایط مجاز برای شرط انشعاب — قرینه‌ی `BranchingCondition` سمت سرور.
 */
export enum BranchingCondition {
  Equals = 1,
  NotEquals = 2,
  Contains = 3,
  GreaterThan = 4,
  LessThan = 5
}

export interface QuestionnaireLocalization {
  language: Language;
  title: string;
  description?: string | null;
}

export interface SectionLocalization {
  language: Language;
  title: string;
}

export interface BranchingRule {
  id: string;
  targetItemId: string;
  condition: BranchingCondition;
  expectedValue: string;
}

export interface QuestionnaireItem {
  id: string;
  sectionId: string;
  questionId: string;
  questionVersionNumber: number;
  questionCode: string;
  questionType: number;
  displayOrder: number;
  isRequired: boolean;
  titleOverride?: string | null;
  /** متن سؤال در زبان درخواست‌شده (برای نمایش سریع در فهرست). */
  questionText: string;
  branchingRules: BranchingRule[];
}

export interface QuestionnaireSection {
  id: string;
  displayOrder: number;
  isOptional: boolean;
  /** عنوان بخش در زبان درخواست‌شده. */
  title: string;
  localizations: SectionLocalization[];
  items: QuestionnaireItem[];
}

export interface Questionnaire {
  id: string;
  code: string;
  status: QuestionnaireStatus;
  version: number;
  title: string;
  description: string | null;
  localizations: QuestionnaireLocalization[];
  sections: QuestionnaireSection[];
  sectionCount: number;
  itemCount: number;
  isPublishable: boolean;
  createdAt: string;
  updatedAt: string | null;
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

export interface SaveBranchingRuleRequest {
  /** شناسه‌ی قانون موجود (در ویرایش؛ خالی برای قانون جدید). */
  id?: string | null;
  targetItemId: string;
  condition: BranchingCondition;
  expectedValue: string;
}

export interface SaveItemRequest {
  /** شناسه‌ی آیتم موجود (در ویرایش؛ خالی برای آیتم جدید). */
  id?: string | null;
  questionId: string;
  isRequired: boolean;
  titleOverride?: string | null;
  branchingRules: SaveBranchingRuleRequest[];
}

export interface SaveSectionRequest {
  /** شناسه‌ی بخش موجود (در ویرایش؛ خالی برای بخش جدید). */
  id?: string | null;
  isOptional: boolean;
  localizations: SectionLocalization[];
  items: SaveItemRequest[];
}

export interface SaveQuestionnaireRequest {
  code: string;
  localizations: QuestionnaireLocalization[];
  sections: SaveSectionRequest[];
}

export interface QuestionnaireSearchRequest {
  searchText?: string | null;
  status?: QuestionnaireStatus | null;
  page?: number;
  pageSize?: number;
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
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<QuestionnaireSummary>>(
      culture,
      appendSearchParams('/questionnaires', params),
      { signal }
    );
  },

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<Questionnaire>(culture, `/questionnaires/${id}`, { signal }),

  create: (culture: Culture, request: SaveQuestionnaireRequest) =>
    apiPost<Questionnaire>(culture, '/questionnaires', request),

  update: (culture: Culture, id: string, request: SaveQuestionnaireRequest) =>
    apiPut<Questionnaire>(culture, `/questionnaires/${id}`, request),

  publish: (culture: Culture, id: string) =>
    apiPost<Questionnaire>(culture, `/questionnaires/${id}/publish`),

  archive: (culture: Culture, id: string) =>
    apiPost<Questionnaire>(culture, `/questionnaires/${id}/archive`),

  delete: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/questionnaires/${id}`)
};
