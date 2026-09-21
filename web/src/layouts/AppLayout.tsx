import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard, FileText, Megaphone, BarChart3, FileBarChart, Settings, Languages
} from 'lucide-react';

import { useLanguage } from '@/i18n/LanguageProvider';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

/**
 * پوسته‌ی برنامه: نوار کناری، سرستار با جستجو و اقدامات سریع.
 * ناوبری بر اساس نقش در فاز ۱ با تکمیل احراز هویت فیلتر می‌شود.
 */
export function AppLayout({ children }: { children: React.ReactNode }) {
  const { t, culture, toggleCulture } = useLanguage();

  const navItems = [
    { to: `/${culture}/dashboard`, label: t.nav.dashboard, icon: LayoutDashboard },
    { to: `/${culture}/surveys`, label: t.nav.surveys, icon: FileText },
    { to: `/${culture}/campaigns`, label: t.nav.campaigns, icon: Megaphone },
    { to: `/${culture}/analytics`, label: t.nav.analytics, icon: BarChart3 },
    { to: `/${culture}/reports`, label: t.nav.reports, icon: FileBarChart },
    { to: `/${culture}/settings`, label: t.nav.settings, icon: Settings }
  ];

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside className="flex w-64 shrink-0 flex-col border-e bg-card">
        <div className="flex h-16 items-center gap-2 border-b px-6">
          <div className="size-8 rounded-lg bg-primary" aria-hidden />
          <span className="font-semibold">{t.app.name}</span>
        </div>

        <nav className="flex flex-1 flex-col gap-1 p-4">
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-accent text-accent-foreground'
                    : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground'
                )
              }
            >
              <Icon className="size-4" />
              {label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex flex-1 flex-col">
        <header className="flex h-16 items-center justify-between gap-4 border-b px-6">
          <div className="relative flex-1 max-w-md">
            <input
              type="search"
              placeholder={t.common.search}
              className="h-9 w-full rounded-md border bg-background px-3 text-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              aria-label={t.common.search}
            />
          </div>

          <div className="flex items-center gap-2">
            <Button variant="ghost" size="icon" onClick={toggleCulture} aria-label={t.common.language}>
              <Languages className="size-4" />
            </Button>
            <Button variant="ghost" size="icon" aria-label={t.common.notifications}>
              <Megaphone className="size-4" />
            </Button>
            <Button variant="ghost" size="icon" aria-label={t.common.profile}>
              <div className="size-7 rounded-full bg-muted" aria-hidden />
            </Button>
          </div>
        </header>

        <main className="flex-1 p-6">{children}</main>
      </div>
    </div>
  );
}
