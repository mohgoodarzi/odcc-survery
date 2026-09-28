import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  actionsApi,
  type ActionItemSearchRequest,
  type ActionPlanSearchRequest,
  type AddActionCommentRequest,
  type AssessEffectivenessRequest,
  type RecordOutcomeRequest,
  type SaveActionItemRequest,
  type SaveActionPlanRequest,
  type TransitionActionItemRequest
} from '@/api/actions';
import { useLanguage } from '@/i18n/LanguageProvider';

/**
 * کلیدهای کوئری ماژول اقدامات تا invalidation بین صفحات و دیالوگ‌ها یکپارچه باشد.
 */
export const actionsQueryKeys = {
  planSearch: (request: ActionPlanSearchRequest) =>
    ['actions', 'plans', 'search', request] as const,
  plans: ['actions', 'plans'] as const,
  plan: (id: string | null) => ['actions', 'plans', 'detail', id] as const,
  planItems: (planId: string | null) => ['actions', 'plans', 'items', planId] as const,
  itemSearch: (request: ActionItemSearchRequest) =>
    ['actions', 'items', 'search', request] as const,
  items: ['actions', 'items'] as const,
  item: (id: string | null) => ['actions', 'items', 'detail', id] as const,
  comments: (itemId: string | null) => ['actions', 'comments', itemId] as const,
  evidence: (itemId: string | null) => ['actions', 'evidence', itemId] as const,
  stats: ['actions', 'stats'] as const
};

function useInvalidateActions() {
  const queryClient = useQueryClient();

  // invalidation با پیشوند «actions» هم برنامه‌ها، هم آیتم‌ها و هم آمار را پوشش می‌دهد.
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['actions'] });
  };
}

// --- برنامه‌ها -----------------------------------------------------------------

export function useActionPlanSearch(request: ActionPlanSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: actionsQueryKeys.planSearch(request),
    queryFn: ({ signal }) => actionsApi.searchPlans(culture, request, signal)
  });
}

export function useActionPlan(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: actionsQueryKeys.plan(id),
    queryFn: ({ signal }) => actionsApi.getPlan(culture, id as string, signal),
    enabled: !!id
  });
}

export function useActionStats() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: actionsQueryKeys.stats,
    queryFn: ({ signal }) => actionsApi.getStats(culture, signal)
  });
}

export function useCreateActionPlan() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: (request: SaveActionPlanRequest) => actionsApi.createPlan(culture, request),
    onSuccess: () => invalidate()
  });
}

export function useUpdateActionPlan() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveActionPlanRequest }) =>
      actionsApi.updatePlan(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useActivateActionPlan() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: (id: string) => actionsApi.activatePlan(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useCompleteActionPlan() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: (id: string) => actionsApi.completePlan(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useCancelActionPlan() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: (id: string) => actionsApi.cancelPlan(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useArchiveActionPlan() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: (id: string) => actionsApi.archivePlan(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useRecordPlanOutcome() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: RecordOutcomeRequest }) =>
      actionsApi.recordOutcome(culture, id, request),
    onSuccess: () => invalidate()
  });
}

// --- آیتم‌ها ---------------------------------------------------------------------

export function useActionItemSearch(request: ActionItemSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: actionsQueryKeys.itemSearch(request),
    queryFn: ({ signal }) => actionsApi.searchItems(culture, request, signal)
  });
}

export function useActionItem(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: actionsQueryKeys.item(id),
    queryFn: ({ signal }) => actionsApi.getItem(culture, id as string, signal),
    enabled: !!id
  });
}

export function useCreateActionItem() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: ({ planId, request }: { planId: string; request: SaveActionItemRequest }) =>
      actionsApi.createItem(culture, planId, request),
    onSuccess: () => invalidate()
  });
}

export function useUpdateActionItem() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveActionItemRequest }) =>
      actionsApi.updateItem(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useTransitionActionItem() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: TransitionActionItemRequest }) =>
      actionsApi.transitionItem(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useAssessActionItemEffectiveness() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: AssessEffectivenessRequest }) =>
      actionsApi.assessEffectiveness(culture, id, request),
    onSuccess: () => invalidate()
  });
}

// --- دیدگاه‌ها -------------------------------------------------------------------

export function useActionComments(itemId: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: actionsQueryKeys.comments(itemId),
    queryFn: ({ signal }) => actionsApi.listComments(culture, itemId as string, signal),
    enabled: !!itemId
  });
}

export function useAddActionComment() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ itemId, request }: { itemId: string; request: AddActionCommentRequest }) =>
      actionsApi.addComment(culture, itemId, request),
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({
        queryKey: actionsQueryKeys.comments(variables.itemId)
      });
    }
  });
}

// --- پیوست‌ها ---------------------------------------------------------------------

export function useActionEvidence(itemId: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: actionsQueryKeys.evidence(itemId),
    queryFn: ({ signal }) => actionsApi.listEvidence(culture, itemId as string, signal),
    enabled: !!itemId
  });
}

export function useUploadActionEvidence() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ itemId, file }: { itemId: string; file: File }) =>
      actionsApi.uploadEvidence(culture, itemId, file),
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({
        queryKey: actionsQueryKeys.evidence(variables.itemId)
      });
    }
  });
}

export function useDeleteActionEvidence() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateActions();

  return useMutation({
    mutationFn: (evidenceId: string) => actionsApi.deleteEvidence(culture, evidenceId),
    onSuccess: () => invalidate()
  });
}

/**
 * دانلود محتوای یک پیوست و باز کردن آن در مرورگر.
 * blob URL ساخته‌شده پس از شروع دانلود آزاد می‌شود.
 */
export function useDownloadActionEvidence() {
  const { culture } = useLanguage();

  return useMutation({
    mutationFn: async (evidence: { id: string; fileName: string }) => {
      const blob = await actionsApi.downloadEvidence(culture, evidence.id);
      const url = URL.createObjectURL(blob);

      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = evidence.fileName;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);

      // آزادسازی منبع پس از شروع دانلود.
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
  });
}
