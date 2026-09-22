import { apiDelete, apiPut, apiRequest } from './client';import type { Culture } from '@/i18n/types';

export enum OrgUnitType {
  Company = 1,
  Division = 2,
  Department = 3,
  Team = 4,
  Unit = 5
}

export enum EmployeeStatus {
  Active = 1,
  OnLeave = 2,
  Suspended = 3,
  Terminated = 4
}

export interface OrgUnit {
  id: string;
  code: string;
  name: string;
  type: OrgUnitType;
  parentId: string | null;
  parentName: string | null;
  path: string;
  level: number;
  isActive: boolean;
  startDate: string | null;
  endDate: string | null;
  managerEmployeeId: string | null;
  managerFullName: string | null;
  employeeCount: number;
}

export interface OrgUnitTreeNode {
  id: string;
  code: string;
  name: string;
  type: OrgUnitType;
  level: number;
  isActive: boolean;
  managerEmployeeId: string | null;
  managerFullName: string | null;
  children: OrgUnitTreeNode[];
}

export interface SaveOrgUnitRequest {
  code: string;
  name: string;
  type: OrgUnitType;
  parentId?: string | null;
  isActive?: boolean;
  startDate?: string | null;
  endDate?: string | null;
  managerEmployeeId?: string | null;
}

export interface Position {
  id: string;
  code: string;
  title: string;
  orgUnitId: string;
  orgUnitName: string | null;
  reportsToPositionId: string | null;
  reportsToTitle: string | null;
  grade: number | null;
  isActive: boolean;
  headcount: number | null;
  description: string | null;
}

export interface SavePositionRequest {
  code: string;
  title: string;
  orgUnitId: string;
  reportsToPositionId?: string | null;
  grade?: number | null;
  isActive?: boolean;
  headcount?: number | null;
  description?: string | null;
}

export interface EmployeeSummary {
  id: string;
  employeeCode: string;
  fullName: string;
  orgUnitId: string;
  orgUnitName: string | null;
  positionTitle: string | null;
  managerFullName: string | null;
  status: EmployeeStatus;
  isCurrentlyEmployed: boolean;
}

export interface Employee {
  id: string;
  employeeCode: string;
  nationalCode: string | null;
  firstName: string;
  lastName: string;
  fullName: string;
  fatherName: string | null;
  userId: string | null;
  userName: string | null;
  orgUnitId: string;
  orgUnitName: string | null;
  positionId: string | null;
  positionTitle: string | null;
  managerId: string | null;
  managerFullName: string | null;
  status: EmployeeStatus;
  startDate: string;
  endDate: string | null;
  workEmail: string | null;
  internalPhone: string | null;
  isCurrentlyEmployed: boolean;
}

export interface SaveEmployeeRequest {
  employeeCode: string;
  nationalCode?: string | null;
  firstName: string;
  lastName: string;
  fatherName?: string | null;
  userId?: string | null;
  orgUnitId: string;
  positionId?: string | null;
  managerId?: string | null;
  status?: EmployeeStatus;
  startDate?: string | null;
  endDate?: string | null;
  workEmail?: string | null;
  internalPhone?: string | null;
}

export interface EmployeeSearchRequest {
  searchText?: string | null;
  orgUnitId?: string | null;
  includeDescendants?: boolean;
  status?: EmployeeStatus | null;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

export const orgUnitsApi = {
  list: (culture: Culture, activeOnly = false, signal?: AbortSignal) =>
    apiRequest<OrgUnit[]>(
      culture,
      appendSearchParams('/organization/units', activeOnly ? { activeOnly: 'true' } : {}),
      { signal }
    ),

  tree: (culture: Culture, signal?: AbortSignal) =>
    apiRequest<OrgUnitTreeNode[]>(culture, '/organization/units/tree', { signal }),

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<OrgUnit>(culture, `/organization/units/${id}`, { signal }),

  descendants: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<string[]>(culture, `/organization/units/${id}/descendants`, { signal }),

  create: (culture: Culture, request: SaveOrgUnitRequest) =>
    apiRequest<OrgUnit>(culture, '/organization/units', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SaveOrgUnitRequest) =>
    apiPut<OrgUnit>(culture, `/organization/units/${id}`, request),

  delete: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/organization/units/${id}`)
};

export const positionsApi = {
  list: (culture: Culture, activeOnly = false, signal?: AbortSignal) =>
    apiRequest<Position[]>(
      culture,
      appendSearchParams('/organization/positions', activeOnly ? { activeOnly: 'true' } : {}),
      { signal }
    ),

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<Position>(culture, `/organization/positions/${id}`, { signal }),

  create: (culture: Culture, request: SavePositionRequest) =>
    apiRequest<Position>(culture, '/organization/positions', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SavePositionRequest) =>
    apiPut<Position>(culture, `/organization/positions/${id}`, request),

  delete: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/organization/positions/${id}`)
};

export const employeesApi = {
  search: (culture: Culture, request: EmployeeSearchRequest, signal?: AbortSignal) => {
    const params: Record<string, string> = {};
    if (request.searchText) params.searchText = request.searchText;
    if (request.orgUnitId) {
      params.orgUnitId = request.orgUnitId;
      params.includeDescendants = String(request.includeDescendants ?? false);
    }
    if (request.status !== null && request.status !== undefined) {
      params.status = String(request.status);
    }
    params.page = String(request.page ?? 1);
    params.pageSize = String(request.pageSize ?? 20);

    return apiRequest<PagedResult<EmployeeSummary>>(
      culture,
      appendSearchParams('/organization/employees', params),
      { signal }
    );
  },

  getById: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<Employee>(culture, `/organization/employees/${id}`, { signal }),

  create: (culture: Culture, request: SaveEmployeeRequest) =>
    apiRequest<Employee>(culture, '/organization/employees', { method: 'POST', body: request }),

  update: (culture: Culture, id: string, request: SaveEmployeeRequest) =>
    apiPut<Employee>(culture, `/organization/employees/${id}`, request),

  delete: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/organization/employees/${id}`)
};
