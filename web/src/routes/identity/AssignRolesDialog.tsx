import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { usersApi, type UserSummary } from '@/api/users';
import { ApiError } from '@/api/client';
import { useLanguage } from '@/i18n/LanguageProvider';
import { queryKeys, useRoles } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { Checkbox } from '@/components/ui/checkbox';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';

interface AssignRolesDialogProps {
  user: UserSummary;
  onClose: () => void;
}

/**
 * انتصاب نقش‌ها به یک کاربر (جایگزینی کامل).
 */
export function AssignRolesDialog({ user, onClose }: AssignRolesDialogProps) {
  const { t, culture } = useLanguage();
  const queryClient = useQueryClient();

  const { data: roles, isLoading } = useRoles();

  // UserSummary فقط نام نقش‌ها را دارد، اما انتصاب نیاز به شناسه دارد.
  // نگاشت نام → شناسه از فهرست نقش‌ها ساخته می‌شود.
  const roleNameToId = useMemo(() => {
    const map = new Map<string, string>();
    for (const role of roles ?? []) {
      map.set(role.name, role.id);
    }
    return map;
  }, [roles]);

  const [selectedRoleIds, setSelectedRoleIds] = useState<Set<string>>(
    () => new Set(user.roles.map((name) => roleNameToId.get(name)).filter((id): id is string => !!id))
  );
  const [formError, setFormError] = useState<string | undefined>();

  useEffect(() => {
    setSelectedRoleIds(
      new Set(user.roles.map((name) => roleNameToId.get(name)).filter((id): id is string => !!id))
    );
    setFormError(undefined);
  }, [user.id, user.roles, roleNameToId]);

  const mutation = useMutation({
    mutationFn: () =>
      usersApi.assignRoles(culture, {
        userId: user.id,
        roleIds: Array.from(selectedRoleIds)
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
      onClose();
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? t.errors.fromCode(error.code) : t.errors.generic);
    }
  });

  function toggleRole(roleId: string) {
    setSelectedRoleIds((previous) => {
      const next = new Set(previous);
      if (next.has(roleId)) {
        next.delete(roleId);
      } else {
        next.add(roleId);
      }
      return next;
    });
    setFormError(undefined);
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={t.users.assignRoles}
      description={`${user.displayName} (${user.userName})`}
    >
      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t.common.loading}</p>
      ) : !roles || roles.length === 0 ? (
        <div className="flex flex-col gap-3">
          <p className="text-sm text-muted-foreground">{t.roles.noRoles}</p>
          <Button onClick={onClose}>{t.common.close}</Button>
        </div>
      ) : (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            setFormError(undefined);
            mutation.mutate();
          }}
          className="flex flex-col gap-4"
        >
          {formError && (
            <div
              className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
              role="alert"
            >
              {formError}
            </div>
          )}

          <div className="flex flex-col gap-2">
            {roles.map((role) => (
              <label
                key={role.id}
                className="flex cursor-pointer items-center gap-3 rounded-md border p-3 transition-colors hover:bg-accent"
              >
                <Checkbox
                  checked={selectedRoleIds.has(role.id)}
                  onCheckedChange={() => toggleRole(role.id)}
                  disabled={mutation.isPending}
                />

                <span className="flex flex-1 flex-col">
                  <span className="flex items-center gap-2 text-sm font-medium">
                    {role.displayName ?? role.name}
                    {role.isSystem && (
                      <Badge variant="warning" className="text-xs">
                        {t.roles.system}
                      </Badge>
                    )}
                  </span>

                  {role.description && (
                    <span className="text-xs text-muted-foreground">{role.description}</span>
                  )}
                </span>

                <span className="text-xs text-muted-foreground" dir="ltr">
                  {role.permissions.length} {t.roles.permissions}
                </span>
              </label>
            ))}
          </div>

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
