using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.QuestionBank.Entities;

/// <summary>
/// ترجمه‌ی متن یک گزینه‌ی پاسخ.
/// </summary>
public class QuestionOptionLocalization : Localization
{
    /// <summary>متن گزینه.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>شناسه‌ی گزینه والد.</summary>
    public Guid QuestionOptionId { get; set; }
}
