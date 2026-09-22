import { useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { KeyRound, Mail, Phone, Building2, ShieldCheck } from 'lucide-react';

import { usersApi } from '@/api/users';
import { ApiError } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import { useLanguage } from '@/i18n/LanguageProvider';
import { PageHeader } from '@/components/ui/page-header';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { AppLayout } from '@/layouts/AppLayout';

/**
 * صفحه‌ی پروفایل: اطلاعات کاربر، نقش‌ها، مجوزها و تغییر رمز عبور.
 */
export function ProfilePage() {
  const { t, culture } = useLanguage();
  const { user, refreshUser } = useAuth();

  if (!user) {
    return null;
  }

  return (
    <AppLayout>
      <PageHeader title={t.profile.title} description={t.profile.personalInfo} />

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t.profile.personalInfo}</CardTitle>
            <CardDescription>{user.displayName}</CardDescription>
          </CardHeader>

          <CardContent className="flex flex-col gap-3">
            <InfoRow
              icon={<Mail className="size-4" />}
              label={t.users.email}
              value={user.email ?? t.common.noData}
            />
            <InfoRow
              icon={<Phone className="size-4" />}
              label={t.users.phoneNumber}
              value={user.phoneNumber ?? t.common.noData}
            />
            <InfoRow
              icon={<Building2 className="size-4" />}
              label={t.profile.orgUnit}
              value={user.orgUnitName ?? t.common.noData}
            />
            <InfoRow
              icon={<ShieldCheck className="size-4" />}
              label={t.profile.dataScope}
              value={dataScopeLabel(t, user.dataScope)}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t.profile.roles}</CardTitle>
            <CardDescription>{t.profile.permissions}</CardDescription>
          </CardHeader>

          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-wrap gap-2">
              {user.roles.length > 0 ? (
                user.roles.map((role) => (
                  <Badge key={role} variant="default">
                    {role}
                  </Badge>
                ))
              ) : (
                <span className="text-sm text-muted-foreground">{t.common.noData}</span>
              )}
            </div>

            <div className="flex flex-wrap gap-1.5">
              {user.permissions.length > 0 ? (
                user.permissions.map((permission) => (
                  <Badge key={permission} variant="outline" dir="ltr">
                    {permission}
                  </Badge>
                ))
              ) : (
                <span className="text-sm text-muted-foreground">{t.profile.noPermissions}</span>
              )}
            </div>
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <KeyRound className="size-5" />
              {t.profile.changePassword}
            </CardTitle>
          </CardHeader>

          <CardContent>
            <ChangePasswordForm onChanged={refreshUser} culture={culture} />
          </CardContent>
        </Card>
      </div>
    </AppLayout>
  );
}

function InfoRow({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="flex items-center gap-3 border-b pb-3 last:border-0 last:pb-0">
      <span className="text-muted-foreground">{icon}</span>
      <span className="w-32 shrink-0 text-sm text-muted-foreground">{label}</span>
      <span className="text-sm font-medium">{value}</span>
    </div>
  );
}

function dataScopeLabel(t: ReturnType<typeof useLanguage>['t'], scope: number): string {
  switch (scope) {
    case 0:
      return t.dataScope.own;
    case 1:
      return t.dataScope.team;
    case 2:
      return t.dataScope.department;
    case 3:
      return t.dataScope.division;
    case 4:
      return t.dataScope.company;
    default:
      return t.dataScope.own;
  }
}

interface ChangePasswordFormProps {
  onChanged: () => Promise<void>;
  culture: ReturnType<typeof useLanguage>['culture'];
}

function ChangePasswordForm({ onChanged, culture }: ChangePasswordFormProps) {
  const { t } = useLanguage();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  const mutation = useMutation({
    mutationFn: () =>
      usersApi.changePassword(culture, {
        currentPassword,
        newPassword
      }),
    onSuccess: async () => {
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setFieldErrors({});
      setFormError(undefined);
      await onChanged();
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        if (error.status === 400 && Object.keys(error.validationErrors).length > 0) {
          setFieldErrors(translateValidationErrors(t, error.validationErrors));
        } else {
          setFormError(t.errors.fromCode(error.code));
        }
      } else {
        setFormError(t.errors.generic);
      }
    }
  });

  function validate(): boolean {
    const errors: Record<string, string> = {};

    if (!currentPassword) {
      errors.currentPassword = t.profile.currentPassword + ' ' + t.common.required;
    }

    if (!newPassword) {
      errors.newPassword = t.profile.newPassword + ' ' + t.common.required;
    } else if (newPassword.length < 8) {
      errors.newPassword = t.errors.validation;
    } else if (!hasMixedCase(newPassword) || !hasDigit(newPassword)) {
      errors.newPassword = t.errors.validation;
    }

    if (newPassword !== confirmPassword) {
      errors.confirmPassword = t.errors.validation;
    }

    if (newPassword === currentPassword && newPassword) {
      errors.newPassword = t.errors.validation;
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);
    mutation.mutate();
  }

  return (
    <form onSubmit={handleSubmit} className="flex max-w-md flex-col gap-4" noValidate>
      {formError && (
        <div
          className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
          role="alert"
        >
          {formError}
        </div>
      )}

      <FormField
        label={t.profile.currentPassword}
        htmlFor="currentPassword"
        required
        error={fieldErrors.currentPassword}
      >
        <Input
          id="currentPassword"
          type="password"
          autoComplete="current-password"
          value={currentPassword}
          onChange={(event) => setCurrentPassword(event.target.value)}
          disabled={mutation.isPending}
          dir="ltr"
        />
      </FormField>

      <FormField
        label={t.profile.newPassword}
        htmlFor="newPassword"
        required
        error={fieldErrors.newPassword}
        hint={t.errors.validation}
      >
        <Input
          id="newPassword"
          type="password"
          autoComplete="new-password"
          value={newPassword}
          onChange={(event) => setNewPassword(event.target.value)}
          disabled={mutation.isPending}
          dir="ltr"
        />
      </FormField>

      <FormField
        label={t.profile.confirmPassword}
        htmlFor="confirmPassword"
        required
        error={fieldErrors.confirmPassword}
      >
        <Input
          id="confirmPassword"
          type="password"
          autoComplete="new-password"
          value={confirmPassword}
          onChange={(event) => setConfirmPassword(event.target.value)}
          disabled={mutation.isPending}
          dir="ltr"
        />
      </FormField>

      <Button type="submit" disabled={mutation.isPending} className="w-fit">
        {mutation.isPending ? t.common.saving : t.common.save}
      </Button>
    </form>
  );
}

function hasMixedCase(value: string): boolean {
  return /[a-z]/.test(value) && /[A-Z]/.test(value);
}

function hasDigit(value: string): boolean {
  return /\d/.test(value);
}

/** تبدیل دیکشنری خطاهای اعتبارسنجی سرور به پیام‌های محلی. */
function translateValidationErrors(
  t: ReturnType<typeof useLanguage>['t'],
  errors: Record<string, string[]>
): Record<string, string> {
  const result: Record<string, string> = {};

  for (const [field, messages] of Object.entries(errors)) {
    result[field] = messages[0] ?? t.errors.validation;
  }

  return result;
}
