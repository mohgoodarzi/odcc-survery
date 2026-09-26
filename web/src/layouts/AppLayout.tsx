import { useRef, useState, type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, FileText, Megaphone, BarChart3, FileBarChart, Settings, Languages,
  Users, ShieldCheck, Building2, Briefcase, UserCircle, LogOut, ChevronDown,
  LayoutTemplate, ClipboardCheck, MessageSquareText
} from 'lucide-react';

import { useAuth } from '@/auth/AuthProvider';
import { Permissions } from '@/auth/permissions';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useClickOutside } from '@/lib/use-click-outside';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

interface NavItem {
  to: string;
  label: string;
  icon: typeof LayoutDashboard;
  /** مجوزهای لازم برای دیدن این آیتم (حداقل یکی). */
  permissions?: string[];
}

/**
 * پوسته‌ی برنامه: نوار کناری، سرستار با جستجو و اقدامات سریع.
 *
 * ناوبری بر اساس مجوزهای کاربر جاری فیلتر می‌شود. هر آیتمی که کاربر
 * مجوز دیدنش را نداشته باشد نمایش داده نمی‌شود. مرز امنیتی واقعی در سمت
 * سرور است؛ این فقط تجربه‌ی کاربری را تمیزتر می‌کند.
 */
export function AppLayout({ children }: { children: ReactNode }) {
  const { t, culture, toggleCulture } = useLanguage();
  const { user, logout, hasAnyPermission } = useAuth();
  const navigate = useNavigate();

  const navItems: NavItem[] = [
    { to: `/${culture}/dashboard`, label: t.nav.dashboard, icon: LayoutDashboard },
    {
      to: `/${culture}/surveys`,
      label: t.nav.surveys,
      icon: FileText,
      permissions: [Permissions.Survey.View]
    },
    {
      to: `/${culture}/survey-templates`,
      label: t.surveys.templatesTitle,
      icon: LayoutTemplate,
      permissions: [Permissions.Survey.View]
    },
    { to: `/${culture}/campaigns`, label: t.nav.campaigns, icon: Megaphone, permissions: [Permissions.Campaign.View] },
    { to: `/${culture}/my-surveys`, label: t.nav.mySurveys, icon: ClipboardCheck },
    {
      to: `/${culture}/responses`,
      label: t.nav.responses,
      icon: MessageSquareText,
      permissions: [Permissions.Response.View]
    },
    {
      to: `/${culture}/analytics`,
      label: t.nav.analytics,
      icon: BarChart3,
      permissions: [Permissions.Analytics.View]
    },
    { to: `/${culture}/reports`, label: t.nav.reports, icon: FileBarChart },
    {
      to: `/${culture}/organization/units`,
      label: t.nav.orgUnits,
      icon: Building2,
      permissions: [Permissions.Organization.UnitsView]
    },
    {
      to: `/${culture}/organization/positions`,
      label: t.nav.positions,
      icon: Briefcase,
      permissions: [Permissions.Organization.PositionsView]
    },
    {
      to: `/${culture}/organization/employees`,
      label: t.nav.employees,
      icon: Users,
      permissions: [Permissions.Organization.EmployeesView]
    },
    {
      to: `/${culture}/identity/users`,
      label: t.nav.users,
      icon: Users,
      permissions: [Permissions.Identity.UsersView]
    },
    {
      to: `/${culture}/identity/roles`,
      label: t.nav.roles,
      icon: ShieldCheck,
      permissions: [Permissions.Identity.RolesView]
    },
    { to: `/${culture}/settings`, label: t.nav.settings, icon: Settings }
  ];

  const visibleNavItems = navItems.filter(
    (item) => !item.permissions || hasAnyPermission(...item.permissions)
  );

  async function handleLogout() {
    await logout();
    navigate(`/${culture}/login`, { replace: true });
  }

  /**
   * ناوبری مشترک برای نوار کناری (دسکتاپ) و نوار افقی بالایی (موبایل).
   * آیتم‌ها shrink-0 هستند تا در نوار افقی فشرده نشوند.
   */
  function renderNavItems(itemClassName?: string) {
    if (visibleNavItems.length === 0) {
      return (
        <p className="px-3 py-2 text-xs text-muted-foreground">{t.errors.forbiddenDescription}</p>
      );
    }

    return visibleNavItems.map(({ to, label, icon: Icon }) => (
      <NavLink
        key={to}
        to={to}
        className={({ isActive }) =>
          cn(
            'flex shrink-0 items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            isActive
              ? 'bg-accent text-accent-foreground'
              : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
            itemClassName
          )
        }
      >
        <Icon className="size-4" />
        {label}
      </NavLink>
    ));
  }

  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground lg:flex-row">
      <aside className="hidden w-64 shrink-0 flex-col border-e bg-card lg:flex">
        <div className="flex h-16 items-center gap-2 border-b px-6">
          <div className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <ShieldCheck className="size-4" />
          </div>
          <span className="font-semibold">{t.app.name}</span>
        </div>

        <nav className="flex flex-1 flex-col gap-1 overflow-y-auto p-4">{renderNavItems()}</nav>
      </aside>

      <div className="flex flex-1 flex-col">
        <header className="flex h-16 items-center justify-end gap-4 border-b px-4 sm:px-6">
          <div className="me-auto flex items-center gap-2 lg:hidden">
            <div className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
              <ShieldCheck className="size-4" />
            </div>
            <span className="font-semibold">{t.app.name}</span>
          </div>

          <div className="flex items-center gap-2">
            <Button variant="ghost" size="icon" onClick={toggleCulture} aria-label={t.common.language}>
              <Languages className="size-4" />
            </Button>

            {user && (
              <UserMenu
                displayName={user.displayName}
                userName={user.userName}
                onProfile={() => navigate(`/${culture}/profile`)}
                onLogout={handleLogout}
              />
            )}
          </div>
        </header>

        <nav className="flex gap-1 overflow-x-auto border-b p-2 lg:hidden">{renderNavItems()}</nav>

        <main className="flex-1 p-4 sm:p-6">{children}</main>
      </div>
    </div>
  );
}

