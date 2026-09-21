/**
 * قرارداد فرهنگ‌ها و دیکشنری‌ها.
 * هر زبان یک فایل دیکشنری دارد که اینترفیس Dictionary را پیاده‌سازی می‌کند.
 */

export type Culture = 'fa' | 'en';
export type Direction = 'rtl' | 'ltr';

export const CULTURES: Culture[] = ['fa', 'en'];
export const DEFAULT_CULTURE: Culture = 'fa';

export const CULTURE_TO_LOCALE: Record<Culture, string> = {
  fa: 'fa-IR',
  en: 'en-US'
};

/** دیکشنری رشته‌های رابط کاربری. افزودن زبان = پیاده‌سازی این اینترفیس. */
export interface Dictionary {
  meta: {
    title: string;
    description: string;
  };
  app: {
    name: string;
    tagline: string;
  };
  nav: {
    dashboard: string;
    surveys: string;
    campaigns: string;
    analytics: string;
    reports: string;
    settings: string;
  };
  common: {
    search: string;
    quickActions: string;
    profile: string;
    notifications: string;
    language: string;
    loading: string;
    error: string;
    retry: string;
    save: string;
    cancel: string;
    notFound: string;
    notFoundDescription: string;
    goHome: string;
  };
}
