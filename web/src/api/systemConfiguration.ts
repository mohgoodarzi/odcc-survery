import { apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import type { PagedResult } from './users';

/**
 * قرارداد API ماژول پیکربندی سامانه: تنظیمات، پرچم‌های ویژگی و سیاست‌های سیستمی.
 *
 * **امنیت:** مقادیر تنظیمات «حساس» هرگز از API خارج نمی‌شوند — فقط پرچم
 * «دارای مقدار». تغییر آن‌ها باید از طریق ابزارهای امن انجام شود.
 */

export enum SettingValueType {
  Text = 0,
  WholeNumber = 1,
  FractionalNumber = 2,
  TrueFalse = 3,
  Date = 4,
  Email = 5,
  Url = 6,
  Duration = 7,
  Json = 8
}

export enum ConfigurationScope {
  System = 0,
  Organization = 1
}

export enum FeatureFlagState {
  Off = 0,
  On = 1,
  Percentage = 2,
  AllowList = 3
}

export enum SystemPolicyType {
  Password = 0,
  Session = 1,
  ResponsePrivacy = 2,
  DataRetention = 3,
  LoginSecurity = 4,
  Custom = 99
}

// --- تنظیمات: درخواست‌ها -------------------------------------------------------------

export interface SettingSearchRequest {
  searchText?: string | null;
  group?: string | null;
  valueType?: SettingValueType | null;
  scope?: ConfigurationScope | null;
  isSensitive?: boolean | null;
  page?: number;
  pageSize?: number;
}

export interface UpdateSettingRequest {
  value: string;
}

export interface CreateSettingRequest {
  key: string;
  name: string;
  description?: string | null;
  valueType: SettingValueType;
  value: string;
  defaultValue?: string | null;
  group?: string | null;
  isSensitive: boolean;
}

// --- پرچم‌های ویژگی: درخواست‌ها -------------------------------------------------------

export interface FeatureFlagSearchRequest {
  searchText?: string | null;
  state?: FeatureFlagState | null;
  scope?: ConfigurationScope | null;
  page?: number;
  pageSize?: number;
}

export interface UpdateFeatureFlagRequest {
  state: FeatureFlagState;
  /** درصد (۰ تا ۱۰۰) — فقط در حالت Percentage. */
  percentage?: number | null;
  /** کلیدهای کاربران مجاز — فقط در حالت AllowList. */
  allowedUserIds?: string[] | null;
  /** نقش‌های مجاز — فقط در حالت AllowList. */
  allowedRoles?: string[] | null;
  expiresAt?: string | null;
}

export interface CreateFeatureFlagRequest {
  key: string;
  name: string;
  description?: string | null;
  state: FeatureFlagState;
  percentage?: number | null;
  allowedUserIds?: string[] | null;
  allowedRoles?: string[] | null;
  expiresAt?: string | null;
}

// --- سیاست‌های سیستمی: درخواست‌ها ------------------------------------------------------

export interface SystemPolicySearchRequest {
  type?: SystemPolicyType | null;
  searchText?: string | null;
  isEnabled?: boolean | null;
  page?: number;
  pageSize?: number;
}

export interface UpdateSystemPolicyRequest {
  value: string;
  isEnabled: boolean;
}

export interface CreateSystemPolicyRequest {
  type: SystemPolicyType;
  key: string;
  name: string;
  description?: string | null;
  value: string;
  defaultValue?: string | null;
  isEnabled: boolean;
}

// --- خروجی‌ها -----------------------------------------------------------------------

export interface SettingDto {
  id: string;
  key: string;
  name: string;
  description?: string | null;
  valueType: SettingValueType;
  scope: ConfigurationScope;
  group?: string | null;
  isReadOnly: boolean;
  isSensitive: boolean;
  orgUnitId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  lastModifiedByUserId?: string | null;
  lastModifiedByUserName?: string | null;
  /** برای تنظیمات حساس، مقدار واقعی هرگز برگردانده نمی‌شود (null). */
  value?: string | null;
  hasValue: boolean;
  defaultValue?: string | null;
}

export interface FeatureFlagDto {
  id: string;
  key: string;
  name: string;
  description?: string | null;
  state: FeatureFlagState;
  percentage: number;
  allowedUserIds: string[];
  allowedRoles: string[];
  scope: ConfigurationScope;
  orgUnitId?: string | null;
  expiresAt?: string | null;
  isEnabled: boolean;
  createdAt: string;
  updatedAt?: string | null;
  lastModifiedByUserId?: string | null;
  lastModifiedByUserName?: string | null;
}

export interface SystemPolicyDto {
  id: string;
  type: SystemPolicyType;
  key: string;
  name: string;
  description?: string | null;
  value: string;
  defaultValue?: string | null;
  isEnabled: boolean;
  createdAt: string;
  updatedAt?: string | null;
  lastModifiedByUserId?: string | null;
  lastModifiedByUserName?: string | null;
}

export interface SystemConfigurationStatsDto {
  totalSettings: number;
  sensitiveSettings: number;
  totalFeatureFlags: number;
  enabledFeatureFlags: number;
  totalPolicies: number;
  enabledPolicies: number;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

function buildSettingParams(request: SettingSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.group) params.group = request.group;
  if (request.valueType !== null && request.valueType !== undefined) params.valueType = String(request.valueType);
  if (request.scope !== null && request.scope !== undefined) params.scope = String(request.scope);
  if (request.isSensitive !== null && request.isSensitive !== undefined) params.isSensitive = String(request.isSensitive);
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 50);
  return params;
}

