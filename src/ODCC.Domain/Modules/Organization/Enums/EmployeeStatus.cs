namespace ODCC.Domain.Modules.Organization.Enums;

/// <summary>
/// وضعیت شغلی کارمند.
/// </summary>
public enum EmployeeStatus
{
    /// <summary>فعال</summary>
    Active = 1,

    /// <summary>مرخصی طولانی</summary>
    OnLeave = 2,

    /// <summary>معلق</summary>
    Suspended = 3,

    /// <summary>ترک کار</summary>
    Terminated = 4
}
