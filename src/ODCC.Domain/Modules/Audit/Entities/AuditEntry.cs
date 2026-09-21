using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Audit.Entities;

/// <summary>
/// یک رخداد ممیزی: چه کسی، چه زمانی، چه عملی را روی چه منبعی انجام داده است.
/// این موجودیت به‌عنوان «ماژول مرجع» پیاده‌سازی شده است تا الگوی پیاده‌سازی
/// سایر ماژول‌ها (لایه‌بندی، DbContext ماژولی، مخزن و کنترلر) مشخص و اثبات‌شده باشد.
/// </summary>
public class AuditEntry : BaseEntity
{
    /// <summary>شناسه‌ی کاربری که عمل را انجام داده. برای رویدادهای سیستمی خالی است.</summary>
    public Guid? UserId { get; set; }

    /// <summary>نام نمایشی کاربر در زمان ثبت (غیرقابل تغییر در گزارش‌های بعدی).</summary>
    public string? UserName { get; set; }

    /// <summary>عملیات انجام‌شده، مثل create / update / delete / publish.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>نوع منبع تحت تأثیر، مثل survey / campaign / response.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>شناسه‌ی منبع تحت تأثیر.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>دسته‌ی حساسیت عملیات: low / medium / high.</summary>
    public string Severity { get; set; } = "medium";

    /// <summary>آدرس IP درخواست‌کننده.</summary>
    public string? ClientIp { get; set; }

    /// <summary>متن کوتاه توصیفی عملیات.</summary>
    public string? Description { get; set; }

    /// <summary>جزئیات تغییرات به‌صورت JSON سریالایز شده. هرگز حاوی اسرار نیست.</summary>
    public string? Changes { get; set; }
}
