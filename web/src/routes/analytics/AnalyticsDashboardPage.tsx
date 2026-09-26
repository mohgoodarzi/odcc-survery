import { useMemo, useState } from 'react';

import { ApiError } from '@/api/client';
import { AnalyticsPeriod } from '@/api/analytics';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useAnalyticsDashboard, useAnalyticsTrend } from '@/api/analyticsHooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { EmptyState, ErrorState, TableLoading } from '@/components/ui/states';
import { formatCount, formatNumber } from '@/i18n/format';
import { MetricCard } from './MetricCard';

/**
 * داشبورد تحلیلات سطح شرکت: شاخص‌های کلیدی، روند ارسال پاسخ و پربازدیدترین
 * نظرسنجی‌ها. دسترسی نیازمند مجوز مشاهده‌ی تحلیلات است؛ مرز امنیتی واقعی
 * سمت سرور است.
 */
export function AnalyticsDashboardPage() {
  const { t, culture } = useLanguage();

  const [period, setPeriod] = useState<AnalyticsPeriod>(AnalyticsPeriod.Day);

  const filter = useMemo(() => ({}), []);

  const { data, isLoading, isError, error, refetch } = useAnalyticsDashboard(filter);
  const trendQuery = useAnalyticsTrend({ period });

  const trend = trendQuery.data ?? [];
  const topSurveys = data?.topSurveys ?? [];
  const maxCount = trend.length > 0 ? Math.max(...trend.map((point) => point.count), 1) : 1;

  return (
    <AppLayout>
      <PageHeader title={t.analytics.title} description={t.analytics.description} />

      {isError ? (
        <ErrorState message={(error as ApiError)?.problem.detail} onRetry={() => void refetch()} />
      ) : isLoading ? (
        <TableLoading columns={4} />
      ) : (
        <div className="flex flex-col gap-6">
          {/* شاخص‌های کلیدی */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <MetricCard
              label={t.analytics.totalSurveys}
              value={data?.totalSurveys ?? 0}
              hint={`${t.analytics.activeSurveys}: ${formatCount(data?.activeSurveys ?? 0, culture)}`}
            />
            <MetricCard
              label={t.analytics.totalCampaigns}
              value={data?.totalCampaigns ?? 0}
              hint={`${t.analytics.activeCampaigns}: ${formatCount(data?.activeCampaigns ?? 0, culture)}`}
            />
            <MetricCard
              label={t.analytics.completedSessions}
              value={data?.completedSessions ?? 0}
              hint={`${t.analytics.completionRate}: ${formatNumber(data?.completionRate ?? 0, culture)}٪`}
            />
            <MetricCard
              label={t.analytics.averageNps}
              value={data?.averageNps}
              asPercentage
              emptyLabel={t.analytics.noData}
            />
          </div>

          {/* شاخص‌های رضایت ثانویه */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <MetricCard label={t.analytics.averageCsat} value={data?.averageCsat} asPercentage />
            <MetricCard label={t.analytics.averageCes} value={data?.averageCes} asPercentage />
            <MetricCard label={t.analytics.metricAverageRating} value={data?.averageRating} />
          </div>

          {/* روند ارسال پاسخ‌ها */}
          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <CardTitle>{t.analytics.submissionTrend}</CardTitle>
                  <CardDescription>{t.analytics.submissions}</CardDescription>
                </div>
                <Select
                  value={String(period)}
                  onChange={(event) => setPeriod(Number(event.target.value) as AnalyticsPeriod)}
                  options={[
                    { value: String(AnalyticsPeriod.Day), label: t.analytics.periodDay },
                    { value: String(AnalyticsPeriod.Week), label: t.analytics.periodWeek },
                    { value: String(AnalyticsPeriod.Month), label: t.analytics.periodMonth }
                  ]}
                  aria-label={t.analytics.submissionTrend}
                />
              </div>
            </CardHeader>
            <CardContent>
              {trendQuery.isLoading ? (
                <TableLoading columns={6} />
              ) : trend.length === 0 ? (
                <EmptyState title={t.analytics.noData} description={t.analytics.noDataDescription} />
              ) : (
                <div className="flex flex-col gap-3">
                  <div className="flex h-48 items-end gap-2" dir="ltr">
                    {trend.map((point) => {
                      const height = Math.max((point.count / maxCount) * 100, 4);

                      return (
                        <div
                          key={point.periodLabel}
                          className="group relative flex flex-1 flex-col items-center justify-end"
                          title={`${point.periodLabel}: ${formatCount(point.count, culture)}`}
                        >
                          <div
                            className="w-full rounded-t bg-primary/70 transition-all group-hover:bg-primary"
                            style={{ height: `${height}%` }}
                          />
                          <span className="mt-1 hidden text-[10px] text-muted-foreground sm:inline">
                            {point.periodLabel.slice(5)}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                  <div className="flex items-center justify-between text-xs text-muted-foreground">
                    <span>
                      {t.analytics.cumulative}: {formatCount(trend[trend.length - 1]?.cumulativeCount ?? 0, culture)}
                    </span>
                    <span>{t.analytics.submissions}: {formatCount(trend.reduce((sum, p) => sum + p.count, 0), culture)}</span>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          {/* پربازدیدترین نظرسنجی‌ها */}
          <Card>
            <CardHeader>
              <CardTitle>{t.analytics.topSurveys}</CardTitle>
              <CardDescription>{t.analytics.completedSessions}</CardDescription>
            </CardHeader>
            <CardContent>
              {topSurveys.length === 0 ? (
                <EmptyState title={t.analytics.noData} description={t.analytics.noDataDescription} />
              ) : (
                <ul className="flex flex-col divide-y">
                  {topSurveys.map((survey) => (
                    <li key={survey.id} className="flex items-center justify-between gap-4 py-3">
                      <div className="flex min-w-0 flex-col gap-1">
                        <span className="truncate text-sm font-medium">{survey.surveyTitle}</span>
                        <span className="font-mono text-xs text-muted-foreground" dir="ltr">
                          {survey.surveyCode}
                          {survey.isAnonymous ? ` · ${t.analytics.anonymousSurvey}` : ''}
                        </span>
                      </div>
                      <div className="flex shrink-0 items-center gap-4 text-sm">
                        <span className="text-muted-foreground" dir="ltr">
                          {formatCount(survey.completedSessions, culture)}
                        </span>
                        {survey.npsScore !== null && survey.npsScore !== undefined && (
                          <span className="font-medium" dir="ltr">
                            NPS {formatNumber(survey.npsScore, culture)}
                          </span>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      )}
    </AppLayout>
  );
}
