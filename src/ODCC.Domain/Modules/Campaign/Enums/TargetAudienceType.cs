namespace ODCC.Domain.Modules.Campaign.Enums;

/// <summary>
/// نحوه‌ی تعیین جمعیت هدف یک کمپین.
/// </summary>
public enum TargetAudienceType
{
    /// <summary>تمام کارمندان شرکت (با امکان محدود کردن به کارمندان شاغل).</summary>
    AllCompany = 1,

    /// <summary>کارمندان چند واحد سازمانی مشخص (با یا بدون زیرمجموعه‌ها).</summary>
    OrgUnits = 2,

    /// <summary>کارمندان مشخص‌شده به‌صورت صریح.</summary>
    Employees = 3
}
