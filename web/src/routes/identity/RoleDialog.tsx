import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { rolesApi, type SaveRoleRequest } from '@/api/roles';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { queryKeys, usePermissionCatalog, useRoles } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';

interface RoleDialogProps {
  open: boolean;
  onClose: () => void;
  roleId?: string | null;
}

/**
 * دیالوگ ایجاد/ویرایش نقش با انتخاب مجوزها از کاتالوگ گروهی.
 */
export function RoleDialog({ open, onClose, roleId }: RoleDialogProps) {
  const { t, culture } = useLanguage();
  const queryClient = useQueryClient();
  const isEdit = !!roleId;

  const { data: catalog } = usePermissionCatalog();
  const { data: roles } = useRoles();

  const existingRole = useMemo(
    () => (roleId ? roles?.find((role) => role.id === roleId) ?? null : null),
    [roleId, roles]
  );

  const [name, setName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedPermissions, setSelectedPermissions] = useState<Set<string>>(new Set());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    if (!open) return;

    setName(existingRole?.name ?? '');
    setDisplayName(existingRole?.displayName ?? '');
    setDescription(existingRole?.description ?? '');
    setSelectedPermissions(new Set(existingRole?.permissions ?? []));
    setErrors({});
    setFormError(undefined);
  }, [open, existingRole]);

  const mutation = useMutation({
    mutationFn: () => {
      const request: SaveRoleRequest = {
        name: name.trim(),
        displayName: displayName.trim() || null,
        description: description.trim() || null,
        permissions: Array.from(selectedPermissions)
      };

      if (isEdit && roleId) {
        return rolesApi.update(culture, roleId, request);
      }

      return rolesApi.create(culture, request);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.roles });
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

  function togglePermission(permission: string) {
    setSelectedPermissions((previous) => {
      const next = new Set(previous);
      if (next.has(permission)) {
        next.delete(permission);
      } else {
        next.add(permission);
      }
      return next;
    });
  }

  function toggleGroup(groupPermissions: string[]) {
    setSelectedPermissions((previous) => {
      const next = new Set(previous);
      const allSelected = groupPermissions.every((permission) => next.has(permission));

      for (const permission of groupPermissions) {
        if (allSelected) {
          next.delete(permission);
        } else {
          next.add(permission);
        }
      }

      return next;
    });
  }

  function validate(): boolean {
    const next: Record<string, string> = {};

    if (!name.trim()) {
      next.name = t.roles.roleName + ' ' + t.common.required;
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;

    setFormError(undefined);
    mutation.mutate();
  }

  const isSystemRole = existingRole?.isSystem ?? false;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? t.roles.editRole : t.roles.newRole}
      description={isSystemRole ? t.roles.isSystem : t.roles.description}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="role-form" disabled={mutation.isPending}>
            {mutation.isPending ? t.common.saving : t.common.save}
          </Button>
        </>
      }
    >
      <form id="role-form" onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
        {formError && (
          <div
            className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
            role="alert"
          >
            {formError}
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label={t.roles.roleName} htmlFor="roleName" required error={errors.name}>
            <Input
              id="roleName"
              value={name}
              onChange={(event) => setName(event.target.value)}
              disabled={mutation.isPending || isSystemRole}
              dir="ltr"
            />
          </FormField>

          <FormField label={t.roles.displayName} htmlFor="displayName" error={errors.displayName}>
            <Input
              id="displayName"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              disabled={mutation.isPending}
            />
          </FormField>
        </div>

        <FormField label={t.roles.descriptionLabel} htmlFor="roleDescription" error={errors.description}>
          <Input
            id="roleDescription"
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            disabled={mutation.isPending}
          />
        </FormField>

        <div className="flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium">{t.roles.selectPermissions}</span>
            <Badge variant="outline">{selectedPermissions.size}</Badge>
          </div>

          {catalog?.map((group) => {
            const allSelected =
              group.permissions.length > 0 &&
              group.permissions.every((permission) => selectedPermissions.has(permission));

            return (
              <div key={group.groupKey} className="rounded-md border p-3">
                <label className="flex cursor-pointer items-center gap-3">
                  <Checkbox
                    checked={allSelected}
                    onCheckedChange={() => toggleGroup(group.permissions)}
                    disabled={mutation.isPending}
                  />
                  <span className="text-sm font-medium">{group.displayName}</span>
                </label>

                <div className="mt-3 grid gap-2 ps-7 sm:grid-cols-2">
                  {group.permissions.map((permission) => (
                    <label
                      key={permission}
                      className="flex cursor-pointer items-center gap-2 rounded-sm p-1.5 transition-colors hover:bg-accent"
                    >
                      <Checkbox
                        checked={selectedPermissions.has(permission)}
                        onCheckedChange={() => togglePermission(permission)}
                        disabled={mutation.isPending}
                      />
                      <span className="font-mono text-xs text-muted-foreground" dir="ltr">
                        {permission}
                      </span>
                    </label>
                  ))}
                </div>
              </div>
            );
          })}

          {!catalog && (
            <p className="text-sm text-muted-foreground">{t.common.loading}</p>
          )}
        </div>
      </form>
    </Dialog>
  );
}
