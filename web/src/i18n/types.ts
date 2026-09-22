/**
 * قرارداد فرهنگ‌ها و دیکشنری‌ها.
 * هر زبان یک فایل دیکشنری دارد که اینترفیس Dictionary را پیاده‌سازی می‌کند.
 */

export type Culture = 'fa' | 'en';
export type Direction = 'rtl' | 'ltr';

export const CULTURES: Culture[] = ['fa', 'en'];
export const DEFAULT_CULTURE: Culture = 'fa';

export const CULTURE_TO_LOCALE: Record<Culture, string> = {
  fa: 'fa-IR',
  en: 'en-US'
};

/** دیکشنری رشته‌های رابط کاربری. افزودن زبان = پیاده‌سازی این اینترفیس. */
export interface Dictionary {
  meta: {
    title: string;
    description: string;
  };
  app: {
    name: string;
    tagline: string;
  };
  nav: {
    dashboard: string;
    surveys: string;
    campaigns: string;
    analytics: string;
    reports: string;
    settings: string;
    profile: string;
    users: string;
    roles: string;
    orgUnits: string;
    positions: string;
    employees: string;
    administration: string;
  };
  common: {
    search: string;
    quickActions: string;
    profile: string;
    notifications: string;
    language: string;
    loading: string;
    error: string;
    retry: string;
    save: string;
    cancel: string;
    notFound: string;
    notFoundDescription: string;
    goHome: string;
    create: string;
    edit: string;
    delete: string;
    confirm: string;
    close: string;
    expand: string;
    collapse: string;
    actions: string;
    yes: string;
    no: string;
    active: string;
    inactive: string;
    all: string;
    optional: string;
    required: string;
    noData: string;
    noDataDescription: string;
    page: string;
    of: string;
    total: string;
    next: string;
    previous: string;
    refresh: string;
    saved: string;
    saving: string;
    deleting: string;
    filters: string;
    clearFilters: string;
    results: string;
  };
  errors: {
    generic: string;
    unauthorized: string;
    forbidden: string;
    forbiddenDescription: string;
    notFound: string;
    validation: string;
    network: string;
    sessionExpired: string;
    invalidCredentials: string;
    accountLocked: string;
    accountInactive: string;
    /** تبدیل کد خطای مستقل از زبان سرور به پیام محلی. */
    fromCode: (code: string) => string;
  };
  auth: {
    signIn: string;
    signOut: string;
    userName: string;
    password: string;
    signInButton: string;
    signingIn: string;
    welcomeBack: string;
    signInDescription: string;
    invalidCredentials: string;
    rememberMe: string;
    forgotPassword: string;
    securityNote: string;
  };
  profile: {
    title: string;
    personalInfo: string;
    security: string;
    changePassword: string;
    currentPassword: string;
    newPassword: string;
    confirmPassword: string;
    passwordChanged: string;
    roles: string;
    permissions: string;
    orgUnit: string;
    dataScope: string;
    memberSince: string;
    noPermissions: string;
  };
  users: {
    title: string;
    description: string;
    newUser: string;
    editUser: string;
    userName: string;
    email: string;
    firstName: string;
    lastName: string;
    phoneNumber: string;
    nationalCode: string;
    password: string;
    orgUnit: string;
    dataScope: string;
    roles: string;
    status: string;
    createdAt: string;
    activate: string;
    deactivate: string;
    resetPassword: string;
    assignRoles: string;
    selectRoles: string;
    noUsers: string;
    noUsersDescription: string;
    searchPlaceholder: string;
    active: string;
    inactive: string;
    all: string;
    changePasswordTitle: string;
    userCreated: string;
    userUpdated: string;
    userActivated: string;
    userDeactivated: string;
    passwordReset: string;
    passwordChanged: string;
    rolesAssigned: string;
  };
  roles: {
    title: string;
    description: string;
    newRole: string;
    editRole: string;
    roleName: string;
    displayName: string;
    descriptionLabel: string;
    permissions: string;
    system: string;
    systemRole: string;
    isSystem: string;
    noRoles: string;
    noRolesDescription: string;
    selectPermissions: string;
    permissionGroups: string;
    roleCreated: string;
    roleUpdated: string;
    permissionsUpdated: string;
    cannotRenameSystemRole: string;
  };
  orgUnits: {
    title: string;
    description: string;
    newUnit: string;
    editUnit: string;
    code: string;
    name: string;
    type: string;
    parent: string;
    status: string;
    startDate: string;
    endDate: string;
    manager: string;
    employeeCount: string;
    level: string;
    path: string;
    noUnits: string;
    noUnitsDescription: string;
    rootUnit: string;
    selectParent: string;
    company: string;
    division: string;
    department: string;
    team: string;
    unit: string;
    unitCreated: string;
    unitUpdated: string;
    unitDeleted: string;
    deleteConfirmTitle: string;
    deleteConfirmDescription: string;
    hasChildren: string;
    hasEmployees: string;
  };
  positions: {
    title: string;
    description: string;
    newPosition: string;
    editPosition: string;
    code: string;
    title_: string;
    orgUnit: string;
    reportsTo: string;
    grade: string;
    headcount: string;
    status: string;
    descriptionLabel: string;
    noPositions: string;
    noPositionsDescription: string;
    selectOrgUnit: string;
    selectReportsTo: string;
    positionCreated: string;
    positionUpdated: string;
    positionDeleted: string;
    deleteConfirmTitle: string;
    deleteConfirmDescription: string;
  };
  employees: {
    title: string;
    description: string;
    newEmployee: string;
    editEmployee: string;
    employeeCode: string;
    nationalCode: string;
    firstName: string;
    lastName: string;
    fatherName: string;
    user: string;
    orgUnit: string;
    position: string;
    manager: string;
    status: string;
    startDate: string;
    endDate: string;
    workEmail: string;
    internalPhone: string;
    noEmployees: string;
    noEmployeesDescription: string;
    searchPlaceholder: string;
    selectOrgUnit: string;
    selectPosition: string;
    selectManager: string;
    includeDescendants: string;
    active: string;
    onLeave: string;
    suspended: string;
    terminated: string;
    employed: string;
    notEmployed: string;
    employeeCreated: string;
    employeeUpdated: string;
    employeeDeleted: string;
    deleteConfirmTitle: string;
    deleteConfirmDescription: string;
    hasSubordinates: string;
  };
  dataScope: {
    own: string;
    team: string;
    department: string;
    division: string;
    company: string;
  };
  dashboard: {
    responses: string;
    averageNps: string;
  };
}
