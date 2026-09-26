import { apiDelete, apiPost, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import type { PagedResult } from './surveys';
import { QuestionType } from './responses';

export { QuestionType };

/**
 * قرارداد API تحلیلات. همه‌ی خروجی‌ها فقط تجمع هستند — هیچ شناسه‌ی
 * پاسخ‌گویی (کاربر، کارمند یا نام) بازگردانده نمی‌شود.
 */

export enum AnalyticsSegment {
  Survey = 1,
  Campaign = 2,
  OrgUnit = 3,
  Source = 4
}

export enum AnalyticsPeriod {
  Day = 1,
  Week = 2,
  Month = 3
}

export enum MetricType {
  Nps = 1,
  Csat = 2,
  Ces = 3,
  CompletionRate = 4,
  ResponseRate = 5,
  AverageRating = 6
}

export interface AnalyticsFilter {
  from?: string | null;
  to?: string | null;
  campaignId?: string | null;
  orgUnitId?: string | null;
  includeDescendants?: boolean;
}

export interface ComputeAnalyticsRequest {
  surveyId: string;
  filter?: AnalyticsFilter;
  segmentType?: AnalyticsSegment;
  orgUnitId?: string | null;
  campaignId?: string | null;
  includeTextAnalytics?: boolean;
}

export interface AnalyticsSearchRequest {
  searchText?: string | null;
  surveyId?: string | null;
  segmentType?: AnalyticsSegment | null;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface TrendRequest {
  surveyId?: string | null;
  period?: AnalyticsPeriod;
  from?: string | null;
  to?: string | null;
}

export interface OptionDistribution {
  optionId: string;
  optionCode: string;
  optionText: string;
  displayOrder: number;
  count: number;
  percentage: number;
}

export interface RatingBucket {
  value: number;
  count: number;
  percentage: number;
}

export interface NumericStats {
  mean?: number | null;
  median?: number | null;
  min?: number | null;
  max?: number | null;
  standardDeviation?: number | null;
  responseCount: number;
  ratingBuckets: RatingBucket[];
}

export interface YesNoDistribution {
  yesCount: number;
  noCount: number;
  yesPercentage: number;
  noPercentage: number;
}

export interface SentimentDistribution {
  positivePercentage: number;
  neutralPercentage: number;
  negativePercentage: number;
  unknownPercentage: number;
  totalCount: number;
}

export interface ThemeExtractionResult {
  theme: string;
  occurrenceCount: number;
  relevanceScore: number;
}

export interface TextAnalytics {
  textResponseCount: number;
  sentiment: SentimentDistribution;
  themes: ThemeExtractionResult[];
}

export interface QuestionMetric {
  questionId: string;
  questionCode: string;
  questionText: string;
  questionType: QuestionType;
  displayOrder: number;
  responseCount: number;
  responseRate: number;
  options: OptionDistribution[];
  numericStats?: NumericStats | null;
  yesNoDistribution?: YesNoDistribution | null;
  textAnalytics?: TextAnalytics | null;
  detectedMetric?: MetricType | null;
}

export interface SurveyAnalytics {
  surveyId: string;
  surveyCode: string;
  surveyTitle: string;
  isAnonymous: boolean;
  canSegmentByOrgUnit: boolean;
  appliedFilter?: AnalyticsFilter;
  totalSessions: number;
  completedSessions: number;
  completionRate: number;
  npsScore?: number | null;
  npsPromoters: number;
  npsPassives: number;
  npsDetractors: number;
  csatScore?: number | null;
  cesScore?: number | null;
  averageRating?: number | null;
  questions: QuestionMetric[];
  trend: TrendPoint[];
  computedAt: string;
  metricId?: string | null;
}

export interface TrendPoint {
  periodLabel: string;
  periodStart: string;
  count: number;
  cumulativeCount: number;
  averageRating?: number | null;
  completionRate?: number | null;
}

export interface SurveyMetricSummary {
  id: string;
  surveyId: string;
  surveyCode: string;
  surveyTitle: string;
  isAnonymous: boolean;
  segmentType: AnalyticsSegment;
  segmentLabel?: string | null;
  totalSessions: number;
  completedSessions: number;
  completionRate: number;
  npsScore?: number | null;
  csatScore?: number | null;
  cesScore?: number | null;
  averageRating?: number | null;
  responseRate?: number | null;
  windowStart: string;
  windowEnd: string;
  computedAt: string;
}

export interface AnalyticsSegmentResult {
  label: string;
  totalSessions: number;
  completedSessions: number;
  completionRate: number;
  averageRating?: number | null;
}

export interface AnalyticsDashboard {
  totalSurveys: number;
  activeSurveys: number;
  totalCampaigns: number;
  activeCampaigns: number;
  totalSessions: number;
  completedSessions: number;
  completionRate: number;
  averageNps?: number | null;
  averageCsat?: number | null;
  averageCes?: number | null;
  averageRating?: number | null;
  trend: TrendPoint[];
  topSurveys: SurveyMetricSummary[];
  from?: string | null;
  to?: string | null;
}

export interface Benchmark {
  id: string;
  name: string;
  metric: MetricType;
  targetValue: number;
  isCompanyWide: boolean;
  orgUnitId?: string | null;
  orgUnitPath?: string | null;
  description?: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface BenchmarkComparison {
  metric: MetricType;
  actualValue?: number | null;
  targetValue: number;
  benchmarkName: string;
  delta?: number | null;
  isAboveTarget?: boolean | null;
}

export interface SaveBenchmarkRequest {
  name: string;
  metric: MetricType;
  targetValue: number;
  isCompanyWide: boolean;
  orgUnitId?: string | null;
  description?: string | null;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

function buildFilterParams(filter: AnalyticsFilter | undefined): Record<string, string> {
  const params: Record<string, string> = {};

  if (!filter) return params;

  if (filter.from) params.from = filter.from;
  if (filter.to) params.to = filter.to;
  if (filter.campaignId) params.campaignId = filter.campaignId;
  if (filter.orgUnitId) params.orgUnitId = filter.orgUnitId;
  if (filter.includeDescendants !== undefined) {
    params.includeDescendants = String(filter.includeDescendants);
  }

  return params;
}

export const analyticsApi = {
  dashboard: (culture: Culture, filter: AnalyticsFilter | undefined, signal?: AbortSignal) =>
    apiRequest<AnalyticsDashboard>(
      culture,
      appendSearchParams('/analytics/dashboard', buildFilterParams(filter)),
      { signal }
    ),

  getSurveyAnalytics: (culture: Culture, surveyId: string, filter: AnalyticsFilter | undefined, signal?: AbortSignal) =>
    apiRequest<SurveyAnalytics>(
      culture,
      appendSearchParams(`/analytics/surveys/${surveyId}`, buildFilterParams(filter)),
      { signal }
    ),

  compute: (culture: Culture, surveyId: string, request?: Partial<ComputeAnalyticsRequest>) =>
    apiPost<SurveyAnalytics>(culture, `/analytics/surveys/${surveyId}/compute`, request ?? {}),

  trend: (culture: Culture, request: TrendRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};

    if (request.surveyId) params.surveyId = request.surveyId;
    if (request.period !== undefined && request.period !== null) params.period = String(request.period);
    if (request.from) params.from = request.from;
    if (request.to) params.to = request.to;

    return apiRequest<TrendPoint[]>(culture, appendSearchParams('/analytics/trend', params), { signal });
  },

  searchMetrics: (culture: Culture, request: AnalyticsSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};

    if (request.searchText) params.searchText = request.searchText;
    if (request.surveyId) params.surveyId = request.surveyId;
    if (request.segmentType !== undefined && request.segmentType !== null) {
      params.segmentType = String(request.segmentType);
    }
    if (request.includeArchived) params.includeArchived = 'true';
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<SurveyMetricSummary>>(
      culture,
      appendSearchParams('/analytics/metrics', params),
      { signal }
    );
  },

  segmentsByOrgUnit: (
    culture: Culture,
    surveyId: string,
    filter: AnalyticsFilter | undefined,
    signal?: AbortSignal
  ) =>
    apiRequest<AnalyticsSegmentResult[]>(
      culture,
      appendSearchParams(`/analytics/surveys/${surveyId}/segments/org-units`, buildFilterParams(filter)),
      { signal }
    ),

  compareWithBenchmarks: (culture: Culture, surveyId: string, signal?: AbortSignal) =>
    apiRequest<BenchmarkComparison[]>(
      culture,
      `/analytics/surveys/${surveyId}/benchmarks`,
      { signal }
    )
};

export const benchmarksApi = {
  search: (culture: Culture, searchText: string | null, includeInactive: boolean, signal?: AbortSignal) =>
    apiRequest<PagedResult<Benchmark>>(
      culture,
      appendSearchParams('/benchmarks', {
        searchText: searchText ?? '',
        includeInactive: String(includeInactive)
      }),
      { signal }
    ),

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<Benchmark>(culture, `/benchmarks/${id}`, { signal }),

  create: (culture: Culture, request: SaveBenchmarkRequest) =>
    apiPost<Benchmark>(culture, '/benchmarks', request),

  update: (culture: Culture, id: string, request: SaveBenchmarkRequest) =>
    apiPut<Benchmark>(culture, `/benchmarks/${id}`, request),

  delete: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/benchmarks/${id}`)
};
