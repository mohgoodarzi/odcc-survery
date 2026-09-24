import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { rolesApi } from '@/api/roles';
import { usersApi } from '@/api/users';
import { employeesApi, EmployeeStatus, orgUnitsApi, positionsApi } from '@/api/organization';
import { questionnairesApi, QuestionnaireStatus } from '@/api/questionnaires';
import {
  surveyTemplatesApi,
  surveysApi,
  SurveyStatus,
  type CreateSurveyFromTemplateRequest,
  type SaveSurveyRequest,
  type SaveSurveyTemplateRequest
} from '@/api/surveys';
import {
  campaignsApi,
  CampaignStatus,
  type DistributionSearchRequest,
  type SaveCampaignRequest
} from '@/api/campaigns';
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
  managerOptions: ['employees', 'manager-options'] as const,
  questionnaires: ['questionnaires'] as const,
  surveySearch: (
    searchText: string | null,
    status: number | null,
    questionnaireId: string | null,
    includeArchived: boolean,
    page: number
  ) =>
    ['surveys', 'search', searchText, status, questionnaireId, includeArchived, page] as const,
  surveys: ['surveys'] as const,
  survey: (id: string | null) => ['surveys', 'detail', id] as const,
  surveyTemplateSearch: (searchText: string | null, status: number | null, page: number) =>
    ['survey-templates', 'search', searchText, status, page] as const,
  surveyTemplates: ['survey-templates'] as const,
  campaignSearch: (
    searchText: string | null,
    status: number | null,
    surveyId: string | null,
    includeArchived: boolean,
    page: number
  ) =>
    ['campaigns', 'search', searchText, status, surveyId, includeArchived, page] as const,
  campaigns: ['campaigns'] as const,
  campaign: (id: string | null) => ['campaigns', 'detail', id] as const,
  campaignDistributions: (campaignId: string | null, status: number | null, page: number) =>
    ['campaigns', 'distributions', campaignId, status, page] as const
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

/**
 * همه‌ی کارمندان (شامل غیرشاغل) برای انتخاب جمعیت هدف صریح در فرم کمپین.
 * جستجو سمت کلاینت روی نام/کد انجام می‌شود تا انتخاب سریع بماند.
 */
export function useEmployeeOptions(searchText: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: ['employees', 'campaign-options', searchText],
    queryFn: ({ signal }) =>
      employeesApi.search(
        culture,
        { searchText, status: null, page: 1, pageSize: 200 },
        signal
      ),
    select: (result) => result.items
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

/**
 * پرسشنامه‌های قابل‌استفاده برای انتخاب در فرم نظرسنجی.
 * فقط پرسشنامه‌های منتشرشده معتبر هستند چون نظرسنجی به یک نسخه‌ی مشخص وصل می‌شود.
 */
export function useQuestionnaires() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.questionnaires,
    queryFn: ({ signal }) =>
      questionnairesApi.search(
        culture,
        { status: QuestionnaireStatus.Published, page: 1, pageSize: 200 },
        signal
      )
  });
}

export function useSurveysSearch(params: {
  searchText: string | null;
  status: number | null;
  questionnaireId: string | null;
  includeArchived: boolean;
  page: number;
}) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.surveySearch(
      params.searchText,
      params.status,
      params.questionnaireId,
      params.includeArchived,
      params.page
    ),
    queryFn: ({ signal }) =>
      surveysApi.search(
        culture,
        {
          searchText: params.searchText,
          status: params.status as SurveyStatus | null,
          questionnaireId: params.questionnaireId,
          includeArchived: params.includeArchived,
          page: params.page,
          pageSize: 20
        },
        signal
      )
  });
}

export function useSurvey(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.survey(id),
    queryFn: ({ signal }) => surveysApi.getById(culture, id as string, signal),
    enabled: !!id
  });
}

function useInvalidateSurveys() {
  const queryClient = useQueryClient();

  // invalidation با پیشوند «surveys» هم فهرست و هم جزئیات را پوشش می‌دهد.
  return () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.surveys });
  };
}

export function useCreateSurvey() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSurveys();

  return useMutation({
    mutationFn: surveysApi.create.bind(null, culture),
    onSuccess: () => invalidate()
  });
}

export function useUpdateSurvey() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSurveys();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveSurveyRequest }) =>
      surveysApi.update(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useDeleteSurvey() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSurveys();

  return useMutation({
    mutationFn: surveysApi.delete.bind(null, culture),
    onSuccess: () => invalidate()
  });
}

export function useCreateSurveyFromTemplate() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSurveys();

  return useMutation({
    mutationFn: (request: CreateSurveyFromTemplateRequest) =>
      surveysApi.createFromTemplate(culture, request),
    onSuccess: () => invalidate()
  });
}

/**
 * اکشن‌های چرخه‌ی عمر نظرسنجی: انتشار، شروع، توقف، از سرگیری، بستن و بایگانی.
 * همه‌ی آن‌ها همان مسیر «نظرسنجی» را باطل می‌کنند تا فهرست همگام بماند.
 */
export function useSurveyLifecycle() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateSurveys();

  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: SurveyAction }) => {
      switch (action) {
        case 'publish': return surveysApi.publish(culture, id);
        case 'start': return surveysApi.start(culture, id);
        case 'pause': return surveysApi.pause(culture, id);
        case 'resume': return surveysApi.resume(culture, id);
        case 'close': return surveysApi.close(culture, id);
        case 'archive': return surveysApi.archive(culture, id);
      }
    },
    onSuccess: () => invalidate()
  });
}

export type SurveyAction = 'publish' | 'start' | 'pause' | 'resume' | 'close' | 'archive';

