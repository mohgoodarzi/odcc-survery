import {
  createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode
} from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import { authApi, type UserProfile } from '@/api/auth';
import { ApiError, configureAuth } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  clearSession, isAccessTokenExpired, readSession, writeSession, type StoredSession
} from './session';

interface AuthState {
  /** کاربر احراز هویت‌شده یا null. */
  user: UserProfile | null;
  /** آیا در حال بارگذاری نشست اولیه است. */
  isLoading: boolean;
  /** آیا کاربر احراز هویت شده است؟ */
  isAuthenticated: boolean;
}

interface AuthContextValue extends AuthState {
  login: (userName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  /** به‌روزرسانی پروفایل در حافظه (مثلاً پس از تغییر زبان یا ویرایش پروفایل). */
  refreshUser: () => Promise<void>;
  /** آیا کاربر این مجوز را دارد؟ */
  hasPermission: (permission: string) => boolean;
  /** آیا کاربر حداقل یکی از این مجوزها را دارد؟ */
  hasAnyPermission: (...permissions: string[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const { culture } = useLanguage();
  const navigate = useNavigate();
  const location = useLocation();

  const [user, setUser] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // نشست فعال در حافظه؛ توکن دسترسی فقط اینجا نگه‌داری می‌شود.
  const sessionRef = useRef<StoredSession | null>(readSession());

  const persistSession = useCallback((session: StoredSession) => {
    sessionRef.current = session;
    writeSession(session);
  }, []);

  const clearLocalSession = useCallback(() => {
    sessionRef.current = null;
    clearSession();
    setUser(null);
  }, []);

  /**
   * تازه‌سازی نشست با توکن تازه‌سازی ذخیره‌شده.
   * این متد به کلاینت API داده می‌شود تا در صورت دریافت ۴۰۱ یک‌بار آن را صدا بزند.
   * یک promise برمی‌گرداند که در صورت موفقیت توکن جدید را تحویل می‌دهد.
   */
  const refreshSession = useCallback(async (): Promise<string | null> => {
    const session = sessionRef.current;
    if (!session) {
      return null;
    }

    try {
      const tokens = await authApi.refresh(culture, {
        accessToken: session.accessToken,
        refreshToken: session.refreshToken
      });

      persistSession({
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        expiresAt: tokens.expiresAt
      });

      return tokens.accessToken;
    } catch {
      clearLocalSession();
      return null;
    }
  }, [culture, persistSession, clearLocalSession]);

  // ثبت منابع توکن در کلاینت API تا درخواست‌ها مجوزدار شوند.
  useEffect(() => {
    configureAuth(() => sessionRef.current?.accessToken ?? null, refreshSession);
    return () => {
      configureAuth(() => null, async () => null);
    };
  }, [refreshSession]);

  /**
   * بارگذاری نشست هنگام راه‌اندازی. اگر نشستی وجود داشته باشد، یک‌بار توکن
   * تازه می‌شود تا یک access token معتبر و پروفایل کاربر به دست آید.
   */
  useEffect(() => {
    let cancelled = false;

    async function restoreSession() {
      const stored = readSession();
      if (!stored) {
        if (!cancelled) {
          clearLocalSession();
          setIsLoading(false);
        }
        return;
      }

      sessionRef.current = stored;

      // اگر توکن دسترسی منقضی شده باشد، یک‌بار تازه می‌شود تا هم نشست اعتبارسنجی
      // شود و هم توکن معتبر در حافظه قرار گیرد. پروفایل جداگانه بارگذاری می‌شود.
      if (isAccessTokenExpired(stored.expiresAt)) {
        const refreshed = await refreshSession();
        if (cancelled) return;

        if (refreshed === null) {
          clearLocalSession();
          setIsLoading(false);
          return;
        }
      }

      try {
        const profile = await authApi.me(culture);
        if (cancelled) return;

        setUser(profile);
      } catch {
        if (cancelled) return;
        clearLocalSession();
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    void restoreSession();

    return () => {
      cancelled = true;
    };
    // فقط یک‌بار در زمان راه‌اندازی اجرا می‌شود.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const login = useCallback(
    async (userName: string, password: string) => {
      const result = await authApi.login(culture, { userName, password });

      // در فاز ۱، MFA فعال نیست؛ ساختار برای فاز بعدی حفظ شده است.
      if (result.requiresMfa || !result.tokens) {
        throw new ApiError(401, {
          title: 'نیاز به احراز هویت دومرحله‌ای',
          detail: 'احراز هویت دومرحله‌ای در این نسخه پشتیبانی نمی‌شود.'
        });
      }

      const tokens = result.tokens;
      persistSession({
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        expiresAt: tokens.expiresAt
      });

      setUser(tokens.profile ?? null);
    },
    [culture, persistSession]
  );

  const logout = useCallback(async () => {
    const session = sessionRef.current;

    if (session) {
      // خروج باید idempotent باشد؛ خطا در سمت سرور نباید کاربر را گیر بیندازد.
      try {
        await authApi.logout(culture, {
          accessToken: session.accessToken,
          refreshToken: session.refreshToken
        });
      } catch {
        /* نشست سمت سرور ممکن است قبلاً باطل شده باشد. */
      }
    }

    clearLocalSession();
  }, [culture, clearLocalSession]);

  const refreshUser = useCallback(async () => {
    try {
      const profile = await authApi.me(culture);
      setUser(profile);
    } catch {
      /* پروفایل به‌روز نشد؛ حالت فعلی حفظ می‌شود. */
    }
  }, [culture]);

  const hasPermission = useCallback(
    (permission: string) => user?.permissions.includes(permission) ?? false,
    [user]
  );

  const hasAnyPermission = useCallback(
    (...permissions: string[]) => permissions.some((p) => user?.permissions.includes(p) ?? false),
    [user]
  );

  // وقتی کاربر از یک مسیر محافظت‌شده خارج می‌شود، نشست باید پاک شود.
  // این effect برای اطمینان از پاک‌سازی نشست هنگام unmount نیست، بلکه
  // برای همگام‌سازی مسیر فعلی با وضعیت احراز هویت است.
  useEffect(() => {
    if (!isLoading && !user && isProtectedPath(location.pathname)) {
      // کاربر احراز هویت‌نشده روی مسیر محافظت‌شده: به ورود هدایت می‌شود.
      const redirect = encodeURIComponent(location.pathname + location.search);
      navigate(`/${culture}/login?redirect=${redirect}`, { replace: true });
    }
  }, [isLoading, user, location.pathname, location.search, culture, navigate]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      isAuthenticated: user !== null,
      login,
      logout,
      refreshUser,
      hasPermission,
      hasAnyPermission
    }),
    [user, isLoading, login, logout, refreshUser, hasPermission, hasAnyPermission]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function isProtectedPath(pathname: string): boolean {
  return !/^\/(?:fa|en)\/login(?:\/|$)/.test(pathname);
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
