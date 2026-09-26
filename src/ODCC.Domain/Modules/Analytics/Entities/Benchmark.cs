using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Enums;

namespace ODCC.Domain.Modules.Analytics.Entities;

/// <summary>
/// بنچمارک (مرجع مقایسه) برای یک شاخص تحلیلی.
///
/// بنچمارک‌ها به مدیران اجازه می‌دهند نتایج نظرسنجی‌ها را با یک مقدار مرجع
/// مقایسه کنند (مثلاً «هدف NPS سازمانی ۴۰ است»). دامنه‌ی بنچمارک می‌تواند
/// «تمام شرکت» یا یک واحد سازمانی مشخص باشد.
///
/// <b>حریم خصوصی:</b> بنچمارک فقط یک عدد مرجع است و هیچ داده‌ی پاسخ‌گو ندارد.
/// </summary>
public class Benchmark : BaseEntity
{
    /// <summary>نام نمایشی بنچمارک، مثلاً «هدف NPS سال ۱۴۰۴».</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>شاخصی که این بنچمارک برای آن تعریف شده.</summary>
    public MetricType Metric { get; set; } = MetricType.Nps;

    /// <summary>مقدار هدف بنچمارک.</summary>
    public decimal TargetValue { get; set; }

    /// <summary>
    /// آیا این بنچمارک برای تمام شرکت است یا فقط یک واحد سازمانی؟
    /// <c>true</c> یعنی تمام شرکت؛ در این صورت <see cref="OrgUnitId"/> خالی است.
    /// </summary>
    public bool IsCompanyWide { get; set; } = true;

    /// <summary>شناسه‌ی واحد سازمانی (فقط اگر <see cref="IsCompanyWide"/> نادرست باشد).</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>مسیر مادی واحد سازمانی (برای فیلتر کارآمد زیردرخت).</summary>
    public string? OrgUnitPath { get; set; }

    /// <summary>توضیحات اختیاری.</summary>
    public string? Description { get; set; }

    /// <summary>آیا بنچمارک فعال است؟ بنچمارک‌های غیرفعال در مقایسه‌ها نادیده گرفته می‌شوند.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// اعمال مقادیر یک بنچمارک. فقط در زمان ساخت/ویرایش توسط مدیر فراخوانی می‌شود.
    /// </summary>
    public void Update(string name, MetricType metric, decimal targetValue, bool isCompanyWide,
        Guid? orgUnitId, string? orgUnitPath, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Metric = metric;
        TargetValue = targetValue;
        IsCompanyWide = isCompanyWide;
        OrgUnitId = isCompanyWide ? null : orgUnitId;
        OrgUnitPath = isCompanyWide ? null : orgUnitPath;
        Description = description;
    }

    /// <summary>غیرفعال‌سازی بنچمارک (به‌جای حذف سخت).</summary>
    public void Deactivate() => IsActive = false;
}
