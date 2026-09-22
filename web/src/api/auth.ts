import { apiPost, apiRequest } from './client';
import type { Culture } from '@/i18n/types';

/** پروفایل کاربر احراز هویت‌شده (دریافتی از /identity/users/me یا پس از ورود). */
export interface UserProfile {
  id: string;
  userName: string;
  email: string | null;
  phoneNumber: string | null;
  firstName: string;
  lastName: string;
  displayName: string;
  avatarUrl: string | null;
  isActive: boolean;
  emailConfirmed: boolean;
  twoFactorEnabled: boolean;
  orgUnitId: string | null;
  orgUnitName: string | null;
  dataScope: number;
  roles: string[];
  permissions: string[];
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresAt: string;
  profile?: UserProfile;
}

export interface LoginResult {
  requiresMfa: boolean;
  mfaChallengeToken: string | null;
  tokens: TokenResponse | null;
}

export interface LoginRequest {
  userName: string;
  password: string;
  deviceInfo?: string | null;
}

export interface RefreshRequest {
  accessToken: string;
  refreshToken: string;
}

export interface ValidateTokenResult {
  isValid: boolean;
  userId: string | null;
  userName: string | null;
  roles: string[];
  permissions: string[];
}

export const authApi = {
  login: (culture: Culture, request: LoginRequest) =>
    apiPost<LoginResult>(culture, '/auth/login', request, { anonymous: true, skipRefresh: true }),

  refresh: (culture: Culture, request: RefreshRequest) =>
    apiPost<TokenResponse>(culture, '/auth/refresh', request, {
      anonymous: true,
      skipRefresh: true
    }),

  logout: (culture: Culture, request: RefreshRequest) =>
    apiPost<void>(culture, '/auth/logout', request, { skipRefresh: true }),

  validate: (culture: Culture, accessToken: string) =>
    apiPost<ValidateTokenResult>(culture, '/auth/validate', { accessToken }, { skipRefresh: true }),

  /** پروفایل کاربر جاری. */
  me: (culture: Culture, signal?: AbortSignal) =>
    apiRequest<UserProfile>(culture, '/identity/users/me', { signal })
};
