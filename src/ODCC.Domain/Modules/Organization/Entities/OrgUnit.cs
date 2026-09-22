using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Enums;

namespace ODCC.Domain.Modules.Organization.Entities;

/// <summary>
/// یک واحد در ساختار درختی سازمان: شرکت، دایرکتی، دپارتمان، تیم یا واحد پروژه‌ای.
///
/// ساختار درختی با مسیر مادی (Materialized Path) در <see cref="Path"/> پیاده‌سازی شده است
/// تا پرس‌وجوهای زیردرخت (مثلاً «تمام دپارتمان‌های زیر این دایرکتی») با یک عملگر
/// LIKE کارآمد انجام شوند. تعداد سطوح سازمانی <b>ثابت نیست</b> و با <see cref="Level"/>
/// به‌صورت پویا محاسبه می‌شود.
/// </summary>
public class OrgUnit : BaseEntity
{
    /// <summary>کد یکتای سازمانی، مثلاً «HQ-FIN-AP».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>نام نمایشی واحد.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>نوع واحد (فقط طبقه‌بندی؛ تعداد سطوح را محدود نمی‌کند).</summary>
    public OrgUnitType Type { get; set; }

    /// <summary>شناسه‌ی واحد والد. <c>null</c> یعنی ریشه‌ی درخت.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>مسیر مادی از ریشه تا این واحد، با اسلش جدا می‌شود: «/root/fin/ap».</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>عمق واحد در درخت (ریشه ۰ است). پویا و در زمان تغییر ساختار محاسبه می‌شود.</summary>
    public int Level { get; set; }

    /// <summary>آیا واحد فعال است؟</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>تاریخ شروع اعتبار واحد (میلادی).</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>تاریخ پایان اعتبار واحد (میلادی).</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>کارمد مدیریت‌کننده‌ی این واحد (سرپرست مستقیم واحد).</summary>
    public Guid? ManagerEmployeeId { get; set; }

    /// <summary>
    /// تنظیم مسیر و عمق بر اساس مسیر والد.
    /// مسیر همواره با اسلش شروع می‌شود (ریشه‌دار است) تا مقایسه‌ی پیشوندها
    /// در <see cref="IsDescendantOf"/> و فیلترهای دامنه‌ی سازمانی یکسان بمانند.
    /// </summary>
    /// <param name="parentPath">مسیر والد (با یا بدون اسلش ابتدا/انتها) یا خالی برای ریشه.</param>
    public void SetPath(string? parentPath)
    {
        var parent = (parentPath ?? string.Empty).Trim('/');
        Path = parent.Length == 0 ? $"/{Code}" : $"/{parent}/{Code}";
        Level = Path.Count(c => c == '/') - 1;
    }

    /// <summary>آیا این واحد زیردرختِ <paramref name="otherPath"/> است؟</summary>
    public bool IsDescendantOf(string otherPath) =>
        !string.IsNullOrWhiteSpace(otherPath)
        && Path.Length > otherPath.Length
        && Path.StartsWith(otherPath, StringComparison.OrdinalIgnoreCase);
}
