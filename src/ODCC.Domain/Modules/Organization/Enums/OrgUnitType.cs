namespace ODCC.Domain.Modules.Organization.Enums;

/// <summary>
/// نوع واحد سازمانی.
///
/// توجه: این فقط یک «برچسب طبقه‌بندی» است و تعداد سطوح سازمانی را محدود نمی‌کند.
/// عمق واقعی هر واحد در ساختار درختی با <c>OrgUnit.Level</c> (یک عدد صحیح پویا)
/// نشان داده می‌شود، بنابراین هر سازمان می‌تواند تعداد سطوح متفاوتی داشته باشد.
/// </summary>
public enum OrgUnitType
{
    /// <summary>شرکت</summary>
    Company = 1,

    /// <summary>دایرکتی/بخش</summary>
    Division = 2,

    /// <summary>دپارتمان</summary>
    Department = 3,

    /// <summary>تیم</summary>
    Team = 4,

    /// <summary>سازمانک/واحد پروژه‌ای</summary>
    Unit = 5
}
