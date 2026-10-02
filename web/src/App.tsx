import { Navigate, Route, Routes } from 'react-router-dom';

import { useLanguage } from '@/i18n/LanguageProvider';
import { RequireAuth } from '@/auth/RequireAuth';
import { Permissions } from '@/auth/permissions';
import { Dashboard } from '@/routes/Dashboard';
import { Forbidden } from '@/routes/Forbidden';
import { NotFound } from '@/routes/NotFound';
import { LoginPage } from '@/routes/auth/LoginPage';
import { ProfilePage } from '@/routes/profile/ProfilePage';
import { UsersPage } from '@/routes/identity/UsersPage';
import { RolesPage } from '@/routes/identity/RolesPage';
import { OrgUnitsPage } from '@/routes/organization/OrgUnitsPage';
import { PositionsPage } from '@/routes/organization/PositionsPage';
import { EmployeesPage } from '@/routes/organization/EmployeesPage';
import { SurveysPage } from '@/routes/survey/SurveysPage';
import { SurveyTemplatesPage } from '@/routes/survey/SurveyTemplatesPage';
import { QuestionnairesPage } from '@/routes/questionnaire/QuestionnairesPage';
import { CampaignsPage } from '@/routes/campaign/CampaignsPage';
import { MySurveysPage } from '@/routes/response/MySurveysPage';
import { RespondentSurveyPage } from '@/routes/response/RespondentSurveyPage';
import { ResponsesPage } from '@/routes/response/ResponsesPage';
import { AnalyticsDashboardPage } from '@/routes/analytics/AnalyticsDashboardPage';
import { SurveyAnalyticsPage } from '@/routes/analytics/SurveyAnalyticsPage';
import { BenchmarksPage } from '@/routes/analytics/BenchmarksPage';
import { ReportsPage } from '@/routes/reports/ReportsPage';
import { ActionPlansPage } from '@/routes/actions/ActionPlansPage';
import { ActionItemsPage } from '@/routes/actions/ActionItemsPage';
import { WorkflowsPage } from '@/routes/workflow/WorkflowsPage';
import { WorkflowInstancesPage } from '@/routes/workflow/WorkflowInstancesPage';
import { WorkflowApprovalsPage } from '@/routes/workflow/WorkflowApprovalsPage';
import { IntegrationEndpointsPage } from '@/routes/integration/IntegrationEndpointsPage';
import { WebhookDeliveriesPage } from '@/routes/integration/WebhookDeliveriesPage';
import { SystemSettingsPage } from '@/routes/system/SystemSettingsPage';
import { FeatureFlagsPage } from '@/routes/system/FeatureFlagsPage';
import { SystemPoliciesPage } from '@/routes/system/SystemPoliciesPage';

/**
 * مسیرها با پیشوند فرهنگ هستند: /fa/dashboard , /en/surveys ...
 * این کار باعث می‌شود هر نسخه‌ی زبانی ایندکس و کرال شود.
 *
 * مرز امنیتی واقعی سمت سرور است (HasPermission + JWT claims)؛ محافظ‌های
 * این‌جا فقط تجربه‌ی کاربری را تمیزتر می‌کنند و مسیرهای محافظت‌نشده‌ی
 * تصادفی را مسدود می‌کنند.
 */
