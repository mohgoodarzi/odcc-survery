import { apiPost, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';

export interface RoleSummary {
  id: string;
  name: string;
  displayName: string | null;
  description: string | null;
  isSystem: boolean;
  permissions: string[];
}

export interface SaveRoleRequest {
  name: string;
  displayName?: string | null;
  description?: string | null;
  permissions?: string[];
}

export interface PermissionGroup {
  groupKey: string;
  displayName: string;
  permissions: string[];
}

export const rolesApi = {
  list: (culture: Culture, signal?: AbortSignal) =>
    apiRequest<RoleSummary[]>(culture, '/identity/roles', { signal }),

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<RoleSummary>(culture, `/identity/roles/${id}`, { signal }),

  create: (culture: Culture, request: SaveRoleRequest) =>
    apiRequest<RoleSummary>(culture, '/identity/roles', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SaveRoleRequest) =>
    apiPut<RoleSummary>(culture, `/identity/roles/${id}`, request),

  assignPermissions: (culture: Culture, roleId: string, permissions: string[]) =>
    apiPost<void>(culture, `/identity/roles/${roleId}/permissions`, { roleId, permissions }),

  /** کاتالوگ تمام مجوزهای سامانه به‌صورت گروهی (برای رابط انتخاب مجوزها). */
  permissionCatalog: (culture: Culture, signal?: AbortSignal) =>
    apiRequest<PermissionGroup[]>(culture, '/identity/roles/permissions/catalog', { signal })
};
