import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  systemConfigurationApi,
  type CreateFeatureFlagRequest,
  type CreateSettingRequest,
  type CreateSystemPolicyRequest,
  type FeatureFlagSearchRequest,
  type SettingSearchRequest,
  type SystemPolicySearchRequest,
  type UpdateFeatureFlagRequest,
  type UpdateSettingRequest,
  type UpdateSystemPolicyRequest
} from '@/api/systemConfiguration';
import { useLanguage } from '@/i18n/LanguageProvider';

/**
 * کلیدهای کوئری ماژول پیکربندی سامانه تا invalidation بین صفحات یکپارچه باشد.
 */
export const systemConfigurationQueryKeys = {
  settingSearch: (request: SettingSearchRequest) => ['system', 'settings', 'search', request] as const,
  settings: ['system', 'settings'] as const,
  featureFlagSearch: (request: FeatureFlagSearchRequest) =>
    ['system', 'feature-flags', 'search', request] as const,
  featureFlags: ['system', 'feature-flags'] as const,
  policySearch: (request: SystemPolicySearchRequest) => ['system', 'policies', 'search', request] as const,
  policies: ['system', 'policies'] as const,
  stats: ['system', 'stats'] as const
};

function useInvalidateSystemConfiguration() {
  const queryClient = useQueryClient();

  // invalidation با پیشوند «system» هم تنظیمات، هم پرچم‌ها، هم سیاست‌ها و هم آمار را پوشش می‌دهد.
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['system'] });
  };
}

// --- آمار ---------------------------------------------------------------------------

export function useSystemConfigurationStats() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: systemConfigurationQueryKeys.stats,
    queryFn: ({ signal }) => systemConfigurationApi.getStats(culture, signal)
  });
}

// --- تنظیمات -------------------------------------------------------------------------

export function useSettingSearch(request: SettingSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: systemConfigurationQueryKeys.settingSearch(request),
    queryFn: ({ signal }) => systemConfigurationApi.searchSettings(culture, request, signal)
  });
}

export function useCreateSetting() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSystemConfiguration();

  return useMutation({
    mutationFn: (request: CreateSettingRequest) => systemConfigurationApi.createSetting(culture, request),
    onSuccess: () => invalidate()
  });
}

export function useUpdateSetting() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSystemConfiguration();

  return useMutation({
    mutationFn: ({ key, request }: { key: string; request: UpdateSettingRequest }) =>
      systemConfigurationApi.updateSetting(culture, key, request),
    onSuccess: () => invalidate()
  });
}

// --- پرچم‌های ویژگی -------------------------------------------------------------------

export function useFeatureFlagSearch(request: FeatureFlagSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: systemConfigurationQueryKeys.featureFlagSearch(request),
    queryFn: ({ signal }) => systemConfigurationApi.searchFeatureFlags(culture, request, signal)
  });
}

export function useTurnFeatureOn() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSystemConfiguration();

  return useMutation({
    mutationFn: (key: string) => systemConfigurationApi.turnFeatureOn(culture, key),
    onSuccess: () => invalidate()
  });
}

export function useTurnFeatureOff() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSystemConfiguration();

  return useMutation({
    mutationFn: (key: string) => systemConfigurationApi.turnFeatureOff(culture, key),
    onSuccess: () => invalidate()
  });
}

export function useCreateFeatureFlag() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSystemConfiguration();

  return useMutation({
    mutationFn: (request: CreateFeatureFlagRequest) =>
      systemConfigurationApi.createFeatureFlag(culture, request),
    onSuccess: () => invalidate()
  });
}

export function useUpdateFeatureFlag() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSystemConfiguration();

  return useMutation({
    mutationFn: ({ key, request }: { key: string; request: UpdateFeatureFlagRequest }) =>
      systemConfigurationApi.updateFeatureFlag(culture, key, request),
    onSuccess: () => invalidate()
  });
}

// --- سیاست‌های سیستمی -----------------------------------------------------------------

export function useSystemPolicySearch(request: SystemPolicySearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: systemConfigurationQueryKeys.policySearch(request),
    queryFn: ({ signal }) => systemConfigurationApi.searchPolicies(culture, request, signal)
  });
}

export function useCreateSystemPolicy() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSystemConfiguration();

  return useMutation({
    mutationFn: (request: CreateSystemPolicyRequest) =>
      systemConfigurationApi.createPolicy(culture, request),
    onSuccess: () => invalidate()
  });
}

export function useUpdateSystemPolicy() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSystemConfiguration();

  return useMutation({
    mutationFn: ({
      type,
      key,
      request
    }: {
      type: import('@/api/systemConfiguration').SystemPolicyType;
      key: string;
      request: UpdateSystemPolicyRequest;
    }) => systemConfigurationApi.updatePolicy(culture, type, key, request),
    onSuccess: () => invalidate()
  });
}
