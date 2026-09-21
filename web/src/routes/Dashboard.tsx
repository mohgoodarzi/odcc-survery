import { useLanguage } from '@/i18n/LanguageProvider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { AppLayout } from '@/layouts/AppLayout';

/**
 * داشبورد — در فاز ۱ با داده‌های واقعی پر می‌شود.
 */
export function Dashboard() {
  const { t } = useLanguage();

  const stats = [
    { label: t.nav.surveys, value: 12 },
    { label: t.nav.campaigns, value: 4 },
    { label: 'پاسخ‌ها', value: 1280 },
    { label: 'NPS میانگین', value: 42 }
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
              <CardTitle className="text-3xl">{stat.value}</CardTitle>
            </CardHeader>
            <CardContent />
          </Card>
        ))}
      </div>
    </AppLayout>
  );
}
