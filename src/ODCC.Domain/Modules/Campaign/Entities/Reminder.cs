using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;

namespace ODCC.Domain.Modules.Campaign.Entities;

/// <summary>
/// یک یادآور زمان‌بندی‌شده برای کمپین: به گیرندگانی که هنوز پاسخ نداده‌اند،
/// یادآوری ارسال می‌کند.
///
/// ارسال واقعی در ماژول اعلان‌ها (Notification) پیاده‌سازی می‌شود؛ در این فاز
/// یادآور فقط زمان‌بندی و ثبت وضعیت می‌شود.
/// </summary>
public class Reminder : BaseEntity
{
    /// <summary>شناسه‌ی کمپین والد.</summary>
    public Guid CampaignId { get; set; }

    /// <summary>زمان ارسال یادآور (UTC).</summary>
    public DateTime SendAt { get; set; }

    /// <summary>وضعیت یادآور.</summary>
    public ReminderStatus Status { get; set; } = ReminderStatus.Scheduled;

    /// <summary>زمان ارسال واقعی (UTC).</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>ترجمه‌های موضوع و متن یادآور.</summary>
    public List<ReminderLocalization> Localizations { get; set; } = [];

    /// <summary>ثبت ارسال یادآور.</summary>
    public void MarkSent()
    {
        Status = ReminderStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    /// <summary>لغو یادآور.</summary>
    public void Cancel() => Status = ReminderStatus.Cancelled;

    /// <summary>افزودن یا به‌روزرسانی ترجمه‌ی یادآور.</summary>
    public void SetLocalization(Language language, string subject, string? body)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);
        if (existing is null)
        {
            Localizations.Add(new ReminderLocalization
            {
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
/// ترجمه‌ی موضوع و متن یک یادآور.
/// </summary>
public class ReminderLocalization : Localization
{
    /// <summary>موضوع یادآور.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>متن یادآور (اختیاری).</summary>
    public string? Body { get; set; }

    /// <summary>شناسه‌ی یادآور والد.</summary>
    public Guid ReminderId { get; set; }
}
