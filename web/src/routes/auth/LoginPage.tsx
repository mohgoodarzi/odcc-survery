import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate, type Location } from 'react-router-dom';
import { LogIn, ShieldCheck } from 'lucide-react';

import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { FormField } from '@/components/ui/form-field';
import { ApiError } from '@/api/client';
import { FullPageSpinner } from '@/components/ui/spinner';

/**
 * صفحه‌ی ورود. اگر کاربر از قبل احراز هویت کرده باشد، به مسیر قبلی یا داشبورد
 * هدایت می‌شود. اعتبارسنجی سمت کلاینت قبل از ارسال انجام می‌شود.
 */
export function LoginPage() {
  const { t, culture, toggleCulture } = useLanguage();
  const { login, isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ userName?: string; password?: string; form?: string }>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  // اگر نشست در حال بازیابی است، صبر می‌کنیم تا مسیر درست را نشان دهیم.
  if (isLoading) {
    return <FullPageSpinner />;
  }

  if (isAuthenticated) {
    const from = readRedirectFrom(location);
    return <Navigate to={from ?? `/${culture}/dashboard`} replace />;
  }

  function validate(): boolean {
    const next: typeof errors = {};

    if (!userName.trim()) {
      next.userName = t.auth.userName + ' ' + t.common.required;
    }

    if (!password) {
      next.password = t.auth.password + ' ' + t.common.required;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!validate()) {
      return;
    }

    setIsSubmitting(true);
    setErrors({});

    try {
      await login(userName.trim(), password);

      const from = readRedirectFrom(location);
      navigate(from ?? `/${culture}/dashboard`, { replace: true });
    } catch (error) {
      const message =
        error instanceof ApiError
          ? t.errors.fromCode(error.code)
          : t.errors.generic;

      setErrors({ form: message });
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-md">
        <div className="mb-6 flex flex-col items-center gap-3 text-center">
          <div className="flex size-12 items-center justify-center rounded-xl bg-primary text-primary-foreground">
            <ShieldCheck className="size-6" />
          </div>
          <div>
            <h1 className="text-xl font-bold">{t.app.name}</h1>
            <p className="text-sm text-muted-foreground">{t.auth.welcomeBack}</p>
          </div>
        </div>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <LogIn className="size-5" />
              {t.auth.signIn}
            </CardTitle>
            <CardDescription>{t.auth.signInDescription}</CardDescription>
          </CardHeader>

          <CardContent>
            <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
              {errors.form && (
                <div
                  className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
                  role="alert"
                >
                  {errors.form}
                </div>
              )}

              <FormField label={t.auth.userName} htmlFor="userName" required error={errors.userName}>
                <Input
                  id="userName"
                  name="userName"
                  autoComplete="username"
                  autoFocus
                  value={userName}
                  onChange={(event) => setUserName(event.target.value)}
                  placeholder={t.auth.userName}
                  disabled={isSubmitting}
                  dir="ltr"
                />
              </FormField>

              <FormField label={t.auth.password} htmlFor="password" required error={errors.password}>
                <Input
                  id="password"
                  name="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="••••••••"
                  disabled={isSubmitting}
                  dir="ltr"
                />
              </FormField>

              <Button type="submit" disabled={isSubmitting} className="w-full">
                {isSubmitting ? t.auth.signingIn : t.auth.signInButton}
              </Button>
            </form>

            <div className="mt-6 flex items-center justify-between text-sm text-muted-foreground">
              <span>{t.auth.securityNote}</span>
              <Button variant="ghost" size="sm" onClick={toggleCulture}>
                {culture === 'fa' ? 'English' : 'فارسی'}
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

/** مسیر ذخیره‌شده برای بازگشت پس از ورود. فقط مسیرهای داخلی قابل قبول هستند. */
function readRedirectFrom(location: Location): string | null {
  const params = new URLSearchParams(location.search);
  const redirect = params.get('redirect');
  if (!redirect) return null;

  const decoded = decodeURIComponent(redirect);
  if (decoded.startsWith('/') && !decoded.startsWith('//')) {
    return decoded;
  }

  return null;
}
