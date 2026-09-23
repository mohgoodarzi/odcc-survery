using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.QuestionBank.Entities;

/// <summary>
/// ترجمه‌ی متن و توضیح یک سؤال به یک زبان.
/// </summary>
public class QuestionLocalization : Localization
{
    /// <summary>متن اصلی سؤال.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>توضیح کمکی (اختیاری).</summary>
    public string? Description { get; set; }

    /// <summary>شناسه‌ی سؤال والد.</summary>
    public Guid QuestionId { get; set; }

    /// <summary>به‌روزرسانی مقادیر ترجمه.</summary>
    public void Update(string text, string? description)
    {
        Text = text;
        Description = description;
    }
}