export function useSurveyTemplatesSearch(params: {
  searchText: string | null;
  status: number | null;
  page: number;
}) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.surveyTemplateSearch(params.searchText, params.status, params.page),
    queryFn: ({ signal }) =>
      surveyTemplatesApi.search(
        culture,
        {
          searchText: params.searchText,
          status: params.status,
          page: params.page,
          pageSize: 20
        },
        signal
      )
  });
}

export function useSurveyTemplate(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: ['survey-templates', 'detail', id],
    queryFn: ({ signal }) => surveyTemplatesApi.getById(culture, id as string, signal),
    enabled: !!id
  });
}

export function useCreateSurveyTemplate() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: surveyTemplatesApi.create.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.surveyTemplates });
    }
  });
}

export function useUpdateSurveyTemplate() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveSurveyTemplateRequest }) =>
      surveyTemplatesApi.update(culture, id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.surveyTemplates });
    }
  });
}

export function useDeleteSurveyTemplate() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: surveyTemplatesApi.delete.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.surveyTemplates });
    }
  });
}

export function useArchiveSurveyTemplate() {
  const { culture } = useLanguage();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: surveyTemplatesApi.archive.bind(null, culture),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.surveyTemplates });
    }
  });
}

// --- کمپین‌ها ---------------------------------------------------------------

/**
 * نظرسنجی‌هایی که می‌توانند در یک کمپین توزیع شوند: هر چیزی به جز بسته/بایگانی‌شده.
 * بر اساس سرویس سمت سرور، فقط نظرسنجی‌های فعال قابل اجرا هستند، اما در زمان
 * ساخت کمپین هنوز انتشار نیازمند نیست تا گردش کار قفل نشود.
 */
export function useCampaignableSurveys() {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: ['surveys', 'campaignable'],
    queryFn: ({ signal }) =>
      surveysApi.search(
        culture,
        {
          status: null,
          includeArchived: false,
          page: 1,
          pageSize: 200
        },
        signal
      ),
    select: (result) => result.items.filter(
      (survey) => survey.status !== SurveyStatus.Closed && survey.status !== SurveyStatus.Archived
    )
  });
}

function useInvalidateCampaigns() {
  const queryClient = useQueryClient();

  // invalidation با پیشوند «campaigns» هم فهرست، هم جزئیات و هم توزیع‌ها را پوشش می‌دهد.
  return () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.campaigns });
  };
}

export function useCampaignsSearch(params: {
  searchText: string | null;
  status: number | null;
  surveyId: string | null;
  includeArchived: boolean;
  page: number;
}) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.campaignSearch(
      params.searchText,
      params.status,
      params.surveyId,
      params.includeArchived,
      params.page
    ),
    queryFn: ({ signal }) =>
      campaignsApi.search(
        culture,
        {
          searchText: params.searchText,
          status: params.status as CampaignStatus | null,
          surveyId: params.surveyId,
          includeArchived: params.includeArchived,
          page: params.page,
          pageSize: 20
        },
        signal
      )
  });
}

export function useCampaign(id: string | null) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.campaign(id),
    queryFn: ({ signal }) => campaignsApi.getById(culture, id as string, signal),
    enabled: !!id
  });
}

export function useCreateCampaign() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: campaignsApi.create.bind(null, culture),
    onSuccess: () => invalidate()
  });
}

export function useUpdateCampaign() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveCampaignRequest }) =>
      campaignsApi.update(culture, id, request),
    onSuccess: () => invalidate()
  });
}

export function useDeleteCampaign() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: campaignsApi.delete.bind(null, culture),
    onSuccess: () => invalidate()
  });
}

export function useScheduleCampaign() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: ({ id, scheduledAt }: { id: string; scheduledAt: string }) =>
      campaignsApi.schedule(culture, id, scheduledAt),
    onSuccess: () => invalidate()
  });
}

export function useLaunchCampaign() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: campaignsApi.launch.bind(null, culture),
    onSuccess: () => invalidate()
  });
}

export function useCompleteCampaign() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: campaignsApi.complete.bind(null, culture),
    onSuccess: () => invalidate()
  });
}

export function useArchiveCampaign() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: campaignsApi.archive.bind(null, culture),
    onSuccess: () => invalidate()
  });
}

export function useCampaignDistributions(params: {
  campaignId: string | null;
  status: number | null;
  page: number;
}) {
  const { culture } = useLanguage();

  return useQuery({
    queryKey: queryKeys.campaignDistributions(params.campaignId, params.status, params.page),
    queryFn: ({ signal }) =>
      campaignsApi.distributions(
        culture,
        params.campaignId as string,
        {
          status: params.status as DistributionSearchRequest['status'],
          page: params.page,
          pageSize: 50
        },
        signal
      ),
    enabled: !!params.campaignId
  });
}

export function useCancelReminder() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: ({ campaignId, reminderId }: { campaignId: string; reminderId: string }) =>
      campaignsApi.cancelReminder(culture, campaignId, reminderId),
    onSuccess: () => invalidate()
  });
}

/**
 * پردازش یادآورهای سررسیده‌ی همه‌ی کمپین‌های «در حال اجرا».
 * این عملیات به‌صورت دوره‌ای (مثلاً توسط یک سرویس زمان‌بندی‌شده) فراخوانی می‌شود؛
 * دکمه‌ی دستی برای آزمون و مشاهده‌ی سریع در نظر گرفته شده است.
 */
export function useProcessDueReminders() {
  const { culture } = useLanguage();
  const invalidate = useInvalidateCampaigns();

  return useMutation({
    mutationFn: () => campaignsApi.processDueReminders(culture),
    onSuccess: () => invalidate()
  });
}

export type CampaignAction = 'schedule' | 'launch' | 'complete' | 'archive';
