using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Organization.Entities;

/// <summary>
/// یک موقعیت شغلی (پست سازمانی) در یک واحد سازمانی.
/// یک موقعیت می‌تواند به موقعیت بالاسری خود گزارش دهد (ساختار درختی موقعیت‌ها).
/// </summary>
public class Position : BaseEntity
{
    /// <summary>کد یکتای موقعیت، مثلاً «P-FIN-MGR».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>عنوان موقعیت.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>واحد سازمانی که این موقعیت به آن تعلق دارد.</summary>
    public Guid OrgUnitId { get; set; }

    /// <summary>موقعیتی که این موقعیت به آن گزارش می‌دهد (مدیر بالاسری).</summary>
    public Guid? ReportsToPositionId { get; set; }

    /// <summary>درجه/گروه موقعیت (اختیاری، برای ساختارهای حقوقی).</summary>
    public int? Grade { get; set; }

    /// <summary>آیا موقعیت فعال است؟</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>تعداد ظرفیت این موقعیت (اختیاری).</summary>
    public int? Headcount { get; set; }

    /// <summary>توضیحات موقعیت.</summary>
    public string? Description { get; set; }
}
