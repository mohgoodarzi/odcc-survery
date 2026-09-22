namespace ODCC.Domain.Common;

/// <summary>
/// دامنه‌ی دسترسی کاربر به داده‌های سازمانی.
/// این مقدار تعیین می‌کند کاربر تا چه سطحی از داده‌ها را می‌بیند.
/// </summary>
public enum DataScope
{
    /// <summary>فقط داده‌های خود کاربر.</summary>
    Own = 0,

    /// <summary>تیم مستقیم کاربر.</summary>
    Team = 1,

    /// <summary>دپارتمان کاربر و زیرمجموعه‌هایش.</summary>
    Department = 2,

    /// <span>دایرکتی/بخش کاربر و زیرمجموعه‌ها.</span>
    Division = 3,

    /// <summary>تمام شرکت.</summary>
    Company = 4
}
