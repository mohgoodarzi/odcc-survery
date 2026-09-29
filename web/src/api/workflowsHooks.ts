import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  workflowsApi,
  type DecideWorkflowApprovalRequest,
  type SaveWorkflowRequest,
  type StartWorkflowInstanceRequest,
  type TransitionWorkflowInstanceRequest,
  type WorkflowApprovalSearchRequest,
  type WorkflowInstanceSearchRequest,
  type WorkflowSearchRequest
} from '@/api/workflows';
import { useLanguage } from '@/i18n/LanguageProvider';

/**
 * کلیدهای کوئری ماژول گردش کار تا invalidation بین صفحات یکپارچه باشد.
 */
export const workflowsQueryKeys = {
  workflowSearch: (request: WorkflowSearchRequest) => ['workflows', 'search', request] as const,
  workflows: ['workflows'] as const,
  workflow: (id: string | null) => ['workflows', 'detail', id] as const,
  workflowStats: ['workflows', 'stats'] as const,
  instanceSearch: (request: WorkflowInstanceSearchRequest) =>
    ['workflow-instances', 'search', request] as const,
  instances: ['workflow-instances'] as const,
  instance: (id: string | null) => ['workflow-instances', 'detail', id] as const,
  approvalSearch: (request: WorkflowApprovalSearchRequest) =>
    ['workflow-approvals', 'search', request] as const,
  approvals: ['workflow-approvals'] as const
};

function useInvalidateWorkflows() {
  const queryClient = useQueryClient();

  // invalidation با پیشوندهای ماژول، تعاریف، نمونه‌ها و تأییدها را پوشش می‌دهد.
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['workflows'] });
    void queryClient.invalidateQueries({ queryKey: ['workflow-instances'] });
    void queryClient.invalidateQueries({ queryKey: ['workflow-approvals'] });
  };
}

// --- تعاریف گردش کار -------------------------------------------------------------

export function useWorkflowSearch(request: WorkflowSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: workflowsQueryKeys.workflowSearch(request),
    queryFn: ({ signal }) => workflowsApi.search(culture, request, signal)
  });
}

export function useWorkflow(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: workflowsQueryKeys.workflow(id),
    queryFn: ({ signal }) => workflowsApi.getById(culture, id as string, signal),
    enabled: !!id
  });
}

export function useWorkflowStats() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: workflowsQueryKeys.workflowStats,
    queryFn: ({ signal }) => workflowsApi.getStats(culture, signal)
  });
}

export function useCreateWorkflow() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateWorkflows();

  return useMutation({
    mutationFn: (request: SaveWorkflowRequest) => workflowsApi.create(culture, request),
    onSuccess: () => invalidate()
  });
}

export function useUpdateWorkflow() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateWorkflows();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveWorkflowRequest }) =>
      workflowsApi.update(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useActivateWorkflow() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateWorkflows();

  return useMutation({
    mutationFn: (id: string) => workflowsApi.activate(culture, id),
    onSuccess: () => invalidate()
  });
}

export function useArchiveWorkflow() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateWorkflows();

  return useMutation({
    mutationFn: (id: string) => workflowsApi.archive(culture, id),
    onSuccess: () => invalidate()
  });
}

// --- نمونه‌های گردش کار -------------------------------------------------------------

export function useWorkflowInstanceSearch(request: WorkflowInstanceSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: workflowsQueryKeys.instanceSearch(request),
    queryFn: ({ signal }) => workflowsApi.searchInstances(culture, request, signal)
  });
}

export function useWorkflowInstance(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: workflowsQueryKeys.instance(id),
    queryFn: ({ signal }) => workflowsApi.getInstance(culture, id as string, signal),
    enabled: !!id
  });
}

export function useStartWorkflowInstance() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateWorkflows();

  return useMutation({
    mutationFn: (request: StartWorkflowInstanceRequest) => workflowsApi.startInstance(culture, request),
    onSuccess: () => invalidate()
  });
}

export function useTransitionWorkflowInstance() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateWorkflows();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: TransitionWorkflowInstanceRequest }) =>
      workflowsApi.transitionInstance(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useCancelWorkflowInstance() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateWorkflows();

  return useMutation({
    mutationFn: (id: string) => workflowsApi.cancelInstance(culture, id),
    onSuccess: () => invalidate()
  });
}

// --- درخواست‌های تأیید -------------------------------------------------------------

export function useWorkflowApprovalSearch(request: WorkflowApprovalSearchRequest) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: workflowsQueryKeys.approvalSearch(request),
    queryFn: ({ signal }) => workflowsApi.searchApprovals(culture, request, signal)
  });
}

export function useDecideWorkflowApproval() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateWorkflows();

  return useMutation({
    mutationFn: ({
      id,
      decision,
      request
    }: {
      id: string;
      decision: 'approve' | 'reject';
      request: DecideWorkflowApprovalRequest;
    }) =>
      decision === 'approve'
        ? workflowsApi.approve(culture, id, request)
        : workflowsApi.reject(culture, id, request),
    onSuccess: () => invalidate()
  });
}
