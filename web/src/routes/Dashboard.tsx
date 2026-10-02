import { useLanguage } from '@/i18n/LanguageProvider';
import { formatCount } from '@/i18n/format';
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Logo } from '@/components/ui/logo';
import { AppLayout } from '@/layouts/AppLayout';
import { Permissions } from '@/auth/permissions';
import { useAuth } from '@/auth/AuthProvider';
import { useSurveysSearch } from '@/api/hooks';
import { useAnalyticsDashboard } from '@/api/analyticsHooks';

interface StatCardProps {
  label: string;
  value: string;
  loading: boolean;
}

function StatCard({ label, value, loading }: StatCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardDescription>{label}</CardDescription>
        {loading ? (
          <div className="h-8 w-16 animate-pulse rounded bg-muted" aria-hidden />
        ) : (
          <CardTitle className="text-3xl">{value}</CardTitle>
        )}
      </CardHeader>
    </Card>
  );
}

/**
 * داشبورد — شاخص‌های کلیدی سامانه.
 *
 * همه‌ی اعداد از همان منابع داده‌ای خوانده می‌شوند که سایر صفحات استفاده
 * می‌کنند، تا هرگز مغایرتی پیش نیاید:
 *
 * - شمارش نظرسنجی‌ها دقیقاً از همان کوئری صفحه‌ی «نظرسنجی‌ها» با فیلتر «همه»
 *   (بدون وضعیت، شامل بایگانی‌ها نمی‌شود) گرفته می‌شود. چون کلید کوئری هم
 *   یکسان است، react-query بین داشبورد و آن صفحه کش را به اشتراک می‌گذارد.
 * - سایر شاخص‌ها از اندپوینت «داشبورد تحلیلات» خوانده می‌شوند که منبع واحد
 *   سمت سرور برای این اعداد است.
 *
 * مجوزها: مرز امنیتی واقعی همواره سمت سرور است (HasPermission روی اندپوینت).
 * این‌جا کوئری‌ها صرفاً برای جلوگیری از خطای ۴۰۳ روی کاربرانی که مجوز مشاهده‌ی
 * بخش مربوطه را ندارند غیرفعال می‌شوند و به‌جای عدد، «—» نمایش داده می‌شود.
 */
export function Dashboard() {
  const { t, culture } = useLanguage();
  const { hasPermission } = useAuth();

  const canViewSurveys = hasPermission(Permissions.Survey.View);
  const canViewAnalytics = hasPermission(Permissions.Analytics.View);

  const surveysQuery = useSurveysSearch(
    { searchText: null, status: null, questionnaireId: null, includeArchived: false, page: 1 },
    canViewSurveys
  );

  const analyticsQuery = useAnalyticsDashboard(undefined, canViewAnalytics);

  const totalSurveys = surveysQuery.data?.totalCount ?? 0;
  const totalCampaigns = analyticsQuery.data?.totalCampaigns ?? 0;
  const completedResponses = analyticsQuery.data?.completedSessions ?? 0;
  const averageNps = analyticsQuery.data?.averageNps ?? null;

  const stats = [
    {
      label: t.nav.surveys,
      loading: canViewSurveys && surveysQuery.isLoading,
      value: canViewSurveys ? formatCount(totalSurveys, culture) : '—'
    },
    {
      label: t.nav.campaigns,
      loading: canViewAnalytics && analyticsQuery.isLoading,
      value: canViewAnalytics ? formatCount(totalCampaigns, culture) : '—'
    },
    {
      label: t.dashboard.responses,
      loading: canViewAnalytics && analyticsQuery.isLoading,
      value: canViewAnalytics ? formatCount(completedResponses, culture) : '—'
    },
    {
      label: t.dashboard.averageNps,
      loading: canViewAnalytics && analyticsQuery.isLoading,
      value: canViewAnalytics && averageNps !== null ? formatCount(averageNps, culture) : '—'
    }
  ];

  return (
    <AppLayout>
      <div className="mb-6 flex items-center gap-3">
        <Logo className="size-10" />
        <div>
          <h1 className="text-2xl font-bold">{t.nav.dashboard}</h1>
          <p className="text-sm text-muted-foreground">{t.app.tagline}</p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat) => (
          <StatCard key={stat.label} label={stat.label} value={stat.value} loading={stat.loading} />
        ))}
      </div>
    </AppLayout>
  );
}
