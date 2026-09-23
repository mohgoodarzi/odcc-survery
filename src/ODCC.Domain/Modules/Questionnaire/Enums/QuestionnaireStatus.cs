namespace ODCC.Domain.Modules.Questionnaire.Enums;

/// <summary>
/// وضعیت چرخه‌ی عمر پرسشنامه.
/// فقط پرسشنامه‌ی «فعال» می‌تواند در نظرسنجی‌ها استفاده شود.
/// </summary>
public enum QuestionnaireStatus
{
    /// <summary>پیش‌نویس — در حال تدوین، هنوز قابل استفاده نیست.</summary>
    Draft = 1,

    /// <summary>فعال — قابل استفاده در نظرسنجی‌ها.</summary>
    Active = 2,

    /// <summary>بایگانی — کنار گذاشته شده، اما پاسخ‌های قدیمی حفظ می‌شوند.</summary>
    Archived = 3
}
