import { Link } from 'react-router-dom';

import { useLanguage } from '@/i18n/LanguageProvider';
import { formatYear } from '@/i18n/format';
import { Button } from '@/components/ui/button';

export function NotFound() {
  const { t, culture } = useLanguage();

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-6 text-center">
      <h1 className="text-6xl font-bold text-primary">{formatYear(404, culture)}</h1>
      <p className="text-xl font-semibold">{t.common.notFound}</p>
      <p className="text-sm text-muted-foreground">{t.common.notFoundDescription}</p>
      <Button asChild>
        <Link to={`/${culture}/dashboard`}>{t.common.goHome}</Link>
      </Button>
    </div>
  );
}
