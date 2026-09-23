using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Questionnaire.Enums;

namespace ODCC.Domain.Modules.Questionnaire.Entities;

/// <summary>
/// یک سؤال داخل یک پرسشنامه: ارجاع به یک نسخه‌ی مشخص از سؤال کتابخانه
/// به‌همراه تنظیمات اختصاصی این پرسشنامه (اجبار، ترتیب، عنوان جایگزین).
///
/// **چسبیدن به نسخه (<see cref="QuestionVersionNumber"/>):** ساختار پاسخ‌گویی
/// باید در طول عمر نظرسنجی ثابت بماند. اگر سؤال در کتابخانه بعداً ویرایش شود،
/// این آیتم همچنان نسخه‌ای را که با آن منتشر شده نمایش می‌دهد.
/// </summary>
public class QuestionnaireItem : BaseEntity
{
    /// <summary>شناسه‌ی بخش والد.</summary>
    public Guid SectionId { get; set; }

    /// <summary>شناسه‌ی سؤال در کتابخانه‌ی سؤالات.</summary>
    public Guid QuestionId { get; set; }

    /// <summary>نسخه‌ی سؤالی که این آیتم به آن چسبیده.</summary>
    public int QuestionVersionNumber { get; set; }

    /// <summary>کد سؤال (برای خوانایی گزارش‌ها — همگام با نسخه‌ی چسبیده).</summary>
    public string QuestionCode { get; set; } = string.Empty;

    /// <summary>نوع سؤال (همگام با نسخه‌ی چسبیده).</summary>
    public QuestionType QuestionType { get; set; }

    /// <summary>ترتیب نمایش در بخش.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>آیا پاسخ به این سؤال اجباری است؟</summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// عنوان جایگزین اختیاری برای این پرسشنامه (در صورت خالی بودن، متن سؤال نمایش داده می‌شود).
    /// </summary>
    public string? TitleOverride { get; set; }

    /// <summary>قوانین انشعابِ این آیتم (پاسخ به سؤال دیگر وابسته است).</summary>
    public List<BranchingRule> BranchingRules { get; set; } = [];

    /// <summary>افزودن یک قانون انشعاب.</summary>
    public void AddBranchRule(BranchingRule rule)
    {
        rule.ItemId = Id;
        BranchingRules.Add(rule);
    }

    /// <summary>حذف یک قانون انشعاب با شناسه.</summary>
    public bool RemoveBranchRule(Guid ruleId)
    {
        var rule = BranchingRules.FirstOrDefault(r => r.Id == ruleId);
        return rule is not null && BranchingRules.Remove(rule);
    }
}
