using System.ComponentModel.DataAnnotations.Schema;

namespace ODCC.Domain.Common;

/// <summary>
/// کلاس پایه‌ی تمام موجودیت‌های سامانه.
/// کلید اصلی همواره <see cref="Guid"/> است؛ در زمان ساخت با <c>Guid.CreateVersion7()</c>
/// تولید می‌شود تا هم تقدم زمانی داشته باشد و هم به‌صورت محلی و بدون هماهنگی مرکزی یکتا بماند.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>زمان ایجاد بر حسب UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>زمان آخرین تغییر بر حسب UTC.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// حذف نرم: ردیف در پایگاه داده باقی می‌ماند اما از نتایج فیلتر می‌شود.
    /// برای حفظ یکپارچگی ارجاعی و پیگیری ممیزی ضروری است.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// کنترل همزمانی خوش‌بینانه (Optimistic Concurrency).
    /// در صورت تغییر همزمان یک ردیف توسط دو کاربر، دومین عملیات با <c>DbUpdateConcurrencyException</c> رد می‌شود.
    /// </summary>
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];
}
