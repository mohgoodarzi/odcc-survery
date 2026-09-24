using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;

namespace ODCC.Application.Modules.Campaign.Dtos;

/// <summary>
/// ترجمه‌ی عنوان و توضیح یک کمپین.
/// </summary>
public sealed record CampaignLocalizationDto
{
    public Language Language { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>
/// یک واحد سازمانی هدف.
/// </summary>
public sealed record CampaignTargetUnitDto
{
    public Guid Id { get; init; }
    public Guid OrgUnitId { get; init; }

    /// <summary>نام واحد سازمانی (برای نمایش، از سمت سرویس پر می‌شود).</summary>
    public string? OrgUnitName { get; init; }

    public bool IncludeDescendants { get; init; }
}

/// <summary>
/// یک کارمند هدف صریح.
/// </summary>
public sealed record CampaignTargetMemberDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }

    /// <summary>نام کامل کارمند (برای نمایش، از سمت سرویس پر می‌شود).</summary>
    public string? EmployeeName { get; init; }
}

/// <summary>
/// ترجمه‌ی موضوع و متن یک یادآور.
/// </summary>
public sealed record ReminderLocalizationDto
{
    public Language Language { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string? Body { get; init; }
}

/// <summary>
/// یک یادآور در خروجی.
/// </summary>
public sealed record ReminderDto
{
    public Guid Id { get; init; }
    public DateTime SendAt { get; init; }
    public ReminderStatus Status { get; init; }
    public DateTime? SentAt { get; init; }
    public IReadOnlyList<ReminderLocalizationDto> Localizations { get; init; } = [];

    /// <summary>موضوع در زبان درخواست‌شده.</summary>
    public string Subject { get; init; } = string.Empty;
}

/// <summary>
/// یک یادآور در درخواست ذخیره.
/// </summary>
public sealed record SaveReminderRequest
{
    /// <summary>شناسه‌ی یادآور موجود (در ویرایش؛ خالی برای یادآور جدید).</summary>
    public Guid? Id { get; init; }

    public DateTime SendAt { get; init; }
    public IReadOnlyList<ReminderLocalizationDto> Localizations { get; init; } = [];
}

/// <summary>
/// یک ردیف توزیع.
/// </summary>
public sealed record DistributionDto
{
    public Guid Id { get; init; }
    public Guid CampaignId { get; init; }
    public Guid EmployeeId { get; init; }

    /// <summary>نام کامل گیرنده (برای نمایش، از سمت سرویس پر می‌شود).</summary>
    public string? EmployeeName { get; init; }

    public string? WorkEmail { get; init; }
    public DistributionStatus Status { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? RespondedAt { get; init; }
    public string? FailureReason { get; init; }
    public int ReminderCount { get; init; }
}

/// <summary>
/// خروجی استاندارد یک کمپین.
/// </summary>
public sealed record CampaignDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public CampaignStatus Status { get; init; }
    public Guid SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;

    public TargetAudienceType AudienceType { get; init; }
    public bool IncludeInactiveEmployees { get; init; }
    public DistributionChannel Channel { get; init; }

    public DateTime? ScheduledAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? ArchivedAt { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    public IReadOnlyList<CampaignLocalizationDto> Localizations { get; init; } = [];
    public IReadOnlyList<CampaignTargetUnitDto> TargetUnits { get; init; } = [];
    public IReadOnlyList<CampaignTargetMemberDto> TargetMembers { get; init; } = [];
    public IReadOnlyList<ReminderDto> Reminders { get; init; } = [];

    public bool IsAudienceConfigured { get; init; }
    public bool CanLaunch { get; init; }

    /// <summary>تعداد کل ردیف‌های توزیع (پس از اجرا).</summary>
    public int TotalDistributions { get; init; }

    /// <summary>تعداد ردیف‌های توزیع در هر وضعیت (پس از اجرا).</summary>
    public IReadOnlyList<DistributionStatusCountDto> DistributionCounts { get; init; } = [];

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// شمارش ردیف‌های توزیع بر اساس وضعیت.
/// </summary>
public sealed record DistributionStatusCountDto
{
    public DistributionStatus Status { get; init; }
    public int Count { get; init; }
}

/// <summary>
/// خلاصه‌ی کمپین برای فهرست.
/// </summary>
public sealed record CampaignSummaryDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public CampaignStatus Status { get; init; }
    public string Title { get; init; } = string.Empty;
    public string SurveyCode { get; init; } = string.Empty;
    public TargetAudienceType AudienceType { get; init; }
    public DistributionChannel Channel { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public DateTime? StartedAt { get; init; }

    /// <summary>تعداد کل ردیف‌های توزیع (پس از اجرا) — برای نمایش در فهرست.</summary>
    public int TotalDistributions { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// درخواست ایجاد یا ویرایش یک کمپین.
/// </summary>
public sealed record SaveCampaignRequest
{
    public string Code { get; init; } = string.Empty;
    public Guid SurveyId { get; init; }
    public TargetAudienceType AudienceType { get; init; } = TargetAudienceType.AllCompany;
    public bool IncludeInactiveEmployees { get; init; }
    public DistributionChannel Channel { get; init; } = DistributionChannel.Email;

    /// <summary>زمان شروع برنامه‌ریزی‌شده (UTC) یا خالی برای اجرای فوری.</summary>
    public DateTime? ScheduledAt { get; init; }

    /// <summary>مهلت نهایی پاسخ‌گویی (UTC) یا خالی.</summary>
    public DateTime? EndsAt { get; init; }

    public IReadOnlyList<CampaignLocalizationDto> Localizations { get; init; } = [];
    public IReadOnlyList<Guid> TargetOrgUnitIds { get; init; } = [];
    public IReadOnlyList<Guid> TargetEmployeeIds { get; init; } = [];
    public IReadOnlyList<SaveReminderRequest> Reminders { get; init; } = [];
}

/// <summary>فیلتر جستجوی کمپین‌ها.</summary>
public sealed record CampaignSearchRequest
{
    public string? SearchText { get; init; }
    public CampaignStatus? Status { get; init; }
    public Guid? SurveyId { get; init; }
    public bool IncludeArchived { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>فیلتر جستجوی ردیف‌های توزیع.</summary>
public sealed record DistributionSearchRequest
{
    public DistributionStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// نتیجه‌ی اجرای یک کمپین.
/// </summary>
public sealed record CampaignLaunchResultDto
{
    public Guid CampaignId { get; init; }
    public string Code { get; init; } = string.Empty;
    public CampaignStatus Status { get; init; }

    /// <summary>تعداد گیرندگان عضو جمعیت هدف.</summary>
    public int ResolvedRecipientCount { get; init; }

    /// <summary>تعداد ردیف‌های توزیعِ ساخته‌شده (تفاوت با بالا یعنی گیرنده‌ی تکراری).</summary>
    public int CreatedDistributionCount { get; init; }
}

/// <summary>
/// نتیجه‌ی پردازش یادآورهای سررسیده.
/// </summary>
public sealed record ReminderProcessResultDto
{
    /// <summary>تعداد یادآورهایی که به‌عنوان ارسال‌شده علامت خوردند.</summary>
    public int RemindersProcessed { get; init; }

    /// <summary>تعداد گیرندگانی که شمارنده‌ی یادآورشان افزایش یافت.</summary>
    public int RecipientsNotified { get; init; }
}
