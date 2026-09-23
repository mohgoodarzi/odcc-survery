using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.QuestionBank.Entities;

/// <summary>
/// برچسب یک سؤال برای دسته‌بندی و جستجو (مثلاً «رضایت‌سنجی»، «محیط کار»).
/// </summary>
public class QuestionTag : BaseEntity
{
    /// <summary>شناسه‌ی سؤال والد.</summary>
    public Guid QuestionId { get; set; }

    /// <summary>نام برچسب.</summary>
    public string Name { get; set; } = string.Empty;
}
