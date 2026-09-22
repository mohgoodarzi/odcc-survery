import { apiPost, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';

export interface UserSummary {
  id: string;
  userName: string;
  email: string | null;
  firstName: string;
  lastName: string;
  displayName: string;
  isActive: boolean;
  emailConfirmed: boolean;
  orgUnitId: string | null;
  orgUnitName: string | null;
  createdAt: string;
  roles: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UserSearchRequest {
  searchText?: string | null;
  isActive?: boolean | null;
  roleId?: string | null;
  page?: number;
  pageSize?: number;
}

export interface CreateUserRequest {
  userName: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
  nationalCode?: string | null;
  orgUnitId?: string | null;
  dataScope?: number;
  roleIds?: string[];
  isActive?: boolean;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  email?: string | null;
  phoneNumber?: string | null;
  orgUnitId?: string | null;
  dataScope?: number;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ResetPasswordRequest {
  userId: string;
  newPassword: string;
}

export interface SetUserActiveRequest {
  userId: string;
  isActive: boolean;
}

export interface AssignRolesRequest {
  userId: string;
  roleIds: string[];
}

function toSearchParams(request: UserSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.isActive !== null && request.isActive !== undefined) {
    params.isActive = String(request.isActive);
  }
  if (request.roleId) params.roleId = request.roleId;
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 20);
  return params;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

export const usersApi = {
  search: (culture: Culture, request: UserSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<UserSummary>>(
      culture,
      appendSearchParams('/identity/users', toSearchParams(request)),
      { signal }
    ),

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<UserSummary>(culture, `/identity/users/${id}`, { signal }),

  create: (culture: Culture, request: CreateUserRequest) =>
    apiRequest<UserSummary>(culture, '/identity/users', {
      method: 'POST',
      body: request
    }),

  update: (culture: Culture, id: string, request: UpdateUserRequest) =>
    apiPut<UserSummary>(culture, `/identity/users/${id}`, request),

  setActive: (culture: Culture, request: SetUserActiveRequest) =>
    apiPost<void>(culture, '/identity/users/activate', request),

  resetPassword: (culture: Culture, request: ResetPasswordRequest) =>
    apiPost<void>(culture, '/identity/users/reset-password', request),

  changePassword: (culture: Culture, request: ChangePasswordRequest) =>
    apiPost<void>(culture, '/identity/users/change-password', request),

  assignRoles: (culture: Culture, request: AssignRolesRequest) =>
    apiPost<void>(culture, '/identity/users/assign-roles', request)
};
