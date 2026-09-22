namespace ODCC.Application.Authorization;

/// <summary>
/// کاتالوگ مجوزهای سامانه.
///
/// مجوزها به‌صورت رشته‌های نقش‌بخشی‌شده (dotted) تعریف می‌شوند و هم به‌عنوان
/// کلیم در توکن JWT و هم به‌عنوان نام Policy در <c>[Authorize]</c> استفاده می‌شوند.
/// افزودن مجوز جدید فقط افزودن یک ثابت در اینجا است؛ هیچ کد مجزایی لازم ندارد.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    /// <summary>
    /// یک مجوز تایپ‌امن. از این نوع به‌جای رشته‌ی خام استفاده کنید تا
    /// اشتباه تایپی در زمان کامپایل کشف شود.
    /// </summary>
    public readonly record struct PermissionKey(string Value)
    {
        public override string ToString() => Value;
        public static implicit operator string(PermissionKey p) => p.Value;
    }

    /// <summary>مجوزهای مدیریتی سیستم و هویت.</summary>
    public static class Identity
    {
        public const string Group = "identity";

        public const string UsersView = "identity.users.view";
        public const string UsersCreate = "identity.users.create";
        public const string UsersEdit = "identity.users.edit";
        public const string UsersDeactivate = "identity.users.deactivate";
        public const string UsersResetPassword = "identity.users.reset_password";
        public const string RolesView = "identity.roles.view";
        public const string RolesManage = "identity.roles.manage";
    }

    /// <summary>مجوزهای ممیزی و گزارش رخدادها.</summary>
    public static class Audit
    {
        public const string Group = "audit";

        /// <summary>مشاهده‌ی رخدادهای ممیزی.</summary>
        public const string View = "audit.view";
    }

    /// <summary>مجوزهای ساختار سازمانی.</summary>
    public static class Organization
    {
        public const string UnitsView = "organization.units.view";
        public const string UnitsManage = "organization.units.manage";
        public const string PositionsView = "organization.positions.view";
        public const string PositionsManage = "organization.positions.manage";
        public const string EmployeesView = "organization.employees.view";
        public const string EmployeesManage = "organization.employees.manage";
    }

    /// <summary>مجوزهای نظرسنجی (برای فازهای بعدی).</summary>
    public static class Survey
    {
        public const string View = "surveys.view";
        public const string Create = "surveys.create";
        public const string Edit = "surveys.edit";
        public const string Publish = "surveys.publish";
        public const string Delete = "surveys.delete";
    }

    /// <summary>مجوزهای کمپین و توزیع.</summary>
    public static class Campaign
    {
        public const string View = "campaigns.view";
        public const string Manage = "campaigns.manage";
    }

    /// <summary>مجوزهای پاسخ‌ها.</summary>
    public static class Response
    {
        public const string View = "responses.view";
        public const string Export = "responses.export";
    }

    /// <summary>مجوزهای تحلیلات و داشبورد.</summary>
    public static class Analytics
    {
        public const string View = "analytics.view";
        public const string DepartmentView = "analytics.department.view";
        public const string CompanyView = "analytics.company.view";
    }

    /// <summary>مجوزهای گزارش‌گیری.</summary>
    public static class Reports
    {
        public const string View = "reports.view";
        public const string Export = "reports.export";
    }

    /// <summary>مجوزهای برنامه‌های اقدام.</summary>
    public static class Actions
    {
        public const string View = "actions.view";
        public const string Manage = "actions.manage";
    }

    /// <summary>تمام مجوزهای تعریف‌شده. برای seed کردن نقش‌ها استفاده می‌شود.</summary>
    public static readonly IReadOnlyCollection<string> All =
    [
        Identity.UsersView, Identity.UsersCreate, Identity.UsersEdit, Identity.UsersDeactivate,
        Identity.UsersResetPassword, Identity.RolesView, Identity.RolesManage,
        Audit.View,
        Organization.UnitsView, Organization.UnitsManage,
        Organization.PositionsView, Organization.PositionsManage,
        Organization.EmployeesView, Organization.EmployeesManage,
        Survey.View, Survey.Create, Survey.Edit, Survey.Publish, Survey.Delete,
        Campaign.View, Campaign.Manage,
        Response.View, Response.Export,
        Analytics.View, Analytics.DepartmentView, Analytics.CompanyView,
        Reports.View, Reports.Export,
        Actions.View, Actions.Manage
    ];
}
