import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  integrationsApi,
  type IntegrationEndpointSearchRequest,
  type SaveIntegrationEndpointRequest,
  type TestIntegrationEndpointRequest,
  type WebhookDeliverySearchRequest
} from '@/api/integrations';
import { useLanguage } from '@/i18n/LanguageProvider';

/**
 * کلیدهای کوئری ماژول یکپارچه‌سازی تا invalidation بین صفحات یکپارچه باشد.
 */
export const integrationsQueryKeys = {
  endpointSearch: (request: IntegrationEndpointSearchRequest) =>
    ['integrations', 'endpoints', 'search', request] as const,
  endpoints: ['integrations', 'endpoints'] as const,
  endpoint: (id: string | null) => ['integrations', 'endpoints', 'detail', id] as const,
  integrationStats: ['integrations', 'stats'] as const,
  deliverySearch: (request: WebhookDeliverySearchRequest) =>
    ['integrations', 'deliveries', 'search', request] as const,
  deliveries: ['integrations', 'deliveries'] as const,
  delivery: (id: string | null) => ['integrations', 'deliveries', 'detail', id] as const
};

function useInvalidateIntegrations() {
  const queryClient = useQueryClient();

  // invalidation با پیشوند «integrations» هم اندپوینت‌ها، هم تحویل‌ها و هم آمار را پوشش می‌دهد.
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['integrations'] });
  };
}

// --- اندپوینت‌ها -------------------------------------------------------------------

export function useIntegrationEndpointSearch(request: IntegrationEndpointSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: integrationsQueryKeys.endpointSearch(request),
    queryFn: ({ signal }) => integrationsApi.searchEndpoints(culture, request, signal)
  });
}

export function useIntegrationStats() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: integrationsQueryKeys.integrationStats,
    queryFn: ({ signal }) => integrationsApi.getStats(culture, signal)
  });
}

export function useCreateIntegrationEndpoint() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateIntegrations();

  return useMutation({
    mutationFn: (request: SaveIntegrationEndpointRequest) =>
      integrationsApi.createEndpoint(culture, request),
    onSuccess: () => invalidate()
  });
}

export function useUpdateIntegrationEndpoint() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateIntegrations();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveIntegrationEndpointRequest }) =>
      integrationsApi.updateEndpoint(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useActivateIntegrationEndpoint() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateIntegrations();

  return useMutation({
    mutationFn: (id: string) => integrationsApi.activateEndpoint(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useDeactivateIntegrationEndpoint() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateIntegrations();

  return useMutation({
    mutationFn: (id: string) => integrationsApi.deactivateEndpoint(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useArchiveIntegrationEndpoint() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateIntegrations();

  return useMutation({
    mutationFn: (id: string) => integrationsApi.archiveEndpoint(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useTestIntegrationEndpoint() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateIntegrations();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: TestIntegrationEndpointRequest }) =>
      integrationsApi.testEndpoint(culture, id, request),
    onSuccess: () => invalidate()
  });
}

// --- تحویل‌های وب‌هوک ---------------------------------------------------------------

export function useWebhookDeliverySearch(request: WebhookDeliverySearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: integrationsQueryKeys.deliverySearch(request),
    queryFn: ({ signal }) => integrationsApi.searchDeliveries(culture, request, signal)
  });
}

export function useRetryWebhookDelivery() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateIntegrations();

  return useMutation({
    mutationFn: (id: string) => integrationsApi.retryDelivery(culture, id),
    onSuccess: () => invalidate()
  });
}