function buildFeatureFlagParams(request: FeatureFlagSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.state !== null && request.state !== undefined) params.state = String(request.state);
  if (request.scope !== null && request.scope !== undefined) params.scope = String(request.scope);
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 50);
  return params;
}

function buildPolicyParams(request: SystemPolicySearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.type !== null && request.type !== undefined) params.type = String(request.type);
  if (request.searchText) params.searchText = request.searchText;
  if (request.isEnabled !== null && request.isEnabled !== undefined) params.isEnabled = String(request.isEnabled);
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 50);
  return params;
}

export const systemConfigurationApi = {
  // --- تنظیمات ------------------------------------------------------------------

  searchSettings: (culture: Culture, request: SettingSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<SettingDto>>(
      culture,
      appendSearchParams('/system/settings', buildSettingParams(request)),
      { signal }
    ),

  getSetting: (culture: Culture, key: string, signal?: AbortSignal) =>
    apiRequest<SettingDto>(culture, `/system/settings/${encodeURIComponent(key)}`, { signal }),

  createSetting: (culture: Culture, request: CreateSettingRequest) =>
    apiRequest<SettingDto>(culture, '/system/settings', { method: 'POST', body: request }),

  updateSetting: (culture: Culture, key: string, request: UpdateSettingRequest) =>
    apiPut<SettingDto>(culture, `/system/settings/${encodeURIComponent(key)}`, request),

  // --- پرچم‌های ویژگی -------------------------------------------------------------

  searchFeatureFlags: (culture: Culture, request: FeatureFlagSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<FeatureFlagDto>>(
      culture,
      appendSearchParams('/system/feature-flags', buildFeatureFlagParams(request)),
      { signal }
    ),

  getFeatureFlag: (culture: Culture, key: string, signal?: AbortSignal) =>
    apiRequest<FeatureFlagDto>(culture, `/system/feature-flags/${encodeURIComponent(key)}`, { signal }),

  isFeatureEnabled: (culture: Culture, key: string, signal?: AbortSignal) =>
    apiRequest<boolean>(culture, `/system/feature-flags/${encodeURIComponent(key)}/enabled`, { signal }),

  turnFeatureOn: (culture: Culture, key: string) =>
    apiRequest<FeatureFlagDto>(culture, `/system/feature-flags/${encodeURIComponent(key)}/turn-on`, {
      method: 'POST'
    }),

  turnFeatureOff: (culture: Culture, key: string) =>
    apiRequest<FeatureFlagDto>(culture, `/system/feature-flags/${encodeURIComponent(key)}/turn-off`, {
      method: 'POST'
    }),

  createFeatureFlag: (culture: Culture, request: CreateFeatureFlagRequest) =>
    apiRequest<FeatureFlagDto>(culture, '/system/feature-flags', { method: 'POST', body: request }),

  updateFeatureFlag: (culture: Culture, key: string, request: UpdateFeatureFlagRequest) =>
    apiPut<FeatureFlagDto>(culture, `/system/feature-flags/${encodeURIComponent(key)}`, request),

  // --- سیاست‌های سیستمی ------------------------------------------------------------

  searchPolicies: (culture: Culture, request: SystemPolicySearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<SystemPolicyDto>>(
      culture,
      appendSearchParams('/system/policies', buildPolicyParams(request)),
      { signal }
    ),

  createPolicy: (culture: Culture, request: CreateSystemPolicyRequest) =>
    apiRequest<SystemPolicyDto>(culture, '/system/policies', { method: 'POST', body: request }),

  updatePolicy: (
    culture: Culture,
    type: SystemPolicyType,
    key: string,
    request: UpdateSystemPolicyRequest
  ) =>
    apiPut<SystemPolicyDto>(
      culture,
      `/system/policies/${type}/${encodeURIComponent(key)}`,
      request
    ),

  // --- آمار ------------------------------------------------------------------------

  getStats: (culture: Culture, signal?: AbortSignal) =>
    apiRequest<SystemConfigurationStatsDto>(culture, '/system/configuration/stats', { signal })
};