export function AppRoutes() {
  const { culture } = useLanguage();

  return (
    <Routes>
      <Route path="/" element={<Navigate to={`/${culture}/dashboard`} replace />} />
      <Route path="/:culture" element={<Navigate to={`/${culture}/dashboard`} replace />} />

      {/* عمومی */}
      <Route path="/:culture/login" element={<LoginPage />} />

      {/* احراز هویت‌شده */}
      <Route
        path="/:culture/dashboard"
        element={
          <RequireAuth>
            <Dashboard />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/forbidden"
        element={
          <RequireAuth>
            <Forbidden />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/profile"
        element={
          <RequireAuth>
            <ProfilePage />
          </RequireAuth>
        }
      />

      {/* مدیریت سامانه */}
      <Route
        path="/:culture/identity/users"
        element={
          <RequireAuth permissions={[Permissions.Identity.UsersView]}>
            <UsersPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/identity/roles"
        element={
          <RequireAuth permissions={[Permissions.Identity.RolesView]}>
            <RolesPage />
          </RequireAuth>
        }
      />

      {/* نظرسنجی‌ها */}
      <Route
        path="/:culture/surveys"
        element={
          <RequireAuth permissions={[Permissions.Survey.View]}>
            <SurveysPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/survey-templates"
        element={
          <RequireAuth permissions={[Permissions.Survey.View]}>
            <SurveyTemplatesPage />
          </RequireAuth>
        }
      />

      {/* پرسشنامه‌ها */}
      <Route
        path="/:culture/questionnaires"
        element={
          <RequireAuth permissions={[Permissions.Questionnaire.View]}>
            <QuestionnairesPage />
          </RequireAuth>
        }
      />

      {/* کمپین‌ها */}
      <Route
        path="/:culture/campaigns"
        element={
          <RequireAuth permissions={[Permissions.Campaign.View]}>
            <CampaignsPage />
          </RequireAuth>
        }
      />

      {/* پاسخ‌ها */}
      <Route
        path="/:culture/my-surveys"
        element={
          <RequireAuth>
            <MySurveysPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/respond/:surveyId"
        element={
          <RequireAuth>
            <RespondentSurveyPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/responses"
        element={
          <RequireAuth permissions={[Permissions.Response.View]}>
            <ResponsesPage />
          </RequireAuth>
        }
      />

      {/* تحلیلات */}
      <Route
        path="/:culture/analytics"
        element={
          <RequireAuth permissions={[Permissions.Analytics.View]}>
            <AnalyticsDashboardPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/analytics/surveys/:surveyId"
        element={
          <RequireAuth permissions={[Permissions.Analytics.View]}>
            <SurveyAnalyticsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/analytics/benchmarks"
        element={
          <RequireAuth permissions={[Permissions.Analytics.CompanyView]}>
            <BenchmarksPage />
          </RequireAuth>
        }
      />

      {/* گزارش‌ها */}
      <Route
        path="/:culture/reports"
        element={
          <RequireAuth permissions={[Permissions.Reports.View]}>
            <ReportsPage />
          </RequireAuth>
        }
      />

      {/* برنامه‌های اقدام */}
      <Route
        path="/:culture/actions/plans"
        element={
          <RequireAuth permissions={[Permissions.Actions.View]}>
            <ActionPlansPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/actions/items"
        element={
          <RequireAuth permissions={[Permissions.Actions.View]}>
            <ActionItemsPage />
          </RequireAuth>
        }
      />

      {/* گردش کار و تأییدها */}
      <Route
        path="/:culture/workflows"
        element={
          <RequireAuth permissions={[Permissions.Workflows.View]}>
            <WorkflowsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/workflow-instances"
        element={
          <RequireAuth permissions={[Permissions.Workflows.View]}>
            <WorkflowInstancesPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/approvals"
        element={
          <RequireAuth permissions={[Permissions.Workflows.View]}>
            <WorkflowApprovalsPage />
          </RequireAuth>
        }
      />

      {/* یکپارچه‌سازی‌ها */}
      <Route
        path="/:culture/integrations/endpoints"
        element={
          <RequireAuth permissions={[Permissions.Integrations.View]}>
            <IntegrationEndpointsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/integrations/deliveries"
        element={
          <RequireAuth permissions={[Permissions.Integrations.View]}>
            <WebhookDeliveriesPage />
          </RequireAuth>
        }
      />

      {/* پیکربندی سامانه */}
      <Route
        path="/:culture/settings"
        element={
          <RequireAuth permissions={[Permissions.System.View]}>
            <SystemSettingsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/system/settings"
        element={
          <RequireAuth permissions={[Permissions.System.View]}>
            <SystemSettingsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/system/feature-flags"
        element={
          <RequireAuth permissions={[Permissions.System.View]}>
            <FeatureFlagsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/system/policies"
        element={
          <RequireAuth permissions={[Permissions.System.View]}>
            <SystemPoliciesPage />
          </RequireAuth>
        }
      />

      {/* سازمان */}
      <Route
        path="/:culture/organization/units"
        element={
          <RequireAuth permissions={[Permissions.Organization.UnitsView]}>
            <OrgUnitsPage />
          </RequireAuth>
        }
      />      <Route
        path="/:culture/organization/positions"
        element={
          <RequireAuth permissions={[Permissions.Organization.PositionsView]}>
            <PositionsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/:culture/organization/employees"
        element={
          <RequireAuth permissions={[Permissions.Organization.EmployeesView]}>
            <EmployeesPage />
          </RequireAuth>
        }
      />

      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}
