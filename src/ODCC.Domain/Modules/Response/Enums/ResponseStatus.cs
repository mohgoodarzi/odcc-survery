namespace ODCC.Domain.Modules.Response.Enums;

/// <summary>
/// وضعیت چرخه‌ی عمر یک پاسخ (نشست پاسخ‌گویی).
/// </summary>
public enum ResponseStatus
{
    /// <summary>در حال تکمیل (ذخیره‌ی جزئی شده ولی هنوز ارسال نشده).</summary>
    InProgress = 1,

    /// <summary>ارسال نهایی شده (غیرقابل تغییر مگر اینکه نظرسنجی اجازه‌ی ویرایش دهد).</summary>
    Submitted = 2
}
