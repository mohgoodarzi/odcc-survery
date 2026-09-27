using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Modules.Notification.Enums;
// نام «Notification» هم یک namespace و هم یک موجودیت است؛ بدون نام‌مستعار،
// کامپایلر namespace را ترجیح می‌دهد (همان الگوی ماژول کمپین برای Campaign).
using NotificationEntity = ODCC.Domain.Modules.Notification.Entities.Notification;
using NotificationTemplateEntity = ODCC.Domain.Modules.Notification.Entities.NotificationTemplate;
using NotificationPreferenceEntity = ODCC.Domain.Modules.Notification.Entities.NotificationPreference;

namespace ODCC.Application.Modules.Notification.Abstractions;

/// <summary>
/// مخزن اختصاصی ماژول اعلان‌ها. فقط این ماژول باید آن را تزریق بگیرد.
/// </summary>
public interface INotificationRepository : IRepository<NotificationEntity>
{
    /// <summary>جستجوی صفحه‌بندی‌شده بر اساس فیلترها (کاربر جاری در سرویس اعمال می‌شود).</summary>
    Task<IReadOnlyList<NotificationEntity>> SearchAsync(NotificationSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد جستجوی صفحه‌بندی‌شده.</summary>
    Task<int> CountAsync(NotificationSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد اعلان‌های درون‌برنامه‌ای خوانده‌نشده‌ی یک کاربر.</summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// اعلان‌های درون‌برنامه‌ای اخلیف یک کاربر برای نمایش در نوار بالای برنامه.
    /// </summary>
    Task<IReadOnlyList<NotificationEntity>> ListRecentInAppAsync(Guid userId, int take, CancellationToken ct = default);

    /// <summary>
    /// شناسه‌ی تمام اعلان‌های درون‌برنامه‌ای خوانده‌نشده‌ی یک کاربر (برای
    /// «همه را خوانده‌شده علامت بزن»).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListUnreadIdsAsync(Guid userId, NotificationChannel? channel, CancellationToken ct = default);

    /// <summary>افزودن دسته‌ای از اعلان‌ها (برای حجم بالای دعوت‌نامه‌ها).</summary>
    Task AddRangeAsync(IReadOnlyCollection<NotificationEntity> entities, CancellationToken ct = default);

    /// <summary>
    /// گرفتن اعلان برای تحویل: tracked (نه AsNoTracking) تا تغییر وضعیت مستقیم
    /// ذخیره شود. قفل سطری با RowVersion (optimistic concurrency) تضمین می‌شود
    /// که دو پردازش همزمان یک اعلان را دو بار نفرستند.
    /// </summary>
    Task<NotificationEntity?> GetForDeliveryAsync(Guid id, CancellationToken ct = default);

    /// <summary>لیست اعلان‌های آماده‌ی تحویل (Pending و سررسیده).</summary>
    Task<IReadOnlyList<NotificationEntity>> ListPendingAsync(int maxBatch, CancellationToken ct = default);

    /// <summary>
    /// جستجوی اعلان‌ها بر اساس نوع و شناسه‌ی منبع (برای پیوند بین اعلان و
    /// موجودیت مبدأ، مثلاً ردیف توزیع کمپین).
    /// </summary>
    Task<IReadOnlyList<NotificationEntity>> ListBySourceAsync(string sourceType, IReadOnlyCollection<Guid> sourceIds, CancellationToken ct = default);
}

/// <summary>مخزن قالب‌های اعلان.</summary>
public interface INotificationTemplateRepository : IRepository<NotificationTemplateEntity>
{
    /// <summary>گرفتن قالب فعال با کد (یا <c>null</c> اگر غیرفعال/موجود نباشد).</summary>
    Task<NotificationTemplateEntity?> FindActiveByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>وجود داشتن قالب با این کد (برای یکتایی کد).</summary>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationTemplateEntity>> SearchAsync(NotificationTemplateSearchRequest request, CancellationToken ct = default);

    Task<int> CountAsync(NotificationTemplateSearchRequest request, CancellationToken ct = default);
}

/// <summary>مخزن ترجیحات تحویل کاربران.</summary>
public interface INotificationPreferenceRepository : IRepository<NotificationPreferenceEntity>
{
    /// <summary>ترجیح خاص (کانام + دسته) یا <c>null</c> اگر تعریف نشده (= مجاز).</summary>
    Task<NotificationPreferenceEntity?> FindAsync(Guid userId, NotificationChannel channel, NotificationCategory? category, CancellationToken ct = default);

    /// <summary>تمام ترجیحات یک کاربر.</summary>
    Task<IReadOnlyList<NotificationPreferenceEntity>> ListByUserAsync(Guid userId, CancellationToken ct = default);
}
