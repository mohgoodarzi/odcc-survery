import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { rolesApi } from '@/api/roles';
import { usersApi } from '@/api/users';
import { employeesApi, EmployeeStatus, orgUnitsApi, positionsApi } from '@/api/organization';
import { useLanguage } from '@/i18n/LanguageProvider';

/**
 * کلیدهای کوئری متمرکز تا invalidation بین صفحات یکپارچه باشد.
 */
export const queryKeys = {
  users: ['users'] as const,
  userSearch: (searchText: string | null, isActive: boolean | null, page: number) =>
    ['users', 'search', searchText, isActive, page] as const,
  roles: ['roles'] as const,
  permissionCatalog: ['permission-catalog'] as const,
  orgUnits: ['org-units'] as const,
  orgUnitTree: ['org-unit-tree'] as const,
  positions: ['positions'] as const,
  employees: (
    searchText: string | null,
    orgUnitId: string | null,
    includeDescendants: boolean,
    status: number | null,
    page: number
  ) => ['employees', searchText, orgUnitId, includeDescendants, status, page] as const,
  employee: (id: string | null) => ['employees', 'detail', id] as const,
  managerOptions: ['employees', 'manager-options'] as const
};

export function useUsersSearch(params: {
  searchText: string | null;
  isActive: boolean | null;
  page: number;
}) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.userSearch(params.searchText, params.isActive, params.page),
    queryFn: ({ signal }) =>
      usersApi.search(culture, {
        searchText: params.searchText,
        isActive: params.isActive,
        page: params.page,
        pageSize: 20
      }, signal)
  });
}

export function useRoles() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.roles,
    queryFn: ({ signal }) => rolesApi.list(culture, signal)
  });
}

export function usePermissionCatalog() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.permissionCatalog,
    queryFn: ({ signal }) => rolesApi.permissionCatalog(culture, signal),
    staleTime: 5 * 60 * 1000
  });
}

export function useCreateRole() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: rolesApi.create.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.roles });
    }
  });
}

export function useUpdateRole() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: Parameters<typeof rolesApi.update>[2] }) =>
      rolesApi.update(culture, id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.roles });
    }
  });
}

export function useOrgUnits() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.orgUnits,
    queryFn: ({ signal }) => orgUnitsApi.list(culture, false, signal)
  });
}

export function useOrgUnitTree() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.orgUnitTree,
    queryFn: ({ signal }) => orgUnitsApi.tree(culture, signal)
  });
}

export function useCreateOrgUnit() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: orgUnitsApi.create.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.orgUnits });
      void queryClient.invalidateQueries({ queryKey: queryKeys.orgUnitTree });
    }
  });
}

export function useUpdateOrgUnit() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: Parameters<typeof orgUnitsApi.update>[2] }) =>
      orgUnitsApi.update(culture, id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.orgUnits });
      void queryClient.invalidateQueries({ queryKey: queryKeys.orgUnitTree });
    }
  });
}

export function useDeleteOrgUnit() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: orgUnitsApi.delete.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.orgUnits });
      void queryClient.invalidateQueries({ queryKey: queryKeys.orgUnitTree });
    }
  });
}

export function usePositions() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.positions,
    queryFn: ({ signal }) => positionsApi.list(culture, false, signal)
  });
}

export function useCreatePosition() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: positionsApi.create.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.positions });
    }
  });
}

export function useUpdatePosition() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: Parameters<typeof positionsApi.update>[2] }) =>
      positionsApi.update(culture, id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.positions });
    }
  });
}

export function useDeletePosition() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: positionsApi.delete.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.positions });
    }
  });
}

export function useEmployeesSearch(params: {
  searchText: string | null;
  orgUnitId: string | null;
  includeDescendants?: boolean;
  status: number | null;
  page: number;
}) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.employees(
      params.searchText,
      params.orgUnitId,
      params.includeDescendants ?? false,
      params.status,
      params.page
    ),
    queryFn: ({ signal }) =>
      employeesApi.search(
        culture,
        {
          searchText: params.searchText,
          orgUnitId: params.orgUnitId,
          includeDescendants: params.includeDescendants ?? false,
          status: params.status,
          page: params.page,
          pageSize: 20
        },
        signal
      )
  });
}

export function useEmployee(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.employee(id),
    queryFn: ({ signal }) => employeesApi.getById(culture, id as string, signal),
    enabled: !!id
  });
}

/**
 * گزینه‌های مدیر مستقیم: کارمندان فعال. برای انتخاب مدیر در فرم کارمند.
 */
export function useManagerOptions() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.managerOptions,
    queryFn: ({ signal }) =>
      employeesApi.search(
        culture,
        { status: EmployeeStatus.Active, page: 1, pageSize: 200 },
        signal
      )
  });
}

export function useCreateEmployee() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: employeesApi.create.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['employees'] });
    }
  });
}

export function useUpdateEmployee() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: Parameters<typeof employeesApi.update>[2] }) =>
      employeesApi.update(culture, id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['employees'] });
    }
  });
}

export function useDeleteEmployee() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: employeesApi.delete.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['employees'] });
    }
  });
}
