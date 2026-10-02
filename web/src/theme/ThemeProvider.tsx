import {
  createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode
} from 'react';

export type Theme = 'light' | 'dark' | 'green';

/** تمام تم‌های پشتیبانی‌شده به ترتیب نمایش در انتخابگر. */
export const THEMES: readonly Theme[] = ['light', 'dark', 'green'];

const STORAGE_KEY = 'odcc.survey.theme';

interface ThemeContextValue {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function readPersistedTheme(): Theme | null {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    return THEMES.includes(stored as Theme) ? (stored as Theme) : null;
  } catch {
    return null;
  }
}

function readSystemTheme(): Theme {
  try {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  } catch {
    return 'light';
  }
}

/**
 * اولویت: تنظیم ذخیره‌شده‌ی کاربر، سپس ترجیح سیستمی، سپس حالت روشن.
 * index.html همان ترتیب را قبل از اولین رنگ‌پردازی اعمال می‌کند تا
 * چشمک تم اشتباه (FOUC) رخ ندهد.
 */
export function resolveTheme(): Theme {
  return readPersistedTheme() ?? readSystemTheme();
}

/**
 * اعمال تم روی سند: کلاس مخصوص هر تم (.dark / .green) روی <html> برای
 * توکن‌های رنگ و color-scheme برای کنترل‌های بومی (اسکرول‌بار، select،
 * date picker). تم روشن هیچ کلاسی نمی‌گیرد (پایه‌ی :root است).
 *
 * تم «سبز ODCC» مانند تم تاریک یک تم تیره است، بنابراین color-scheme
 * آن هم dark است تا کنترل‌های بومی (scrollbar، select بومی، date picker)
 * با پس‌زمینه‌ی سبز تیره هماهنگ شوند.
 */
export function applyTheme(theme: Theme) {
  const html = document.documentElement;
  html.classList.remove('dark', 'green');
  if (theme === 'dark' || theme === 'green') {
    html.classList.add(theme);
  }
  html.style.colorScheme = theme === 'light' ? 'light' : 'dark';
}

/**
 * فراهم‌کننده‌ی تم برنامه.
 * تم انتخاب‌شده در localStorage ذخیره می‌شود تا پس از بارگذاری مجدد
 * یا باز شدن دوباره‌ی برنامه حفظ بماند.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(() => resolveTheme());

  const setTheme = useCallback((next: Theme) => {
    setThemeState(next);

    try {
      window.localStorage.setItem(STORAGE_KEY, next);
    } catch {
      /* storage ممکن است در حالت خصوصی در دسترس نباشد؛ تم در حافظه باقی می‌ماند. */
    }
  }, []);

  const toggleTheme = useCallback(() => {
    // چرخش بین سه تم: روشن → تاریک → سبز → روشن.
    const currentIndex = THEMES.indexOf(theme);
    const next = THEMES[(currentIndex + 1) % THEMES.length];
    setTheme(next);
  }, [theme, setTheme]);

  // اعمال-theme روی سند با هر تغییر.
  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  // اگر کاربر هنوز تمی را انتخاب نکرده، تغییر ترجیح سیستمی را دنبال می‌کنیم.
  // انتخاب صریح کاربر همواره اولویت دارد.
  useEffect(() => {
    let media: MediaQueryList;
    try {
      media = window.matchMedia('(prefers-color-scheme: dark)');
    } catch {
      return;
    }

    const handleChange = (event: MediaQueryListEvent) => {
      if (!readPersistedTheme()) {
        setThemeState(event.matches ? 'dark' : 'light');
      }
    };

    media.addEventListener('change', handleChange);
    return () => media.removeEventListener('change', handleChange);
  }, []);

  const value = useMemo<ThemeContextValue>(
    () => ({ theme, setTheme, toggleTheme }),
    [theme, setTheme, toggleTheme]
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}
