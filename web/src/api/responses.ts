import { apiDelete, apiPost, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import { Language, type PagedResult } from './surveys';

export enum ResponseStatus {
  InProgress = 1,
  Submitted = 2
}

export enum ResponseSource {
  DirectLink = 1,
  CampaignEmail = 2,
  CampaignSms = 3,
  CampaignInApp = 4,
  PublicLink = 5
}

export enum QuestionType {
  SingleChoice = 1,
  MultipleChoice = 2,
  Rating = 3,
  Number = 4,
  ShortText = 5,
  LongText = 6,
  YesNo = 7
}

export enum BranchingCondition {
  Equals = 1,
  NotEquals = 2,
  Contains = 3,
  GreaterThan = 4,
  LessThan = 5
}

export interface ResponseAnswerSelection {
  optionId: string;
  optionCode: string;
  displayOrder: number;
}

export interface ResponseAnswer {
  id: string;
  questionnaireItemId: string;
  questionId: string;
  questionCode: string;
  questionType: QuestionType;
  displayOrder: number;
  textValue?: string | null;
  numericValue?: number | null;
  selections: ResponseAnswerSelection[];
  displayText: string;
}

export interface ResponseSession {
  id: string;
  surveyId: string;
  surveyCode: string;
  campaignId?: string | null;
  campaignCode?: string | null;
  status: ResponseStatus;
  source: ResponseSource;
  isAnonymous: boolean;
  respondentDisplayName?: string | null;
  startedAt: string;
  submittedAt?: string | null;
  lastActivityAt: string;
  answerCount: number;
  answers: ResponseAnswer[];
  isEditable: boolean;
}

export interface ResponseSessionSummary {
  id: string;
  surveyCode: string;
  campaignCode?: string | null;
  status: ResponseStatus;
  isAnonymous: boolean;
  respondentDisplayName?: string | null;
  startedAt: string;
  submittedAt?: string | null;
  answerCount: number;
  createdAt: string;
}

export interface RespondentOption {
  id: string;
  code: string;
  text: string;
  displayOrder: number;
}

export interface RespondentBranchingRule {
  targetItemId: string;
  condition: BranchingCondition;
  expectedValue: string;
}

export interface RespondentItem {
  id: string;
  sectionId: string;
  questionId: string;
  questionCode: string;
  questionType: QuestionType;
  displayOrder: number;
  isRequired: boolean;
  text: string;
  description?: string | null;
  scaleMax: number;
  options: RespondentOption[];
  branchingRules: RespondentBranchingRule[];
}

export interface RespondentSection {
  id: string;
  title: string;
  displayOrder: number;
  isOptional: boolean;
  items: RespondentItem[];
}

export interface RespondentSurveyContext {
  surveyId: string;
  surveyCode: string;
  title: string;
  description?: string | null;
  welcomeMessage?: string | null;
  thankYouMessage?: string | null;
  estimatedMinutes: number;
  showProgressBar: boolean;
  isAnonymous: boolean;
  allowEditResponse: boolean;
  endDate?: string | null;
  sections: RespondentSection[];
  session?: ResponseSession | null;
}

export interface RespondableSurvey {
  surveyId: string;
  surveyCode: string;
  title: string;
  description?: string | null;
  estimatedMinutes: number;
  isAnonymous: boolean;
  campaignId?: string | null;
  campaignCode?: string | null;
  distributionId?: string | null;
  sessionStatus?: ResponseStatus | null;
}

export interface SaveAnswerRequest {
  questionnaireItemId: string;
  textValue?: string | null;
  numericValue?: number | null;
  selectedOptionIds?: string[];
}

export interface SaveAnswersRequest {
  answers: SaveAnswerRequest[];
}

export interface SubmitResponseRequest extends SaveAnswersRequest {
  distributionId?: string | null;
}

export interface StartSessionRequest {
  surveyId: string;
  campaignId?: string | null;
  distributionId?: string | null;
  source?: ResponseSource;
  responseLanguage?: Language;
}

export interface ResponseSearchRequest {
  searchText?: string | null;
  surveyId?: string | null;
  status?: ResponseStatus | null;
  page?: number;
  pageSize?: number;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

export const responsesApi = {
  mySurveys: (culture: Culture, page = 1, pageSize = 50, signal?: AbortSignal) =>
    apiRequest<PagedResult<RespondableSurvey>>(
      culture,
      appendSearchParams('/responses/my-surveys', { page: String(page), pageSize: String(pageSize) }),
      { signal }
    ),

  getRespondentContext: (culture: Culture, surveyId: string, signal?: AbortSignal) =>
    apiRequest<RespondentSurveyContext>(culture, `/responses/surveys/${surveyId}/context`, { signal }),

  getMySession: (culture: Culture, surveyId: string, signal?: AbortSignal) =>
    apiRequest<ResponseSession>(culture, `/responses/surveys/${surveyId}/session`, { signal }),

  startSession: (culture: Culture, request: StartSessionRequest) =>
    apiRequest<ResponseSession>(culture, '/responses/sessions', { method: 'POST', body: request }),

  saveAnswers: (culture: Culture, sessionId: string, request: SaveAnswersRequest) =>
    apiPut<ResponseSession>(culture, `/responses/sessions/${sessionId}/answers`, request),

  submit: (culture: Culture, sessionId: string, request: SubmitResponseRequest) =>
    apiPost<ResponseSession>(culture, `/responses/sessions/${sessionId}/submit`, request),

  search: (culture: Culture, request: ResponseSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.searchText) params.searchText = request.searchText;
    if (request.surveyId) params.surveyId = request.surveyId;
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<ResponseSessionSummary>>(
      culture,
      appendSearchParams('/responses', params),
      { signal }
    );
  },

  getById: (culture: Culture, sessionId: string, signal?: AbortSignal) =>
    apiRequest<ResponseSession>(culture, `/responses/sessions/${sessionId}`, { signal }),

  delete: (culture: Culture, sessionId: string) =>
    apiDelete<void>(culture, `/responses/sessions/${sessionId}`)
};
