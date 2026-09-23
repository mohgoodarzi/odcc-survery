namespace ODCC.Application.Authorization;

/// <summary>
/// گروهی از مجوزها برای نمایش در رابط کاربری مدیریت نقش‌ها.
/// </summary>
public sealed record PermissionGroupDto
{
    /// <summary>کلید گروه (انگلیسی، فاقد تغییر در ترجمه).</summary>
    public required string GroupKey { get; init; }

    /// <summary>برچسب نمایشی گروه.</summary>
    public required string DisplayName { get; init; }

    /// <summary>مجوزهای این گروه.</summary>
    public required IReadOnlyList<string> Permissions { get; init; }
}

/// <summary>
/// کاتالوگ مجوزها به‌صورت گروهی. برای رابط کاربری انتخاب مجوز هنگام
/// ویرایش نقش استفاده می‌شود تا کلاینت مجبور نباشد لیست خام را Hard-code کند.
/// </summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionGroupDto> Groups =
    [
        new()
        {
            GroupKey = nameof(Permissions.Audit),
            DisplayName = "ممیزی و رخدادها",
            Permissions = [Permissions.Audit.View]
        },
        new()
        {
            GroupKey = nameof(Permissions.Identity),
            DisplayName = "هویت و کاربران",
            Permissions = [
                Permissions.Identity.UsersView,
                Permissions.Identity.UsersCreate,
                Permissions.Identity.UsersEdit,
                Permissions.Identity.UsersDeactivate,
                Permissions.Identity.UsersResetPassword,
                Permissions.Identity.RolesView,
                Permissions.Identity.RolesManage
            ]
        },
        new()
        {
            GroupKey = nameof(Permissions.Organization),
            DisplayName = "سازمان و ساختار",
            Permissions = [
                Permissions.Organization.UnitsView,
                Permissions.Organization.UnitsManage,
                Permissions.Organization.PositionsView,
                Permissions.Organization.PositionsManage,
                Permissions.Organization.EmployeesView,
                Permissions.Organization.EmployeesManage
            ]
        },
        new()
        {
            GroupKey = nameof(Permissions.QuestionBank),
            DisplayName = "کتابخانه‌ی سؤالات",
            Permissions = [Permissions.QuestionBank.View, Permissions.QuestionBank.Manage]
        },
        new()
        {
            GroupKey = nameof(Permissions.Questionnaire),
            DisplayName = "پرسشنامه‌ها",
            Permissions = [Permissions.Questionnaire.View, Permissions.Questionnaire.Manage]
        },
        new()
        {
            GroupKey = nameof(Permissions.Survey),
            DisplayName = "نظرسنجی‌ها",
            Permissions = [
                Permissions.Survey.View,
                Permissions.Survey.Create,
                Permissions.Survey.Edit,
                Permissions.Survey.Publish,
                Permissions.Survey.Delete
            ]
        },
        new()
        {
            GroupKey = nameof(Permissions.Campaign),
            DisplayName = "کمپین‌ها",
            Permissions = [Permissions.Campaign.View, Permissions.Campaign.Manage]
        },
        new()
        {
            GroupKey = nameof(Permissions.Response),
            DisplayName = "پاسخ‌ها",
            Permissions = [Permissions.Response.View, Permissions.Response.Export]
        },
        new()
        {
            GroupKey = nameof(Permissions.Analytics),
            DisplayName = "تحلیلات",
            Permissions = [
                Permissions.Analytics.View,
                Permissions.Analytics.DepartmentView,
                Permissions.Analytics.CompanyView
            ]
        },
        new()
        {
            GroupKey = nameof(Permissions.Reports),
            DisplayName = "گزارش‌ها",
            Permissions = [Permissions.Reports.View, Permissions.Reports.Export]
        },
        new()
        {
            GroupKey = nameof(Permissions.Actions),
            DisplayName = "برنامه‌های اقدام",
            Permissions = [Permissions.Actions.View, Permissions.Actions.Manage]
        }
    ];
}
