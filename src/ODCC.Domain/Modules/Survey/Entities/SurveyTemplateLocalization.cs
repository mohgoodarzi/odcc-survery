using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Survey.Entities;

/// <summary>
/// ترجمه‌ی عنوان و توضیح یک قالب نظرسنجی.
/// </summary>
public class SurveyTemplateLocalization : Localization
{
    /// <summary>عنوان قالب.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>توضیح قالب (اختیاری).</summary>
    public string? Description { get; set; }

    /// <summary>شناسه‌ی قالب والد.</summary>
    public Guid TemplateId { get; set; }
}
