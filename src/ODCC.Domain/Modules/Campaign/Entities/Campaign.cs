using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;

namespace ODCC.Domain.Modules.Campaign.Entities;

/// <summary>
/// یک کمپین: برنامه‌ی توزیع یک نظرسنجی به جمعیت هدف با زمان‌بندی، کانال ارسال،
/// یادآورها و پیگیری وضعیت توزیع.
///
/// **ساختار:** کمپین ← واحدهای هدف / اعضای هدف / یادآورها، و ردیف‌های توزیع
/// (موجودیت مجازی <see cref="Distribution"/>) که در زمان اجرا برای هر گیرنده ساخته
/// می‌شوند.
///
/// **مرز ماژول‌ها:** کمپین به نظرسنجی (Survey) با شناسه ارجاع می‌دهد و برای حل
/// جمعیت هدف فقط از قراردادهای ماژول سازمان استفاده می‌کند، هرگز از DbContext آن.
/// </summary>
public class Campaign : BaseEntity
{
    /// <summary>کد یکتای کمپین، مثلاً «CMP-ENG-2026Q3».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>وضعیت چرخه‌ی عمر.</summary>
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    /// <summary>شناسه‌ی نظرسنجی‌ای که توزیع می‌شود.</summary>
    public Guid SurveyId { get; set; }

    /// <summary>کد نظرسنجی (برای خوانایی فهرست‌ها).</summary>
    public string SurveyCode { get; set; } = string.Empty;

    /// <summary>نحوه‌ی تعیین جمعیت هدف.</summary>
    public TargetAudienceType AudienceType { get; set; } = TargetAudienceType.AllCompany;

    /// <summary>آیا کارمندان غیرشاغل (ترک‌کار/معلق) هم در جمعیت هدف باشند؟</summary>
    public bool IncludeInactiveEmployees { get; set; }

    /// <summary>کانال توزیع دعوت‌نامه.</summary>
    public DistributionChannel Channel { get; set; } = DistributionChannel.Email;

    // --- زمان‌بندی -------------------------------------------------------------

    /// <summary>زمان برنامه‌ریزی‌شده‌ی شروع توزیع (UTC). <c>null</c> یعنی فوری.</summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>مهلت نهایی پاسخ‌گویی (UTC) — بر پایان نظرسنجی ارجح است.</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>زمان اجرای واقعی کمپین (UTC).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>زمان تکمیل کمپین (UTC).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>زمان بایگانی (UTC).</summary>
    public DateTime? ArchivedAt { get; set; }

    // --- مجموعه‌ها --------------------------------------------------------------

    /// <summary>ترجمه‌های عنوان و توضیح کمپین (موضوع/متن دعوت‌نامه).</summary>
    public List<CampaignLocalization> Localizations { get; set; } = [];

    /// <summary>واحدهای سازمانی هدف (برای <see cref="TargetAudienceType.OrgUnits"/>).</summary>
    public List<CampaignTargetUnit> TargetUnits { get; set; } = [];

    /// <summary>کارمندان هدف صریح (برای <see cref="TargetAudienceType.Employees"/>).</summary>
    public List<CampaignTargetMember> TargetMembers { get; set; } = [];

    /// <summary>یادآورهای زمان‌بندی‌شده‌ی کمپین.</summary>
    public List<Reminder> Reminders { get; set; } = [];

    /// <summary>افزودن یا به‌روزرسانی ترجمه‌ی کمپین.</summary>
    public void SetLocalization(Language language, string title, string? description)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);
        if (existing is null)
        {
            Localizations.Add(new CampaignLocalization
            {
                Language = language,
                Title = title,
                Description = description
            });
        }
        else
        {
            existing.Title = title;
            existing.Description = description;
        }
    }

    /// <summary>افزودن یک واحد سازمانی هدف (بدون تکرار).</summary>
    public bool AddTargetUnit(Guid orgUnitId, bool includeDescendants)
    {
        if (TargetUnits.Any(t => t.OrgUnitId == orgUnitId))
        {
            return false;
        }

        TargetUnits.Add(new CampaignTargetUnit
        {
            CampaignId = Id,
            OrgUnitId = orgUnitId,
            IncludeDescendants = includeDescendants
        });

        return true;
    }

    /// <summary>افزودن یک کارمند هدف صریح (بدون تکرار).</summary>
    public bool AddTargetMember(Guid employeeId)
    {
        if (TargetMembers.Any(m => m.EmployeeId == employeeId))
        {
            return false;
        }

        TargetMembers.Add(new CampaignTargetMember
        {
            CampaignId = Id,
            EmployeeId = employeeId
        });

        return true;
    }

    /// <summary>افزودن یک یادآور زمان‌بندی‌شده.</summary>
    public void AddReminder(Reminder reminder)
    {
        reminder.CampaignId = Id;
        Reminders.Add(reminder);
    }

    /// <summary>لغو یک یادآور با شناسه.</summary>
    public bool CancelReminder(Guid reminderId)
    {
        var reminder = Reminders.FirstOrDefault(r => r.Id == reminderId);
        if (reminder is null or { Status: ReminderStatus.Sent })
        {
            return false;
        }

        reminder.Cancel();
        return true;
    }

    // --- ماشین وضعیت ----------------------------------------------------------

    /// <summary>آیا جمعیت هدف به‌درستی پیکربندی شده است؟</summary>
    public bool IsAudienceConfigured => AudienceType switch
    {
        TargetAudienceType.AllCompany => true,
        TargetAudienceType.OrgUnits => TargetUnits.Count > 0,
        TargetAudienceType.Employees => TargetMembers.Count > 0,
        _ => false
    };

    /// <summary>آیا کمپین قابل اجراست؟</summary>
    public bool CanLaunch => Status is CampaignStatus.Draft or CampaignStatus.Scheduled;

    /// <summary>زمان‌بندی کمپین برای آینده.</summary>
    public void Schedule(DateTime scheduledAt)
    {
        Status = CampaignStatus.Scheduled;
        ScheduledAt = scheduledAt;
    }

    /// <summary>اجرای کمپین: گذار به «در حال اجرا».</summary>
    public void Launch()
    {
        Status = CampaignStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>تکمیل کمپین.</summary>
    public void Complete()
    {
        Status = CampaignStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>بایگانی کمپین.</summary>
    public void Archive()
    {
        Status = CampaignStatus.Archived;
        ArchivedAt = DateTime.UtcNow;
    }
}
