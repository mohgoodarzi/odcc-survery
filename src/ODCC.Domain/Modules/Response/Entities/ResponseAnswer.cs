using System.Globalization;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Response.Enums;

namespace ODCC.Domain.Modules.Response.Entities;

/// <summary>
/// پاسخ به یک آیتم (سؤال) از پرسشنامه، داخل یک <see cref="ResponseSession"/>.
///
/// **ذخیره‌سازی بر اساس نوع سؤال:**
/// - سؤال‌های گزینه‌ای (<see cref="QuestionType.SingleChoice"/> و
///   <see cref="QuestionType.MultipleChoice"/>): انتخاب‌ها در
///   <see cref="Selections"/> (یکی یا چند گزینه).
/// - <see cref="QuestionType.Rating"/> و <see cref="QuestionType.Number"/>:
///   مقدار عددی در <see cref="NumericValue"/>.
/// - <see cref="QuestionType.YesNo"/>: «yes»/«no» در <see cref="TextValue"/>
///   (مستقل از زبان تا تحلیلات قابل‌شمارش بماند).
/// - سؤال‌های متنی: متن در <see cref="TextValue"/>.
///
/// نوع هر سؤال و کد آن از نسخه‌ی چسبیده‌ی پرسشنامه ثبت می‌شود تا پاسخ‌های
/// تاریخی حتی پس از تغییر کتابخانه‌ی سؤالات قابل تفسیر بمانند.
/// </summary>
public class ResponseAnswer : BaseEntity
{
    /// <summary>شناسه‌ی نشست والد.</summary>
    public Guid SessionId { get; set; }

    /// <summary>شناسه‌ی آیتم داخل پرسشنامه.</summary>
    public Guid QuestionnaireItemId { get; set; }

    /// <summary>شناسه‌ی سؤال در کتابخانه‌ی سؤالات.</summary>
    public Guid QuestionId { get; set; }

    /// <summary>کد سؤال (تصویر لحظه‌ای برای گزارش‌ها).</summary>
    public string QuestionCode { get; set; } = string.Empty;

    /// <summary>نوع سؤال (تصویر لحظه‌ای).</summary>
    public QuestionType QuestionType { get; set; }

    /// <summary>ترتیب نمایش آیتم در پرسشنامه.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>پاسخ متنی (سؤال‌های متنی و بله/خیر).</summary>
    public string? TextValue { get; set; }

    /// <summary>پاسخ عددی (امتیازدهی و عدد).</summary>
    public decimal? NumericValue { get; set; }

    /// <summary>گزینه‌های انتخاب‌شده (سؤال‌های گزینه‌ای).</summary>
    public List<ResponseAnswerSelection> Selections { get; set; } = [];

    /// <summary>آیا این پاسخ مقداری دارد؟</summary>
    public bool HasValue =>
        !string.IsNullOrWhiteSpace(TextValue)
        || NumericValue.HasValue
        || Selections.Count > 0;

    /// <summary>تنظیم پاسخ متنی (و پاک کردن سایر اشکال).</summary>
    public void SetText(string? text)
    {
        TextValue = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        NumericValue = null;
        Selections.Clear();
    }

    /// <summary>تنظیم پاسخ عددی (و پاک کردن سایر اشکال).</summary>
    public void SetNumber(decimal? value)
    {
        NumericValue = value;
        TextValue = null;
        Selections.Clear();
    }

    /// <summary>تنظیم پاسخ بله/خیر.</summary>
    public void SetYesNo(bool? value)
    {
        TextValue = value switch
        {
            true => "yes",
            false => "no",
            _ => null
        };
        NumericValue = null;
        Selections.Clear();
    }

    /// <summary>تنظیم گزینه‌های انتخاب‌شده (و پاک کردن سایر اشکال).</summary>
    public void SetSelections(IReadOnlyCollection<(Guid OptionId, string OptionCode, int DisplayOrder)> options)
    {
        Selections.Clear();

        foreach (var option in options)
        {
            Selections.Add(new ResponseAnswerSelection
            {
                AnswerId = Id,
                OptionId = option.OptionId,
                OptionCode = option.OptionCode,
                DisplayOrder = option.DisplayOrder
            });
        }

        TextValue = null;
        NumericValue = null;
    }

    /// <summary>
    /// مهم‌ترین مقدار پاسخ به‌صورت متن (برای نمایش فشرده در فهرست‌ها و تحلیلات).
    /// </summary>
    public string ToDisplayString()
    {
        if (Selections.Count > 0)
        {
            return string.Join(", ", Selections.OrderBy(s => s.DisplayOrder).Select(s => s.OptionCode));
        }

        if (NumericValue.HasValue)
        {
            return NumericValue.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return TextValue ?? string.Empty;
    }
}
