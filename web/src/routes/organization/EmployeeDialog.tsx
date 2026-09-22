import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';

import {
  employeesApi, EmployeeStatus, type SaveEmployeeRequest
} from '@/api/organization';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useEmployee, useManagerOptions, useOrgUnits, usePositions } from '@/api/hooks';import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';

interface EmployeeDialogProps {
  open: boolean;
  onClose: () => void;
  employeeId?: string | null;
}

/**
 * دیالوگ ایجاد/ویرایش کارمند. در حالت ویرایش، کارمند از سرور بارگذاری می‌شود.
 */
export function EmployeeDialog({ open, onClose, employeeId }: EmployeeDialogProps) {
  const { t, culture } = useLanguage();
  const queryClient = useQueryClient();
  const isEdit = !!employeeId;

  const { data: existingEmployee } = useEmployee(open ? (employeeId ?? null) : null);
  const { data: orgUnits } = useOrgUnits();
  const { data: positions } = usePositions();
  const { data: managers } = useManagerOptions();

  const [form, setForm] = useState(() => createEmptyForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!open) return;

    setForm(
      existingEmployee
        ? {
            employeeCode: existingEmployee.employeeCode,
            nationalCode: existingEmployee.nationalCode ?? '',
            firstName: existingEmployee.firstName,
            lastName: existingEmployee.lastName,
            fatherName: existingEmployee.fatherName ?? '',
            orgUnitId: existingEmployee.orgUnitId,
            positionId: existingEmployee.positionId ?? '',
            managerId: existingEmployee.managerId ?? '',
            status: String(existingEmployee.status),
            startDate: existingEmployee.startDate,
            endDate: existingEmployee.endDate ?? '',
            workEmail: existingEmployee.workEmail ?? '',
            internalPhone: existingEmployee.internalPhone ?? ''
          }
        : createEmptyForm()
    );
    setErrors({});
    setFormError(undefined);
  }, [open, existingEmployee]);

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

    if (!form.employeeCode.trim()) {
      next.employeeCode = t.employees.employeeCode + ' ' + t.common.required;
    }

    if (!form.firstName.trim()) next.firstName = t.employees.firstName + ' ' + t.common.required;
    if (!form.lastName.trim()) next.lastName = t.employees.lastName + ' ' + t.common.required;
    if (!form.orgUnitId) next.orgUnitId = t.employees.orgUnit + ' ' + t.common.required;

    if (!form.startDate) {
      next.startDate = t.employees.startDate + ' ' + t.common.required;
    } else if (form.endDate && form.endDate < form.startDate) {
      next.endDate = t.errors.validation;
    }

    if (form.nationalCode && !/^\d{10}$/.test(form.nationalCode.trim())) {
      next.nationalCode = t.errors.validation;
    }

    if (form.workEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.workEmail.trim())) {
      next.workEmail = t.errors.validation;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);
    setIsSaving(true);

    const request: SaveEmployeeRequest = {
      employeeCode: form.employeeCode.trim(),
      nationalCode: form.nationalCode.trim() || null,
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      fatherName: form.fatherName.trim() || null,
      orgUnitId: form.orgUnitId,
      positionId: form.positionId || null,
      managerId: form.managerId || null,
      status: Number(form.status) as EmployeeStatus,
      startDate: form.startDate,
      endDate: form.endDate || null,
      workEmail: form.workEmail.trim() || null,
      internalPhone: form.internalPhone.trim() || null
    };

    try {
      if (isEdit && employeeId) {
        await employeesApi.update(culture, employeeId, request);
      } else {
        await employeesApi.create(culture, request);
      }

      void queryClient.invalidateQueries({ queryKey: ['employees'] });
      onClose();
    } catch (error) {
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
    } finally {
      setIsSaving(false);
    }
  }

  const orgUnitOptions = (orgUnits ?? []).map((unit) => ({
    value: unit.id,
    label: `${unit.name} (${unit.code})`
  }));

  // موقعیت‌های واحد سازمانی انتخاب‌شده؛ موقعیت باید به واحد تعلق داشته باشد.
  const positionOptions = (positions ?? [])
    .filter((position) => !form.orgUnitId || position.orgUnitId === form.orgUnitId)
    .map((position) => ({
      value: position.id,
      label: `${position.title} (${position.code})`
    }));

  // مدیر نمی‌تواند خود کارمند باشد.
  const managerOptions = (managers?.items ?? [])
    .filter((manager) => manager.id !== employeeId)
    .map((manager) => ({
      value: manager.id,
      label: `${manager.fullName} (${manager.employeeCode})`
    }));

  const statusOptions = useMemo(
    () => [
      { value: String(EmployeeStatus.Active), label: t.employees.active },
      { value: String(EmployeeStatus.OnLeave), label: t.employees.onLeave },
      { value: String(EmployeeStatus.Suspended), label: t.employees.suspended },
      { value: String(EmployeeStatus.Terminated), label: t.employees.terminated }
    ],
    [t.employees]
  );

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.employees.editEmployee : t.employees.newEmployee}
      description={isEdit ? existingEmployee?.fullName : t.employees.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="employee-form" disabled={isSaving}>
            {isSaving ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form
        id="employee-form"
        onSubmit={handleSubmit}
        className="grid gap-4 sm:grid-cols-2"
        noValidate
      >
        {formError && (
          <div
            className="col-span-full rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <FormField
          label={t.employees.employeeCode}
          htmlFor="employeeCode"
          required
          error={errors.employeeCode}
        >
          <Input
            id="employeeCode"
            value={form.employeeCode}
            onChange={(event) => updateField('employeeCode', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.employees.nationalCode}
          htmlFor="nationalCode"
          error={errors.nationalCode}
          hint={t.common.optional}
        >
          <Input
            id="nationalCode"
            value={form.nationalCode}
            onChange={(event) => updateField('nationalCode', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.employees.firstName}
          htmlFor="employeeFirstName"
          required
          error={errors.firstName}
        >
          <Input
            id="employeeFirstName"
            value={form.firstName}
            onChange={(event) => updateField('firstName', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField
          label={t.employees.lastName}
          htmlFor="employeeLastName"
          required
          error={errors.lastName}
        >
          <Input
            id="employeeLastName"
            value={form.lastName}
            onChange={(event) => updateField('lastName', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField
          label={t.employees.fatherName}
          htmlFor="employeeFatherName"
          error={errors.fatherName}
          hint={t.common.optional}
        >
          <Input
            id="employeeFatherName"
            value={form.fatherName}
            onChange={(event) => updateField('fatherName', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.employees.orgUnit} htmlFor="employeeOrgUnit" required error={errors.orgUnitId}>
          <Select
            id="employeeOrgUnit"
            value={form.orgUnitId}
            onChange={(event) => {
              updateField('orgUnitId', event.target.value);
              // موقعیت قبلی ممکن است به واحد جدید تعلق نداشته باشد.
              updateField('positionId', '');
            }}
            options={orgUnitOptions}
            placeholder={t.employees.selectOrgUnit}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.employees.position} htmlFor="employeePosition" error={errors.positionId}>
          <Select
            id="employeePosition"
            value={form.positionId}
            onChange={(event) => updateField('positionId', event.target.value)}
            options={positionOptions}
            placeholder={t.employees.selectPosition}
            disabled={isSaving || positionOptions.length === 0}
          />
        </FormField>

        <FormField label={t.employees.manager} htmlFor="employeeManager" error={errors.managerId}>
          <Select
            id="employeeManager"
            value={form.managerId}
            onChange={(event) => updateField('managerId', event.target.value)}
            options={managerOptions}
            placeholder={t.employees.selectManager}
            disabled={isSaving || managerOptions.length === 0}
          />
        </FormField>

        <FormField label={t.employees.status} htmlFor="employeeStatus">
          <Select
            id="employeeStatus"
            value={form.status}
            onChange={(event) => updateField('status', event.target.value)}
            options={statusOptions}
            disabled={isSaving}
          />
        </FormField>

        <FormField
          label={t.employees.startDate}
          htmlFor="employeeStartDate"
          required
          error={errors.startDate}
        >
          <Input
            id="employeeStartDate"
            type="date"
            value={form.startDate}
            onChange={(event) => updateField('startDate', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField
          label={t.employees.endDate}
          htmlFor="employeeEndDate"
          error={errors.endDate}
          hint={t.common.optional}
        >
          <Input
            id="employeeEndDate"
            type="date"
            value={form.endDate}
            onChange={(event) => updateField('endDate', event.target.value)}
            disabled={isSaving}
          />
        </FormField>

        <FormField label={t.employees.workEmail} htmlFor="employeeWorkEmail" error={errors.workEmail}>
          <Input
            id="employeeWorkEmail"
            type="email"
            value={form.workEmail}
            onChange={(event) => updateField('workEmail', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>

        <FormField
          label={t.employees.internalPhone}
          htmlFor="employeeInternalPhone"
          error={errors.internalPhone}
        >
          <Input
            id="employeeInternalPhone"
            value={form.internalPhone}
            onChange={(event) => updateField('internalPhone', event.target.value)}
            disabled={isSaving}
            dir="ltr"
          />
        </FormField>
      </form>
    </Dialog>
  );
}

interface EmployeeFormState {
  employeeCode: string;
  nationalCode: string;
  firstName: string;
  lastName: string;
  fatherName: string;
  orgUnitId: string;
  positionId: string;
  managerId: string;
  status: string;
  startDate: string;
  endDate: string;
  workEmail: string;
  internalPhone: string;
}

function createEmptyForm(): EmployeeFormState {
  return {
    employeeCode: '',
    nationalCode: '',
    firstName: '',
    lastName: '',
    fatherName: '',
    orgUnitId: '',
    positionId: '',
    managerId: '',
    status: String(EmployeeStatus.Active),
    startDate: new Date().toISOString().slice(0, 10),
    endDate: '',
    workEmail: '',
    internalPhone: ''
  };
}
