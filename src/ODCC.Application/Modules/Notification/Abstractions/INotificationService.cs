using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Application.Modules.Notification.Abstractions;

/// <summary>
/// سرویس اعلان‌ها: ساخت، تحویل، جستجو و مدیریت صندوق ورودی کاربر جاری.
///
/// <b>مرز حریم خصوصی:</b> کاربر فقط اعلان‌های <i>خودش</i> را می‌بیند و فقط
/// می‌تواند آن‌ها را خوانده‌شده علامت بزند. enforcing این مرز در این سرویس
/// است (نه در کنترلر) تا هیچ مسیری دور نزند.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// ساخت یک اعلان و تحویل بلافاصله‌ی آن. این مسیر اصلی «ارسال اعلان» است.
    /// </summary>
    Task<Result<NotificationDto>> SendAsync(SendNotificationRequest request, CancellationToken ct = default);

    /// <summary>
    /// ساخت چندین اعلان به گیرندگان مختلف با یک قالب. بازگشت: تعداد ساخته‌شده.
    /// برای حجم بالا (دعوت‌نامه‌ی کمپین) بهینه است.
    /// </summary>
    Task<Result<int>> SendToManyAsync(SendNotificationsRequest request, CancellationToken ct = default);

    /// <summary>جستجوی اعلان‌ها (کاربر جاری فیلتر می‌شود مگر اینکه مجوز مدیریت باشد).</summary>
    Task<PagedResult<NotificationDto>> SearchAsync(NotificationSearchRequest request, CancellationToken ct = default);

    /// <summary>دریافت یک اعلان با شناسه (فقط مالک یا مدیر).</summary>
    Task<Result<NotificationDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>خوانده‌شده علامت زدن یک اعلان درون‌برنامه‌ای.</summary>
    Task<Result<NotificationDto>> MarkReadAsync(Guid id, CancellationToken ct = default);

    /// <summary>خوانده‌شده علامت زدن تمام اعلان‌های خوانده‌نشده‌ی کاربر جاری.</summary>
    Task<Result> MarkAllReadAsync(NotificationChannel? channel, CancellationToken ct = default);

    /// <summary>تعداد اعلان‌های خوانده‌نشده‌ی کاربر جاری.</summary>
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
}

/// <summary>
/// رابط کاربری پنل مدیریت قالب‌ها. نیاز به مجوز <c>notifications.manage</c> دارد.
/// </summary>
public interface INotificationTemplateService
{
    Task<PagedResult<NotificationTemplateDto>> SearchAsync(NotificationTemplateSearchRequest request, CancellationToken ct = default);

    Task<Result<NotificationTemplateDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<NotificationTemplateDto>> CreateAsync(SaveNotificationTemplateRequest request, CancellationToken ct = default);

    Task<Result<NotificationTemplateDto>> UpdateAsync(Guid id, SaveNotificationTemplateRequest request, CancellationToken ct = default);

    Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// ترجیحات تحویل کاربر جاری. هر کاربر فقط ترجیحات خودش را می‌بیند/تغییر می‌دهد.
/// </summary>
public interface INotificationPreferenceService
{
    /// <summary>دریافت ترجیحات کاربر جاری (ردیف‌های موجود + پیش‌فرض‌ها).</summary>
    Task<IReadOnlyList<NotificationPreferenceDto>> ListAsync(CancellationToken ct = default);

    /// <summary>به‌روزرسانی یک ترجیح ( Upsert).</summary>
    Task<Result<NotificationPreferenceDto>> UpdateAsync(UpdateNotificationPreferenceRequest request, CancellationToken ct = default);
}

/// <summary>
/// حل‌کننده‌ی گیرندگان: تبدیل «مجوز» یا «کارمند» به گیرنده‌ی اعلان. این قرارداد
/// فقط از طریق APIهای عمومی ماژول هویت/سازمان کار می‌کند، هرگز از DbContext آن‌ها.
/// </summary>
public interface INotificationRecipientResolver
{
    /// <summary>
    /// همه‌ی کاربران فعال که این مجوز را دارند (از طریق RoleClaims عمومی هویت).
    /// برای اعلان‌های سیستمی به مدیران (مثلاً هشدار افت NPS).
    /// </summary>
    Task<IReadOnlyList<NotificationRecipient>> ResolveByPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>حل‌کردن چند کارمند سازمانی به گیرنده (با ایمیل/شماره/نام).</summary>
    Task<IReadOnlyList<NotificationRecipient>> ResolveByEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken ct = default);
}

/// <summary>گیرنده‌ی حل‌شده با آدرس‌های تحویل.</summary>
public sealed record NotificationRecipient
{
    public Guid? UserId { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>آیا این گیرنده کانال درون‌برنامه‌ای دارد (کاربر سامانه است)؟</summary>
    public bool SupportsInApp => UserId is not null;

    /// <summary>آیا ایمیل قابل‌استفاده دارد؟</summary>
    public bool SupportsEmail => !string.IsNullOrWhiteSpace(Email);
}

/// <summary>
/// موتور پردازش اعلان‌های در صف: بررسی ترجیب، فراخوانی ارائه‌دهنده‌ی کانال و
/// به‌روزرسانی وضعیت (با امتحان مجدد نمایی). قابل استفاده هم به‌صورت همزمان
/// بعد از <c>SendAsync</c> و هم توسط زمان‌بند پس‌زمینه.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// پردازش یک دسته از اعلان‌های آماده. بازگشت: تعداد پردازش‌شده.
    /// </summary>
    Task<int> ProcessPendingAsync(int maxBatch, CancellationToken ct = default);
}

/// <summary>رندر قالب‌ها: تبدیل کد قالب + زبان + متغیرها به موضوع/بدنه.</summary>
public interface INotificationTemplateRenderer
{
    /// <summary>
    /// رندر با قالب فعال پایگاه داده؛ اگر نبود، از متن پیش‌فرض توکار استفاده
    /// می‌کند تا سامانه همیشه کار کند.
    /// </summary>
    Task<RenderedTemplate> RenderAsync(string templateCode, Language language, IReadOnlyDictionary<string, string?> properties, CancellationToken ct = default);
}

/// <summary>خروجی رندر.</summary>
public sealed record RenderedTemplate(string Subject, string Body);
