/**
 * کاتالوگ مجوزهای سامانه — قرینه‌ی `Permissions` در سمت سرور.
 *
 * کلاینت این مقادیر را فقط برای پنهان کردن ناوبری و کنش‌ها استفاده می‌کند.
 * مرز امنیتی واقعی همواره سمت سرور است (صفت HasPermission و کلیم‌های JWT)،
 * بنابراین هرگز نباید به پنهان‌سازی سمت کلاینت به‌عنوان کنترل دسترسی تکیه کرد.
 */
export const Permissions = {
  Identity: {
    UsersView: 'identity.users.view',
    UsersCreate: 'identity.users.create',
    UsersEdit: 'identity.users.edit',
    UsersDeactivate: 'identity.users.deactivate',
    UsersResetPassword: 'identity.users.reset_password',
    RolesView: 'identity.roles.view',
    RolesManage: 'identity.roles.manage'
  },
  Audit: {
    View: 'audit.view'
  },
  Organization: {
    UnitsView: 'organization.units.view',
    UnitsManage: 'organization.units.manage',
    PositionsView: 'organization.positions.view',
    PositionsManage: 'organization.positions.manage',
    EmployeesView: 'organization.employees.view',
    EmployeesManage: 'organization.employees.manage'
  },
  Survey: {
    View: 'surveys.view',
    Create: 'surveys.create',
    Edit: 'surveys.edit',
    Publish: 'surveys.publish',
    Delete: 'surveys.delete'
  },
  Campaign: {
    View: 'campaigns.view',
    Manage: 'campaigns.manage'
  },
  Response: {
    View: 'responses.view',
    Export: 'responses.export'
  }
} as const;

export type Permission = string;
