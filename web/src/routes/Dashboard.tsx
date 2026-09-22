import { useLanguage } from '@/i18n/LanguageProvider';
import { formatCount } from '@/i18n/format';
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { AppLayout } from '@/layouts/AppLayout';

/**
 * داشبورد — در فاز ۱ با داده‌های واقعی پر می‌شود.
 */
export function Dashboard() {
  const { t, culture } = useLanguage();

  const stats = [
    { label: t.nav.surveys, value: 12 },
    { label: t.nav.campaigns, value: 4 },
    { label: t.dashboard.responses, value: 1280 },
    { label: t.dashboard.averageNps, value: 42 }
  ];

  return (
    <AppLayout>
      <div className="mb-6">
        <h1 className="text-2xl font-bold">{t.nav.dashboard}</h1>
        <p className="text-sm text-muted-foreground">{t.app.tagline}</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat) => (
          <Card key={stat.label}>
            <CardHeader>
              <CardDescription>{stat.label}</CardDescription>
              <CardTitle className="text-3xl">{formatCount(stat.value, culture)}</CardTitle>
            </CardHeader>
          </Card>
        ))}
      </div>
    </AppLayout>
  );
}
