namespace ODCC.Domain.Modules.QuestionBank.Enums;

/// <summary>
/// نوع سؤال. تعیین‌کننده‌ی نحوه‌ی پاسخ‌گویی و اعتبارسنجی پاسخ است.
/// مقادیر به‌صورت <c>int</c> ذخیره می‌شوند تا افزودن نوع جدید در آینده
/// فقط افزودن یک عضو اینجا باشد و ردیف‌های موجود نامعتبر نشوند.
/// </summary>
public enum QuestionType
{
    /// <summary>تک‌انتخابی از میان گزینه‌ها.</summary>
    SingleChoice = 1,

    /// <summary>چندانتخابی از میان گزینه‌ها.</summary>
    MultipleChoice = 2,

    /// <summary>امتیازدهی روی طیف (مثلاً ۱ تا ۵).</summary>
    Rating = 3,

    /// <summary>بله/خیر.</summary>
    YesNo = 4,

    /// <summary>متن کوتاه (یک خط).</summary>
    ShortText = 5,

    /// <summary>متن بلند (پاراگراف).</summary>
    LongText = 6,

    /// <summary>عدد.</summary>
    Number = 7
}

/// <summary>
/// ابزار کمکی برای تشخیص سؤال‌های گزینه‌ای.
/// </summary>
public static class QuestionTypeExtensions
{
    /// <summary>آیا این نوع سؤال نیاز به جدول گزینه‌ها دارد؟</summary>
    public static bool HasOptions(this QuestionType type) =>
        type is QuestionType.SingleChoice or QuestionType.MultipleChoice;

    /// <summary>آیا این نوع سؤال پاسخ متنی می‌پذیرد؟</summary>
    public static bool IsTextual(this QuestionType type) =>
        type is QuestionType.ShortText or QuestionType.LongText;
}
