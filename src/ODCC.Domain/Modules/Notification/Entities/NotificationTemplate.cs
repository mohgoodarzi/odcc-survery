using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Domain.Modules.Notification.Entities;

/// <summary>
/// قالب پیام قابل‌استفاده‌ی مجدد: موضوع و بدنه به ازای هر زبان، با متغیرهای
/// جایگزین‌شونده (مثل <c>{{recipient_name}}</c>).
///
/// قالب‌ها در پایگاه داده نگه‌داری می‌شوند تا مدیران بتوانند متن‌ها را بدون
/// تغییر کد ویرایش کنند. اگر قالبی برای کدی وجود نداشته باشد یا غیرفعال باشد،
/// رندر از متن‌های پیش‌فرض توکار استفاده می‌کند تا سامانه همیشه کار کند.
/// </summary>
public sealed class NotificationTemplate : BaseEntity
{
    /// <summary>
    /// کد یکتای قالب (مثلاً <c>campaign_invitation</c>). کد مبنای جستجو است،
    /// نه شناسه، تا فراخوانان به جزئیات پایگاه داده وابسته نباشند.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>نام نمایشی قالب برای پنل مدیریت.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>کانالی که این قالب برای آن است.</summary>
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    /// <summary>دسته‌بندی منطقی اعلان‌های تولیدشده از این قالب.</summary>
    public NotificationCategory Category { get; set; } = NotificationCategory.General;

    /// <summary>قالب‌های غیرفعال در رندر نادیده گرفته می‌شوند (افت می‌کنند به پیش‌فرض توکار).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>ترجمه‌های موضوع/بدنه.</summary>
    public List<NotificationTemplateLocalization> Localizations { get; set; } = [];

    /// <summary>درج یا به‌روزرسانی ترجمه‌ی یک زبان (Upsert بر اساس زبان).</summary>
    public void SetLocalization(Language language, string subject, string body)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);

        if (existing is null)
        {
            Localizations.Add(new NotificationTemplateLocalization
            {
                TemplateId = Id,
                Language = language,
                Subject = subject,
                Body = body
            });
        }
        else
        {
            existing.Subject = subject;
            existing.Body = body;
        }
    }
}

/// <summary>
/// ترجمه‌ی موضوع و بدنه‌ی یک قالب.
/// </summary>
public sealed class NotificationTemplateLocalization : Localization
{
    /// <summary>موضوع پیام (می‌تواند شامل متغیرهای <c>{{name}}</c> باشد).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>بدنه‌ی پیام.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>شناسه‌ی قالب مادر.</summary>
    public Guid TemplateId { get; set; }
}
