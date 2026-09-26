import { useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowRight, RotateCw } from 'lucide-react';

import { ApiError } from '@/api/client';
import { MetricType, QuestionType } from '@/api/analytics';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  useAnalyticsSegments,
  useBenchmarkComparisons,
  useComputeAnalytics,
  useMetricLabel,
  useSurveyAnalytics
} from '@/api/analyticsHooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { formatCount, formatNumber } from '@/i18n/format';
import { MetricCard } from './MetricCard';

/**
 * صفحه‌ی تحلیلات یک نظرسنجی: شاخص‌های NPS/CSAT/CES، تحلیل تک‌تک سؤال‌ها،
 * بخش‌بندی سازمانی و مقایسه با بنچمارک‌ها.
 */
export function SurveyAnalyticsPage() {
  const { surveyId } = useParams<{ surveyId: string }>();
  const navigate = useNavigate();
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();
  const metricLabel = useMetricLabel();

  const filter = useMemo(() => ({}), []);

  const { data, isLoading, isError, error, refetch } = useSurveyAnalytics(surveyId ?? null, filter);
  const computeMutation = useComputeAnalytics();
  const segmentsQuery = useAnalyticsSegments(
    surveyId ?? null,
    filter,
    hasPermission(Permissions.Analytics.DepartmentView) && data?.canSegmentByOrgUnit === true
  );
  const comparisonsQuery = useBenchmarkComparisons(surveyId ?? null);

  const questions = data?.questions ?? [];
  const segments = segmentsQuery.data ?? [];
  const comparisons = comparisonsQuery.data ?? [];

  return (
    <AppLayout>
      <PageHeader
        title={t.analytics.surveyAnalytics}
        description={t.analytics.surveyAnalyticsDescription}
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => navigate(`/${culture}/analytics`)}
            >
              <ArrowRight className="size-4" />
              {t.analytics.title}
            </Button>
            <Button
              variant="default"
              size="sm"
              disabled={computeMutation.isPending}
              onClick={() => surveyId && computeMutation.mutate({ surveyId })}
            >
              <RotateCw className={`size-4 ${computeMutation.isPending ? 'animate-spin' : ''}`} />
              {computeMutation.isPending ? t.analytics.recomputing : t.analytics.recompute}
            </Button>
          </div>
        }
      />

      {isError ? (
        <ErrorState message={(error as ApiError)?.problem.detail} onRetry={() => void refetch()} />
      ) : isLoading ? (
        <TableLoading columns={4} />
      ) : data ? (
        <div className="flex flex-col gap-6">
          {/* سرتیتر نظرسنجی */}
          <div className="flex flex-wrap items-center gap-3">
            <div className="flex min-w-0 flex-col gap-1">
              <h2 className="text-lg font-semibold">{data.surveyTitle}</h2>
              <span className="font-mono text-xs text-muted-foreground" dir="ltr">
                {data.surveyCode}
              </span>
            </div>
            {data.isAnonymous && (
              <Badge variant="outline">{t.analytics.anonymousSurvey}</Badge>
            )}
          </div>

          {/* شاخص‌های کلیدی */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <MetricCard
              label={t.analytics.completedSessions}
              value={data.completedSessions}
              hint={`${t.analytics.totalSessions}: ${formatCount(data.totalSessions, culture)}`}
            />
            <MetricCard
              label={t.analytics.completionRate}
              value={data.completionRate}
              asPercentage
            />
            <MetricCard
              label={t.analytics.metricNps}
              value={data.npsScore}
              asPercentage
              hint={
                data.npsScore !== null && data.npsScore !== undefined
                  ? `${t.analytics.promoterPassiveDetractor}: ${data.npsPromoters} / ${data.npsPassives} / ${data.npsDetractors}`
                  : undefined
              }
            />
            <MetricCard
              label={t.analytics.metricAverageRating}
              value={data.averageRating}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <MetricCard label={t.analytics.metricCsat} value={data.csatScore} asPercentage />
            <MetricCard label={t.analytics.metricCes} value={data.cesScore} asPercentage />
            <MetricCard
              label={t.analytics.computedAt}
              value={null}
              emptyLabel={new Date(data.computedAt).toLocaleDateString(CULTURE_LOCALE(culture))}
            />
          </div>

          {/* مقایسه با بنچمارک */}
          {comparisons.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>{t.analytics.benchmarkComparisons}</CardTitle>
                <CardDescription>{t.analytics.benchmarkComparisonsDescription}</CardDescription>
              </CardHeader>
              <CardContent>
                <ul className="flex flex-col divide-y">
                  {comparisons.map((comparison, index) => (
                    <li key={`${comparison.metric}-${index}`} className="flex items-center justify-between gap-4 py-3">
                      <div className="flex min-w-0 flex-col gap-1">
                        <span className="text-sm font-medium">{metricLabel(comparison.metric)}</span>
                        <span className="text-xs text-muted-foreground">
                          {comparison.benchmarkName || t.analytics.noTarget}
                        </span>
                      </div>
                      <div className="flex shrink-0 items-center gap-3 text-sm">
                        <span className="text-muted-foreground" dir="ltr">
                          {comparison.actualValue !== null && comparison.actualValue !== undefined
                            ? formatNumber(comparison.actualValue, culture)
                            : '—'}
                        </span>
                        <span dir="ltr">
                          {comparison.isAboveTarget === null || comparison.isAboveTarget === undefined
                            ? t.analytics.noTarget
                            : comparison.isAboveTarget
                              ? t.analytics.aboveTarget
                              : comparison.delta === 0
                                ? t.analytics.onTarget
                                : t.analytics.belowTarget}
                        </span>
                      </div>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          )}

          {/* بخش‌بندی سازمانی */}
          {data.canSegmentByOrgUnit && (
            <Card>
              <CardHeader>
                <CardTitle>{t.analytics.segmentByOrgUnit}</CardTitle>
                <CardDescription>{t.analytics.segmentByOrgUnitDescription}</CardDescription>
              </CardHeader>
              <CardContent>
                {segmentsQuery.isLoading ? (
                  <TableLoading columns={4} />
                ) : segments.length === 0 ? (
                  <EmptyState title={t.analytics.noSegments} description={t.analytics.noSegmentsDescription} />
                ) : (
                  <ul className="flex flex-col divide-y">
                    {segments.map((segment) => (
                      <li key={segment.label} className="flex items-center justify-between gap-4 py-3">
                        <span className="font-mono text-sm" dir="ltr">{segment.label}</span>
                        <div className="flex shrink-0 items-center gap-4 text-sm text-muted-foreground">
                          <span dir="ltr">{formatCount(segment.completedSessions, culture)}</span>
                          <span dir="ltr">{formatNumber(segment.completionRate, culture)}٪</span>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          )}

          {/* تحلیل سؤال‌ها */}
          <Card>
            <CardHeader>
              <CardTitle>{t.analytics.questions}</CardTitle>
              <CardDescription>{t.analytics.responseRate}</CardDescription>
            </CardHeader>
            <CardContent>
              {questions.length === 0 ? (
                <EmptyState title={t.analytics.noData} description={t.analytics.noDataDescription} />
              ) : (
                <div className="flex flex-col gap-6">
                  {questions.map((question) => (
                    <QuestionAnalysis key={question.questionId} question={question} />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      ) : null}
    </AppLayout>
  );
}

function CULTURE_LOCALE(culture: string): string {
  return culture === 'fa' ? 'fa-IR' : 'en-US';
}

interface QuestionAnalysisProps {
  question: import('@/api/analytics').QuestionMetric;
}

/**
 * تحلیل یک سؤال: توزیع گزینه‌ها، آمار عددی، توزیع بله/خیر یا تحلیل متن.
 */
function QuestionAnalysis({ question }: QuestionAnalysisProps) {
  const { t, culture } = useLanguage();
  const metricLabel = useMetricLabel();

  return (
    <div className="flex flex-col gap-3 border-b pb-6 last:border-b-0 last:pb-0">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex min-w-0 flex-col gap-1">
          <span className="text-sm font-medium">
            <span className="font-mono text-xs text-muted-foreground" dir="ltr">
              {question.questionCode}
            </span>
            {' — '}
            {question.questionText}
          </span>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {question.detectedMetric !== null && question.detectedMetric !== undefined && (
            <Badge variant="default">{metricLabel(question.detectedMetric as MetricType)}</Badge>
          )}
          <Badge variant="outline" dir="ltr">
            {formatCount(question.responseCount, culture)} · {formatNumber(question.responseRate, culture)}٪
          </Badge>
        </div>
      </div>

      {question.questionType === QuestionType.SingleChoice ||
      question.questionType === QuestionType.MultipleChoice ? (
        <div className="flex flex-col gap-2">
          {question.options.map((option) => (
            <div key={option.optionId} className="flex items-center gap-3">
              <span className="w-32 shrink-0 truncate text-xs text-muted-foreground">
                {option.optionText}
              </span>
              <div className="h-2.5 flex-1 overflow-hidden rounded bg-muted" dir="ltr">
                <div
                  className="h-full rounded bg-primary/70"
                  style={{ width: `${Math.min(option.percentage, 100)}%` }}
                />
              </div>
              <span className="w-20 shrink-0 text-end text-xs text-muted-foreground" dir="ltr">
                {formatCount(option.count, culture)} · {formatNumber(option.percentage, culture)}٪
              </span>
            </div>
          ))}
        </div>
      ) : null}

      {question.numericStats ? (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          <StatChip label={t.analytics.mean} value={question.numericStats.mean} />
          <StatChip label={t.analytics.median} value={question.numericStats.median} />
          <StatChip label={t.analytics.min} value={question.numericStats.min} />
          <StatChip label={t.analytics.max} value={question.numericStats.max} />
        </div>
      ) : null}

      {question.numericStats && question.numericStats.ratingBuckets.length > 0 ? (
        <div className="flex items-end gap-2" dir="ltr">
          {question.numericStats.ratingBuckets.map((bucket) => (
            <div
              key={String(bucket.value)}
              className="flex flex-1 flex-col items-center gap-1"
              title={`${bucket.value}: ${formatCount(bucket.count, culture)}`}
            >
              <div
                className="w-full rounded-t bg-primary/60"
                style={{ height: `${Math.max((bucket.count / Math.max(...question.numericStats!.ratingBuckets.map((b) => b.count), 1)) * 40, 4)}px` }}
              />
              <span className="text-[10px] text-muted-foreground">{formatNumber(bucket.value, culture)}</span>
            </div>
          ))}
        </div>
      ) : null}

      {question.yesNoDistribution ? (
        <div className="flex gap-4 text-sm">
          <span className="text-muted-foreground">
            {t.analytics.yes}: <span dir="ltr">{formatCount(question.yesNoDistribution.yesCount, culture)}</span>{' '}
            ({formatNumber(question.yesNoDistribution.yesPercentage, culture)}٪)
          </span>
          <span className="text-muted-foreground">
            {t.analytics.no}: <span dir="ltr">{formatCount(question.yesNoDistribution.noCount, culture)}</span>{' '}
            ({formatNumber(question.yesNoDistribution.noPercentage, culture)}٪)
          </span>
        </div>
      ) : null}

      {question.textAnalytics && question.textAnalytics.textResponseCount > 0 ? (
        <div className="flex flex-col gap-2 rounded-md border bg-muted/30 p-3">
          <span className="text-xs font-medium">{t.analytics.textAnalytics}</span>
          <div className="flex flex-wrap gap-3 text-xs text-muted-foreground">
            <span>{t.analytics.positive}: {formatNumber(question.textAnalytics.sentiment.positivePercentage, culture)}٪</span>
            <span>{t.analytics.neutral}: {formatNumber(question.textAnalytics.sentiment.neutralPercentage, culture)}٪</span>
            <span>{t.analytics.negative}: {formatNumber(question.textAnalytics.sentiment.negativePercentage, culture)}٪</span>
          </div>
          {question.textAnalytics.themes.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {question.textAnalytics.themes.slice(0, 8).map((theme) => (
                <Badge key={theme.theme} variant="outline">
                  {theme.theme} · {formatCount(theme.occurrenceCount, culture)}
                </Badge>
              ))}
            </div>
          )}
        </div>
      ) : null}
    </div>
  );
}

interface StatChipProps {
  label: string;
  value: number | null | undefined;
}

function StatChip({ label, value }: StatChipProps) {
  const { culture, t } = useLanguage();

  const hasValue = value !== null && value !== undefined && !Number.isNaN(value);

  return (
    <div className="flex flex-col gap-0.5 rounded-md border bg-muted/30 px-3 py-2">
      <span className="text-[10px] text-muted-foreground">{label}</span>
      <span className="text-sm font-medium" dir="ltr">
        {hasValue ? formatNumber(value as number, culture) : t.analytics.noData}
      </span>
    </div>
  );
}
