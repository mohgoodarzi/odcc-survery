import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { usersApi, type UserSummary } from '@/api/users';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { queryKeys } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

interface ResetPasswordDialogProps {
  user: UserSummary;
  onClose: () => void;
}

/**
 * بازنشانی رمز عبور توسط مدیر. رمز جدید یک‌بار وارد می‌شود چون مدیر
 * مستقیماً به کاربر می‌دهد.
 */
export function ResetPasswordDialog({ user, onClose }: ResetPasswordDialogProps) {
  const { t, culture } = useLanguage();
  const queryClient = useQueryClient();

  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | undefined>();
  const [success, setSuccess] = useState(false);

  const mutation = useMutation({
    mutationFn: () => usersApi.resetPassword(culture, { userId: user.id, newPassword }),
    onSuccess: () => {
      setSuccess(true);
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
    },
    onError: (mutationError) => {
      setError(
        mutationError instanceof ApiError
          ? t.errors.fromCode(mutationError.code)
          : t.errors.generic
      );
    }
  });

  useEffect(() => {
    setNewPassword('');
    setError(undefined);
    setSuccess(false);
  }, [user.id]);

  function validate(): boolean {
    if (!newPassword) {
      setError(t.users.password + ' ' + t.common.required);
      return false;
    }

    if (newPassword.length < 8 || !hasMixedCase(newPassword) || !hasDigit(newPassword)) {
      setError(t.errors.validation);
      return false;
    }

    setError(undefined);
    return true;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    mutation.mutate();
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={t.users.resetPassword}
      description={`${user.displayName} (${user.userName})`}
    >
      {success ? (
        <div className="flex flex-col gap-3">
          <div
            className="rounded-md border border-emerald-500/30 bg-emerald-500/5 p-3 text-sm text-emerald-700 dark:text-emerald-400"
            role="status"
          >
            {t.users.passwordReset}
          </div>
          <Button onClick={onClose}>{t.common.close}</Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          {error && (
            <div
              className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
              role="alert"
            >
              {error}
            </div>
          )}

          <FormField
            label={t.users.password}
            htmlFor="newPassword"
            required
            error={error}
            hint={t.errors.validation}
          >
            <Input
              id="newPassword"
              type="password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              disabled={mutation.isPending}
              dir="ltr"
            />
          </FormField>

          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
              {t.common.cancel}
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? t.common.saving : t.common.save}
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
}

function hasMixedCase(value: string): boolean {
  return /[a-z]/.test(value) && /[A-Z]/.test(value);
}

function hasDigit(value: string): boolean {
  return /\d/.test(value);
}
