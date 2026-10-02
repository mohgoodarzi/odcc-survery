import { apiPost, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import { Language, type PagedResult } from './surveys';

export { Language };

/**
 * نوع سؤال — قرینه‌ی `QuestionType` سمت سرور.
 */
export enum QuestionType {
  SingleChoice = 1,
  MultipleChoice = 2,
  Rating = 3,
  YesNo = 4,
  ShortText = 5,
  LongText = 6,
  Number = 7
}

export interface QuestionLocalization {
  language: Language;
  text: string;
  description?: string | null;
}

export interface QuestionOptionLocalization {
  language: Language;
  text: string;
}

export interface QuestionOption {
  id: string;
  code: string;
  displayOrder: number;
  /** متن گزینه در زبان درخواست‌شده. */
  text: string;
  localizations: QuestionOptionLocalization[];
}

export interface Question {
  id: string;
  code: string;
  type: QuestionType;
  scaleMax: number;
  isArchived: boolean;
  currentVersionNumber: number;
  /** متن سؤال در زبان درخواست‌شده. */
  text: string;
  description?: string | null;
  tags: string[];
  options: QuestionOption[];
  localizations: QuestionLocalization[];
  createdAt: string;
  updatedAt: string | null;
}

export interface QuestionSearchRequest {
  searchText?: string | null;
  type?: QuestionType | null;
  tag?: string | null;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SaveQuestionOptionRequest {
  /** شناسه‌ی گزینه‌ی موجود (در ویرایش؛ خالی برای گزینه‌ی جدید). */
  id?: string | null;
  code: string;
  displayOrder: number;
  localizations: QuestionOptionLocalization[];
}

export interface SaveQuestionRequest {
  code: string;
  type: QuestionType;
  scaleMax?: number;
  isArchived?: boolean;
  tags?: string[];
  /** ترجمه‌های سؤال — حداقل یک ترجمه‌ی فارسی الزامی است. */
  localizations: QuestionLocalization[];
  /** گزینه‌ها — الزامی برای انواع گزینه‌ای، ممنوع برای سایر انواع. */
  options?: SaveQuestionOptionRequest[];
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

export const questionsApi = {
  search: (culture: Culture, request: QuestionSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.searchText) params.searchText = request.searchText;
    if (request.type !== null && request.type !== undefined) {
      params.type = String(request.type);
    }
    if (request.tag) params.tag = request.tag;
    if (request.includeArchived) params.includeArchived = 'true';
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 100);

    return apiRequest<PagedResult<Question>>(
      culture,
      appendSearchParams('/question-bank/questions', params),
      { signal }
    );
  },

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<Question>(culture, `/question-bank/questions/${id}`, { signal }),

  create: (culture: Culture, request: SaveQuestionRequest) =>
    apiPost<Question>(culture, '/question-bank/questions', request)
};
