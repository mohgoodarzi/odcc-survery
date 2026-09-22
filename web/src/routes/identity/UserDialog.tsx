import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { usersApi, type CreateUserRequest, type UpdateUserRequest, type UserSummary } from '@/api/users';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { queryKeys, useOrgUnits } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';

interface UserDialogProps {
  open: boolean;
  onClose: () => void;
  user?: UserSummary | null;
}

/**
 * دیالوگ ایجاد/ویرایش کاربر. در حالت ویرایش رمز عبور پرسیده نمی‌شود.
 */
export function UserDialog({ open, onClose, user }: UserDialogProps) {
  const { t, culture } = useLanguage();
  const queryClient = useQueryClient();
  const isEdit = !!user;

  const [form, setForm] = useState(() => createEmptyForm(user));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  const { data: orgUnits } = useOrgUnits();

  // هنگام باز شدن دیالوگ، فرم ریست می‌شود.
  useEffect(() => {
    if (open) {
      setForm(createEmptyForm(user));
      setErrors({});
      setFormError(undefined);
    }
  }, [open, user]);

  const mutation = useMutation({
    mutationFn: () => {
      if (isEdit && user) {
        const request: UpdateUserRequest = {
          firstName: form.firstName,
          lastName: form.lastName,
          email: form.email,
          phoneNumber: form.phoneNumber || null,
          orgUnitId: form.orgUnitId || null,
          dataScope: Number(form.dataScope)
        };
        return usersApi.update(culture, user.id, request);
      }

      const request: CreateUserRequest = {
        userName: form.userName,
        email: form.email,
        password: form.password,
        firstName: form.firstName,
        lastName: form.lastName,
        phoneNumber: form.phoneNumber || null,
        orgUnitId: form.orgUnitId || null,
        dataScope: Number(form.dataScope),
        isActive: true
      };
      return usersApi.create(culture, request);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        if (error.status === 400 && Object.keys(error.validationErrors).length > 0) {
          const translated: Record<string, string> = {};
          for (const [field, messages] of Object.entries(error.validationErrors)) {
            translated[field] = messages[0] ?? t.errors.validation;
          }
          setErrors(translated);
        } else {
          setFormError(t.errors.fromCode(error.code));
        }
      } else {
        setFormError(t.errors.generic);
      }
    }
  });

  function updateField(field: string, value: string) {
    setForm((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => {
      if (!(field in previous)) return previous;
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  function validate(): boolean {
    const next: Record<string, string> = {};

    if (!isEdit) {
      if (!form.userName.trim()) next.userName = t.users.userName + ' ' + t.common.required;
      else if (form.userName.length < 3) next.userName = t.errors.validation;
      else if (!/^[a-zA-Z0-9._-]+$/.test(form.userName)) next.userName = t.errors.validation;

      if (!form.password) next.password = t.users.password + ' ' + t.common.required;
      else if (form.password.length < 8) next.password = t.errors.validation;
      else if (!hasMixedCase(form.password) || !hasDigit(form.password)) {
        next.password = t.errors.validation;
      }
    }

    if (!form.firstName.trim()) next.firstName = t.users.firstName + ' ' + t.common.required;
    if (!form.lastName.trim()) next.lastName = t.users.lastName + ' ' + t.common.required;
    if (!form.email.trim()) next.email = t.users.email + ' ' + t.common.required;
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) next.email = t.errors.validation;

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);
    mutation.mutate();
  }

  const orgUnitOptions = (orgUnits ?? []).map((unit) => ({
    value: unit.id,
    label: `${unit.name} (${unit.code})`
  }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.users.editUser : t.users.newUser}
      description={isEdit ? user?.displayName : t.users.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="user-form" disabled={mutation.isPending}>
            {mutation.isPending ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="user-form" onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        {!isEdit && (
          <FormField label={t.users.userName} htmlFor="userName" required error={errors.userName}>
            <Input
              id="userName"
              value={form.userName}
              onChange={(event) => updateField('userName', event.target.value)}
              disabled={mutation.isPending}
              dir="ltr"
            />
          </FormField>
        )}

        <FormField label={t.users.firstName} htmlFor="firstName" required error={errors.firstName}>
          <Input
            id="firstName"
            value={form.firstName}
            onChange={(event) => updateField('firstName', event.target.value)}
            disabled={mutation.isPending}
          />
        </FormField>

        <FormField label={t.users.lastName} htmlFor="lastName" required error={errors.lastName}>
          <Input
            id="lastName"
            value={form.lastName}
            onChange={(event) => updateField('lastName', event.target.value)}
            disabled={mutation.isPending}
          />
        </FormField>

        <FormField label={t.users.email} htmlFor="email" required error={errors.email}>
          <Input
            id="email"
            type="email"
            value={form.email}
            onChange={(event) => updateField('email', event.target.value)}
            disabled={mutation.isPending}
            dir="ltr"
          />
        </FormField>

        <FormField label={t.users.phoneNumber} htmlFor="phoneNumber" error={errors.phoneNumber}>
          <Input
            id="phoneNumber"
            value={form.phoneNumber}
            onChange={(event) => updateField('phoneNumber', event.target.value)}
            disabled={mutation.isPending}
            dir="ltr"
          />
        </FormField>

        {!isEdit && (
          <FormField
            label={t.users.password}
            htmlFor="password"
            required
            error={errors.password}
            hint={t.errors.validation}
          >
            <Input
              id="password"
              type="password"
              value={form.password}
              onChange={(event) => updateField('password', event.target.value)}
              disabled={mutation.isPending}
              dir="ltr"
            />
          </FormField>
        )}

        <FormField label={t.users.orgUnit} htmlFor="orgUnitId">
          <Select
            id="orgUnitId"
            value={form.orgUnitId}
            onChange={(event) => updateField('orgUnitId', event.target.value)}
            options={orgUnitOptions}
            placeholder={t.orgUnits.selectParent}
            disabled={mutation.isPending}
          />
        </FormField>

        <FormField label={t.users.dataScope} htmlFor="dataScope">
          <Select
            id="dataScope"
            value={form.dataScope}
            onChange={(event) => updateField('dataScope', event.target.value)}
            options={[
              { value: '0', label: t.dataScope.own },
              { value: '1', label: t.dataScope.team },
              { value: '2', label: t.dataScope.department },
              { value: '3', label: t.dataScope.division },
              { value: '4', label: t.dataScope.company }
            ]}
            disabled={mutation.isPending}
          />
        </FormField>
      </form>
    </Dialog>
  );
}

interface UserFormState {
  userName: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  password: string;
  orgUnitId: string;
  dataScope: string;
}

function createEmptyForm(user?: UserSummary | null): UserFormState {
  return {
    userName: user?.userName ?? '',
    firstName: user?.firstName ?? '',
    lastName: user?.lastName ?? '',
    email: user?.email ?? '',
    phoneNumber: '',
    password: '',
    orgUnitId: user?.orgUnitId ?? '',
    dataScope: '0'
  };
}

function hasMixedCase(value: string): boolean {
  return /[a-z]/.test(value) && /[A-Z]/.test(value);
}

function hasDigit(value: string): boolean {
  return /\d/.test(value);
}
