using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Response.Entities;

/// <summary>
/// یک گزینه‌ی انتخاب‌شده در پاسخ یک سؤال گزینه‌ای (تک‌انتخابی/چندانتخابی).
/// شناسه و کد گزینه در زمان ثبت ثبت می‌شوند تا حتی اگر گزینه بعداً در کتابخانه‌ی
/// سؤالات تغییر کرد، پاسخ تاریخی معنا خود را حفظ کند.
/// </summary>
public class ResponseAnswerSelection : BaseEntity
{
    /// <summary>شناسه‌ی پاسخی که این انتخاب به آن تعلق دارد.</summary>
    public Guid AnswerId { get; set; }

    /// <summary>شناسه‌ی گزینه در کتابخانه‌ی سؤالات.</summary>
    public Guid OptionId { get; set; }

    /// <summary>کد گزینه (تصویر لحظه‌ای برای تاریخچه).</summary>
    public string OptionCode { get; set; } = string.Empty;

    /// <summary>ترتیب نمایش گزینه در زمان پاسخ‌گویی.</summary>
    public int DisplayOrder { get; set; }
}
