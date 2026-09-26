import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  analyticsApi,
  benchmarksApi,
  MetricType,
  type AnalyticsFilter,
  type AnalyticsSegment,
  type ComputeAnalyticsRequest,
  type SaveBenchmarkRequest,
  type TrendRequest
} from '@/api/analytics';
import { useLanguage } from '@/i18n/LanguageProvider';

/**
 * کلیدهای کوئری تحلیلات تا invalidation بین صفحات یکپارچه باشد.
 */
export const analyticsQueryKeys = {
  dashboard: (filter: AnalyticsFilter | undefined) =>
    ['analytics', 'dashboard', filter ?? {}] as const,
  surveyAnalytics: (surveyId: string | null, filter: AnalyticsFilter | undefined) =>
    ['analytics', 'survey', surveyId, filter ?? {}] as const,
  trend: (request: TrendRequest) => ['analytics', 'trend', request] as const,
  metricsSearch: (
    searchText: string | null,
    surveyId: string | null,
    segmentType: AnalyticsSegment | null,
    page: number
  ) => ['analytics', 'metrics', searchText, surveyId, segmentType, page] as const,
  segments: (surveyId: string | null, filter: AnalyticsFilter | undefined) =>
    ['analytics', 'segments', surveyId, filter ?? {}] as const,
  benchmarkComparisons: (surveyId: string | null) =>
    ['analytics', 'benchmark-comparisons', surveyId] as const,
  benchmarks: (searchText: string | null, includeInactive: boolean) =>
    ['benchmarks', searchText, includeInactive] as const,
  benchmark: (id: string | null) => ['benchmarks', 'detail', id] as const
};

function useInvalidateAnalytics() {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: ['analytics'] });
  };
}

export function useAnalyticsDashboard(filter: AnalyticsFilter | undefined) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: analyticsQueryKeys.dashboard(filter),
    queryFn: ({ signal }) => analyticsApi.dashboard(culture, filter, signal)
  });
}

export function useSurveyAnalytics(
  surveyId: string | null,
  filter: AnalyticsFilter | undefined,
  enabled = true
) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: analyticsQueryKeys.surveyAnalytics(surveyId, filter),
    queryFn: ({ signal }) =>
      analyticsApi.getSurveyAnalytics(culture, surveyId as string, filter, signal),
    enabled: !!surveyId && enabled
  });
}

export function useComputeAnalytics() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateAnalytics();

  return useMutation({
    mutationFn: ({ surveyId, request }: { surveyId: string; request?: Partial<ComputeAnalyticsRequest> }) =>
      analyticsApi.compute(culture, surveyId, request),
    onSuccess: () => invalidate()
  });
}

export function useAnalyticsTrend(request: TrendRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: analyticsQueryKeys.trend(request),
    queryFn: ({ signal }) => analyticsApi.trend(culture, request, signal)
  });
}

export function useAnalyticsMetricsSearch(params: {
  searchText: string | null;
  surveyId: string | null;
  segmentType: AnalyticsSegment | null;
  page: number;
}) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: analyticsQueryKeys.metricsSearch(
      params.searchText,
      params.surveyId,
      params.segmentType,
      params.page
    ),
    queryFn: ({ signal }) =>
      analyticsApi.searchMetrics(
        culture,
        {
          searchText: params.searchText,
          surveyId: params.surveyId,
          segmentType: params.segmentType,
          page: params.page,
          pageSize: 20
        },
        signal
      )
  });
}

export function useAnalyticsSegments(
  surveyId: string | null,
  filter: AnalyticsFilter | undefined,
  enabled = true
) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: analyticsQueryKeys.segments(surveyId, filter),
    queryFn: ({ signal }) =>
      analyticsApi.segmentsByOrgUnit(culture, surveyId as string, filter, signal),
    enabled: !!surveyId && enabled
  });
}

export function useBenchmarkComparisons(surveyId: string | null, enabled = true) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: analyticsQueryKeys.benchmarkComparisons(surveyId),
    queryFn: ({ signal }) => analyticsApi.compareWithBenchmarks(culture, surveyId as string, signal),
    enabled: !!surveyId && enabled
  });
}

export function useBenchmarks(searchText: string | null, includeInactive = false) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: analyticsQueryKeys.benchmarks(searchText, includeInactive),
    queryFn: ({ signal }) =>
      benchmarksApi.search(culture, searchText, includeInactive, signal)
  });
}

export function useCreateBenchmark() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: SaveBenchmarkRequest) => benchmarksApi.create(culture, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['benchmarks'] });
    }
  });
}

export function useUpdateBenchmark() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveBenchmarkRequest }) =>
      benchmarksApi.update(culture, id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['benchmarks'] });
    }
  });
}

export function useDeleteBenchmark() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: benchmarksApi.delete.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['benchmarks'] });
    }
  });
}

/**
 * برچسب نمایشی یک شاخص.
 */
export function useMetricLabel() {
  const { t } = useLanguage();

  return (metric: MetricType): string => {
    switch (metric) {
      case MetricType.Nps: return t.analytics.metricNps;
      case MetricType.Csat: return t.analytics.metricCsat;
      case MetricType.Ces: return t.analytics.metricCes;
      case MetricType.CompletionRate: return t.analytics.metricCompletionRate;
      case MetricType.ResponseRate: return t.analytics.metricResponseRate;
      default: return t.analytics.metricAverageRating;
    }
  };
}
