using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Questionnaire.Entities;

/// <summary>
/// ترجمه‌ی عنوان یک بخش از پرسشنامه.
/// </summary>
public class SectionLocalization : Localization
{
    /// <summary>عنوان بخش.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>شناسه‌ی بخش والد.</summary>
    public Guid SectionId { get; set; }
}
