namespace ODCC.Domain.Modules.Survey.Enums;

/// <summary>
/// وضعیت چرخه‌ی عمر یک قالب نظرسنجی.
/// قالب‌ها ساده‌تر از خود نظرسنجی هستند: یا فعال (قابل استفاده برای ساخت نظرسنجی)
/// یا بایگانی‌شده.
/// </summary>
public enum SurveyTemplateStatus
{
    /// <summary>فعال — می‌توان از آن نظرسنجی جدید ساخت.</summary>
    Active = 1,

    /// <summary>بایگانی — دیگر قابل استفاده نیست، ولی نظرسنجی‌های ساخته‌شده از آن حفظ می‌شوند.</summary>
    Archived = 2
}
