import {
  createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode
} from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import type { Culture, Direction, Dictionary } from './types';
import { CULTURE_TO_LOCALE, DEFAULT_CULTURE } from './types';
import { fa } from './dictionaries/fa';
import { en } from './dictionaries/en';

const DICTIONARIES: Record<Culture, Dictionary> = { fa, en };
const STORAGE_KEY = 'odcc.survey.culture';

interface LanguageContextValue {
  culture: Culture;
  direction: Direction;
  locale: string;
  t: Dictionary;
  setCulture: (culture: Culture) => void;
  toggleCulture: () => void;
}

const LanguageContext = createContext<LanguageContextValue | null>(null);

export function readCultureFromPath(pathname: string): Culture | null {
  const match = pathname.match(/^\/(fa|en)(?:\/|$)/);
  return match ? (match[1] as Culture) : null;
}

function readPersistedCulture(): Culture | null {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    return stored === 'fa' || stored === 'en' ? stored : null;
  } catch {
    return null;
  }
}

/**
 * مسیر برنده است، سپس تنظیم ذخیره‌شده، سپس پیش‌فرض سامانه (فارسی).
 */
export function resolveCulture(pathname: string): Culture {
  return readCultureFromPath(pathname) ?? readPersistedCulture() ?? DEFAULT_CULTURE;
}

export function LanguageProvider({ children }: { children: ReactNode }) {
  const location = useLocation();
  const navigate = useNavigate();
  const [culture, setCultureState] = useState<Culture>(() => resolveCulture(location.pathname));

  const setCulture = useCallback(
    (next: Culture) => {
      setCultureState(next);
      try {
        window.localStorage.setItem(STORAGE_KEY, next);
      } catch {
        /* storage ممکن است در حالت خصوصی در دسترس نباشد؛ مسیر هنوز تنظیمات را حمل می‌کند. */
      }

      // فقط بخش فرهنگ بازنویسی می‌شود و بقیه‌ی مسیر حفظ می‌شود.
      const remainder = location.pathname.replace(/^\/(?:fa|en)(?=\/|$)/, '') || '/';
      const path = `/${next}${remainder === '/' ? '' : remainder}${location.hash}`;
      navigate(path);
    },
    [location.pathname, location.hash, navigate]
  );

  const toggleCulture = useCallback(() => {
    setCulture(culture === 'fa' ? 'en' : 'fa');
  }, [culture, setCulture]);

  // همگام‌سازی با دکمه‌های back/forward مرورگر.
  useEffect(() => {
    const next = resolveCulture(location.pathname);
    setCultureState(next);
  }, [location.pathname]);

  const value = useMemo<LanguageContextValue>(
    () => ({
      culture,
      direction: culture === 'fa' ? 'rtl' : 'ltr',
      locale: CULTURE_TO_LOCALE[culture],
      t: DICTIONARIES[culture],
      setCulture,
      toggleCulture
    }),
    [culture, setCulture, toggleCulture]
  );

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useLanguage(): LanguageContextValue {
  const context = useContext(LanguageContext);
  if (!context) {
    throw new Error('useLanguage must be used within a LanguageProvider');
  }
  return context;
}

export { CULTURE_TO_LOCALE };