interface UserMenuProps {
  displayName: string;
  userName: string;
  onProfile: () => void;
  onLogout: () => void;
}

/**
 * منوی کاربر: نام نمایشی، پروفایل و خروج. با کلیک بیرون از منو بسته می‌شود.
 */
function UserMenu({ displayName, userName, onProfile, onLogout }: UserMenuProps) {
  const { t } = useLanguage();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useClickOutside(containerRef, () => setOpen(false), open);

  return (
    <div className="relative" ref={containerRef}>
      <Button
        variant="ghost"
        className="flex items-center gap-2 px-2"
        onClick={() => setOpen((previous) => !previous)}
        aria-expanded={open}
        aria-haspopup="menu"
      >
        <div
          className="flex size-7 items-center justify-center rounded-full bg-primary/10 text-xs font-medium text-primary"
          aria-hidden
        >
          {initials(displayName)}
        </div>
        <span className="hidden text-sm font-medium sm:inline">{displayName}</span>
        <ChevronDown className="size-4" />
      </Button>

      {open && (
        <div
          className="absolute end-0 top-full z-40 mt-2 w-56 rounded-md border bg-card text-card-foreground shadow-md"
          role="menu"
        >
          <div className="border-b p-3">
            <p className="text-sm font-medium">{displayName}</p>
            <p className="text-xs text-muted-foreground" dir="ltr">
              {userName}
            </p>
          </div>

          <div className="flex flex-col p-1">
            <button
              type="button"
              className="flex items-center gap-2 rounded-sm px-3 py-2 text-sm transition-colors hover:bg-accent"
              role="menuitem"
              onClick={() => {
                setOpen(false);
                onProfile();
              }}
            >
              <UserCircle className="size-4" />
              {t.nav.profile}
            </button>

            <button
              type="button"
              className="flex items-center gap-2 rounded-sm px-3 py-2 text-sm text-destructive transition-colors hover:bg-destructive/5"
              role="menuitem"
              onClick={() => {
                setOpen(false);
                onLogout();
              }}
            >
              <LogOut className="size-4" />
              {t.auth.signOut}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

/** حروف اول نام برای آواتار. */
function initials(displayName: string): string {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '؟';

  if (parts.length === 1) {
    return parts[0].slice(0, 2);
  }

  return (parts[0][0] ?? '') + (parts[1][0] ?? '');
}
