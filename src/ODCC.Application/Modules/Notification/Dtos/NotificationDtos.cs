using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Application.Modules.Notification.Dtos;

// --- درخواست‌های ارسال -------------------------------------------------------

/// <summary>ارسال یک اعلان.</summary>
public sealed record SendNotificationRequest
{
    /// <summary>کد قالب (مثلاً <c>campaign_invitation</c>).</summary>
    public required string TemplateCode { get; init; }

    public NotificationChannel Channel { get; init; } = NotificationChannel.InApp;

    public NotificationCategory Category { get; init; } = NotificationCategory.General;

    public Language Language { get; init; } = Language.Fa;

    /// <summary>گیرنده (کاربر سامانه).</summary>
    public required NotificationRecipientDto Recipient { get; init; }

    /// <summary>متغیرهای جایگزین‌شونده در قالب (مثلاً <c>recipient_name</c>).</summary>
    public IReadOnlyDictionary<string, string?> Properties { get; init; } = new Dictionary<string, string?>();

    public string? SourceType { get; init; }

    public Guid? SourceId { get; init; }

    /// <summary>مسیر deep-link نسبی (بدون پیشوند فرهنگ).</summary>
    public string? Url { get; init; }
}

/// <summary>ارسال چندین اعلان با یک قالب (برای حجم بالا).</summary>
public sealed record SendNotificationsRequest
{
    public required string TemplateCode { get; init; }

    public NotificationChannel Channel { get; init; } = NotificationChannel.InApp;

    public NotificationCategory Category { get; init; } = NotificationCategory.General;

    public Language Language { get; init; } = Language.Fa;

    public required IReadOnlyCollection<NotificationRecipientDto> Recipients { get; init; }

    /// <summary>متغیرهای مشترک بین همه‌ی گیرندگان.</summary>
    public IReadOnlyDictionary<string, string?> Properties { get; init; } = new Dictionary<string, string?>();

    public string? SourceType { get; init; }

    /// <summary>در حالت چندگانه، شناسه‌ی منبع به‌ازای هر گیرنده از <see cref="NotificationRecipientDto.SourceId"/> می‌آید.</summary>
    public string? Url { get; init; }
}

/// <summary>یک گیرنده در درخواست ارسال.</summary>
public sealed record NotificationRecipientDto
{
    public Guid? UserId { get; init; }

    /// <summary>شناسه‌ی کارمند سازمانی (در صورت وجود).</summary>
    public Guid? EmployeeId { get; init; }

    public string? Name { get; init; }

    /// <summary>آدرس ایمیل — اگر خالی باشد، از پروفایل کاربر حل می‌شود.</summary>
    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>متغیرهای اختصاصی این گیرنده.</summary>
    public IReadOnlyDictionary<string, string?> Properties { get; init; } = new Dictionary<string, string?>();

    /// <summary>شناسه‌ی موجودیت مبدأ به‌ازای این گیرنده (مثلاً شناسه‌ی ردیف توزیع).</summary>
    public Guid? SourceId { get; init; }
}

// --- جستجو --------------------------------------------------------------------

public sealed record NotificationSearchRequest
{
    public NotificationChannel? Channel { get; init; }
    public NotificationCategory? Category { get; init; }
    public NotificationStatus? Status { get; init; }

    /// <summary>فقط خوانده‌نشده‌ها؟</summary>
    public bool? UnreadOnly { get; init; }

    /// <summary>فیلتر روی کاربر (فقط مدیران می‌توانند غیر از خودشان را ببینند).</summary>
    public Guid? RecipientUserId { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record NotificationTemplateSearchRequest
{
    public string? SearchText { get; init; }
    public NotificationChannel? Channel { get; init; }
    public bool IncludeArchived { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

// --- خروجی‌ها ------------------------------------------------------------------

public sealed record NotificationDto
{
    public Guid Id { get; init; }
    public Guid? RecipientUserId { get; init; }
    public string? RecipientName { get; init; }
    public NotificationChannel Channel { get; init; }
    public NotificationCategory Category { get; init; }
    public NotificationStatus Status { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public Language Language { get; init; }

    public DateTime? NextTryAt { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public int RetryCount { get; init; }
    public string? LastError { get; init; }

    public string? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public string? Url { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>آیا خوانده‌نشده است (فقط معنا برای کانال درون‌برنامه‌ای)؟</summary>
    public bool IsUnread { get; init; }
}

public sealed record NotificationTemplateDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public NotificationChannel Channel { get; init; }
    public NotificationCategory Category { get; init; }
    public bool IsActive { get; init; }

    public IReadOnlyList<NotificationTemplateLocalizationDto> Localizations { get; init; } = [];

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record NotificationTemplateLocalizationDto
{
    public Language Language { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

public sealed record NotificationPreferenceDto
{
    public Guid Id { get; init; }
    public NotificationChannel Channel { get; init; }

    /// <summary><c>null</c> یعنی این ترجیح به تمام دسته‌ها اعمال می‌شود.</summary>
    public NotificationCategory? Category { get; init; }

    public bool IsEnabled { get; init; }

    /// <summary>آیا این یک ردیف پیش‌فرض است (در پایگاه داده نیست)؟</summary>
    public bool IsDefault { get; init; }
}

// --- درخواست‌های مدیریت -------------------------------------------------------

public sealed record SaveNotificationTemplateRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public NotificationChannel Channel { get; init; } = NotificationChannel.InApp;
    public NotificationCategory Category { get; init; } = NotificationCategory.General;
    public bool IsActive { get; init; } = true;

    /// <summary>حداقل یک زبان لازم است.</summary>
    public required IReadOnlyList<SaveTemplateLocalizationRequest> Localizations { get; init; }
}

public sealed record SaveTemplateLocalizationRequest
{
    public Language Language { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

public sealed record UpdateNotificationPreferenceRequest
{
    public NotificationChannel Channel { get; init; }

    /// <summary><c>null</c> یعنی ترجیح عمومی (تمام دسته‌ها).</summary>
    public NotificationCategory? Category { get; init; }

    public bool IsEnabled { get; init; }
}
