using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Domain.Modules.Notification.Entities;

/// <summary>
/// ترجیح تحویل یک کاربر: آیا دریافت اعلان از یک کانال (و اختیاریاً یک دسته)
/// مجاز است؟
///
/// این موجودیت «انصراف» (opt-out) را پیاده می‌کند: ردیفی وجود ندارد یعنی
/// مجاز؛ ردیف با <see cref="IsEnabled"/> == false یعنی غیرفعال. dispatcher قبل
/// از هر تحویلی این ترجیح را بررسی می‌کند و در صورت غیرفعال بودن، اعلان را
/// به‌جای ارسال، <c>Suppressed</c> علامت می‌زند — تا تاریخچه کامل بماند.
///
/// <b>نکته:</b> اعلان‌های سیستمی/حیاتی (دسته‌ی <see cref="NotificationCategory.General"/>)
/// را نمی‌توان خاموش کرد؛ آن‌ها همیشه تحویل داده می‌شوند.
/// </summary>
public sealed class NotificationPreference : BaseEntity
{
    /// <summary>کاربر مالک ترجیح.</summary>
    public Guid UserId { get; set; }

    /// <summary>کانالی که ترجیح برای آن است.</summary>
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    /// <summary>
    /// دسته‌ای که ترجیح برای آن است. <c>null</c> یعنی این ترجیح به تمام دسته‌ها
    /// (به‌جز سیستمی) اعمال می‌شود.
    /// </summary>
    public NotificationCategory? Category { get; set; }

    /// <summary>آیا تحویل مجاز است؟ پیش‌فرض <c>true</c> است.</summary>
    public bool IsEnabled { get; set; } = true;
}
