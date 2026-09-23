using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.QuestionBank.Entities;

/// <summary>
/// یک نسخه‌ی ثبت‌شده از محتوای سؤال.
///
/// هنگام ایجاد سؤال نسخه‌ی ۱ و هنگام تغییر نوع/متن/گزینه‌ها نسخه‌ی جدیدی ثبت
/// می‌شود. پرسشنامه‌ها به نسخه‌ی مشخصی ارجاع می‌دهند تا ساختار پاسخ‌گویی در
/// طول عمر نظرسنجی ثابت بماند — حتی اگر سؤال در کتابخانه بعداً ویرایش شود.
/// </summary>
public class QuestionVersion : BaseEntity
{
    /// <summary>شناسه‌ی سؤال والد.</summary>
    public Guid QuestionId { get; set; }

    /// <summary>شماره‌ی نسخه (۱، ۲، ۳، …).</summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// تصویر لحظه‌ای تعریف سؤال به‌صورت JSON سریالایز شده: متن‌ها، گزینه‌ها و نوع.
    /// این تصویر غیرقابل‌تغییر است و برای بازتولید پرسشنامه در زمان پاسخ‌گویی
    /// (و گزارش‌های بعدی) استفاده می‌شود.
    /// </summary>
    public string Snapshot { get; set; } = string.Empty;

    /// <summary>خلاصه‌ی تغییرات این نسخه نسبت به نسخه‌ی قبلی (اختیاری).</summary>
    public string? ChangeSummary { get; set; }

    /// <summary>شناسه‌ی کاربری که نسخه را ایجاد کرده (اختیاری).</summary>
    public Guid? CreatedByUserId { get; set; }
}
