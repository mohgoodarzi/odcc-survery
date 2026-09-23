using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.QuestionBank.Entities;

/// <summary>
/// یک گزینه‌ی پاسخ برای سؤال‌های تک‌انتخابی/چندانتخابی.
/// ترتیب نمایش با <see cref="DisplayOrder"/> کنترل می‌شود.
/// </summary>
public class QuestionOption : BaseEntity
{
    /// <summary>کد یکتای گزینه در سطح سؤال، مثلاً «A» یا «OPT-1».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>شناسه‌ی سؤال والد.</summary>
    public Guid QuestionId { get; set; }

    /// <summary>ترتیب نمایش.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>ترجمه‌های متن گزینه.</summary>
    public List<QuestionOptionLocalization> Localizations { get; set; } = [];

    /// <summary>افزودن یا به‌روزرسانی یک ترجمه.</summary>
    public void SetLocalization(Language language, string text)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);
        if (existing is null)
        {
            Localizations.Add(new QuestionOptionLocalization
            {
                Language = language,
                Text = text
            });
        }
        else
        {
            existing.Text = text;
        }
    }
}
