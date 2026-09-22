using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Enums;

namespace ODCC.Domain.Modules.Organization.Entities;

/// <summary>
/// یک کارمند در ساختار سازمانی.
///
/// کارمند می‌تواند با یک حساب کاربری (<see cref="UserId"/>) مرتبط باشد یا نباشد
/// (مثلاً کارمندانی که فقط در جمعیت هدف نظرسنجی قرار می‌گیرند ولی به سامانه وارد نمی‌شوند).
/// روابط مدیریتی با <see cref="ManagerId"/> و تاریخ‌های اثرگذاری با
/// <see cref="StartDate"/>/<see cref="EndDate"/> مدیریت می‌شوند.
/// </summary>
public class Employee : BaseEntity
{
    /// <summary>کد یکتای پرسنلی.</summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>کد ملی (اختیاری).</summary>
    public string? NationalCode { get; set; }

    /// <summary>نام.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>نام خانوادگی.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>نام پدر (اختیاری، برای تشخیص هم‌نام‌ها).</summary>
    public string? FatherName { get; set; }

    /// <summary>شناسه‌ی حساب کاربری مرتبط (اختیاری).</summary>
    public Guid? UserId { get; set; }

    /// <summary>واحد سازمانی فعلی کارمند.</summary>
    public Guid OrgUnitId { get; set; }

    /// <summary>موقعیت شغلی فعلی کارمند.</summary>
    public Guid? PositionId { get; set; }

    /// <summary>کارمندِ مدیریت‌کننده (مدیر مستقیم). self-reference.</summary>
    public Guid? ManagerId { get; set; }

    /// <summary>وضعیت شغلی.</summary>
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>تاریخ شروع اشتغال (میلادی).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>تاریخ پایان اشتغال (میلادی). <c>null</c> یعنی همچنان فعال.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>ایمیل سازمانی (اختیاری).</summary>
    public string? WorkEmail { get; set; }

    /// <summary>تلفن داخلی (اختیاری).</summary>
    public string? InternalPhone { get; set; }

    /// <summary>آیا کارمند در حال حاضر شاغل است؟</summary>
    public bool IsCurrentlyEmployed =>
        Status == EmployeeStatus.Active || Status == EmployeeStatus.OnLeave;

    /// <summary>نام کامل نمایشی.</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
