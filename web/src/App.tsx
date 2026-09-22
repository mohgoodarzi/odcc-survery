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

      {/* سازمان */}
      <Route
        path="/:culture/organization/units"
        element={
          <RequireAuth permissions={[Permissions.Organization.UnitsView]}>
            <OrgUnitsPage />
          </RequireAuth>
        }
      />
      <Route
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
