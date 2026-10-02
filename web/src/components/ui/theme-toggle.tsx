import { useRef, useState } from 'react';
import { Check, ChevronDown, Leaf, Moon, Sun } from 'lucide-react';
import type { ComponentType } from 'react';

import { useTheme, type Theme } from '@/theme/ThemeProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useClickOutside } from '@/lib/use-click-outside';
import { Button } from '@/components/ui/button';

interface ThemeOption {
  value: Theme;
  label: string;
  icon: ComponentType<{ className?: string }>;
}

/**
 * انتخابگر تم برنامه: سه گزینه‌ی «روشن»، «تاریک» و «سبز ODCC».
 *
 * تم روشن پایه‌ی پیش‌فرض است، تم تاریک کلاس .dark و تم سبز کلاس .green را
 * روی <html> فعال می‌کنند. انتخاب کاربر از طریق ThemeProvider در
 * localStorage ذخیره می‌شود.
 */
export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const { t } = useLanguage();

  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useClickOutside(containerRef, () => setOpen(false), open);

  const options: ThemeOption[] = [
    { value: 'light', label: t.common.lightTheme, icon: Sun },
    { value: 'dark', label: t.common.darkTheme, icon: Moon },
    { value: 'green', label: t.common.greenTheme, icon: Leaf }
  ];

  const current = options.find((option) => option.value === theme) ?? options[0];
  const CurrentIcon = current.icon;

  return (
    <div className="relative" ref={containerRef}>
      <Button
        variant="ghost"
        className="flex items-center gap-2 px-2"
        onClick={() => setOpen((previous) => !previous)}
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label={t.common.theme}
        title={t.common.theme}
      >
        <CurrentIcon className="size-4" />
        <span className="hidden text-sm font-medium sm:inline">{current.label}</span>
        <ChevronDown className="size-4" />
      </Button>

      {open && (
        <div
          className="absolute end-0 top-full z-40 mt-2 w-48 rounded-md border bg-card text-card-foreground shadow-md"
          role="menu"
        >
          <div className="flex flex-col p-1">
            {options.map((option) => {
              const Icon = option.icon;
              const isActive = option.value === theme;

              return (
                <button
                  key={option.value}
                  type="button"
                  className="flex items-center gap-2 rounded-sm px-3 py-2 text-sm transition-colors hover:bg-accent hover:text-accent-foreground"
                  role="menuitemradio"
                  aria-checked={isActive}
                  onClick={() => {
                    setTheme(option.value);
                    setOpen(false);
                  }}
                >
                  <Icon className="size-4" />
                  <span className="flex-1 text-start">{option.label}</span>
                  {isActive && <Check className="size-4 text-primary" />}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
