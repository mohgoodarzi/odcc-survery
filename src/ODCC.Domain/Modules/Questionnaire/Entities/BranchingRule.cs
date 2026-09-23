using ODCC.Domain.Common;
using ODCC.Domain.Modules.Questionnaire.Enums;

namespace ODCC.Domain.Modules.Questionnaire.Entities;

/// <summary>
/// قانون انشعاب: اگر پاسخ به آیتمِ مبدأ شرط <see cref="Condition"/> را نسبت به
/// <see cref="ExpectedValue"/> برآورده کرد، پاسخ‌گو به آیتم <see cref="TargetItemId"/>
/// هدایت می‌شود (پرش شرطی).
/// </summary>
public class BranchingRule : BaseEntity
{
    /// <summary>شناسه‌ی آیتم مبدأ (سؤالی که پاسخ آن شرط را ارزیابی می‌کند).</summary>
    public Guid ItemId { get; set; }

    /// <summary>شناسه‌ی آیتم مقصد (سؤالی که در صورت برآورده شدن شرط نمایش داده می‌شود).</summary>
    public Guid TargetItemId { get; set; }

    /// <summary>نوع شرط مقایسه.</summary>
    public BranchingCondition Condition { get; set; } = BranchingCondition.Equals;

    /// <summary>
    /// مقدار مورد انتظار: کد گزینه برای سؤال‌های گزینه‌ای، عدد برای سؤال‌های عددی/امتیازی،
    /// و متن برای سؤال‌های متنی.
    /// </summary>
    public string ExpectedValue { get; set; } = string.Empty;
}
