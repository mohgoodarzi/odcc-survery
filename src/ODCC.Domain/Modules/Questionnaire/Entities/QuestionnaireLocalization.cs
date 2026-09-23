using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Questionnaire.Entities;

/// <summary>
/// ترجمه‌ی عنوان و توضیح یک پرسشنامه.
/// </summary>
public class QuestionnaireLocalization : Localization
{
    /// <summary>عنوان پرسشنامه.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>توضیح پرسشنامه (اختیاری).</summary>
    public string? Description { get; set; }

    /// <summary>شناسه‌ی پرسشنامه والد.</summary>
    public Guid QuestionnaireId { get; set; }

    /// <summary>به‌روزرسانی مقادیر ترجمه.</summary>
    public void Update(string title, string? description)
    {
        Title = title;
        Description = description;
    }
}
