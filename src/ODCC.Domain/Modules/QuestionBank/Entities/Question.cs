using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;

namespace ODCC.Domain.Modules.QuestionBank.Entities;

/// <summary>
/// یک سؤال قابل‌استفاده‌ی مجدد در کتابخانه‌ی سؤالات.
///
/// سؤال‌ها موجودیت‌های محتوایی هستند (قالب)، نه داده‌ی متعلق به یک واحد سازمانی؛
/// بنابراین دامنه‌ی سازمانی روی آن‌ها اعمال نمی‌شود. هر سؤال می‌تواند چندین
/// ترجمه (فارسی/انگلیسی) و چندین گزینه داشته باشد. گزینه‌ها نیز قابل‌ترجمه هستند.
///
/// **نسخه‌برداری:** متن، توضیح و گزینه‌های سؤال در <see cref="CurrentVersionNumber"/>
/// نگه‌ری می‌شوند. در زمان ویرایش محتوای سؤال (متن/گزینه‌ها/نوع) نسخه‌ی جدیدی
/// در <c>question_versions</c> ثبت می‌شود تا پرسشنامه‌هایی که به نسخه‌ی قبلی
/// ارجاع داده‌اند، ساختار خود را حفظ کنند (ثبات داده در طول زمان).
/// </summary>
public class Question : BaseEntity
{
    /// <summary>کد یکتای سؤال در کتابخانه، مثلاً «QB-ENG-001».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>نوع سؤال — نحوه‌ی پاسخ‌گویی را تعیین می‌کند.</summary>
    public QuestionType Type { get; set; }

    /// <summary>
    /// حداکثر طیف برای سؤال‌های امتیازدهی (مثلاً ۵ برای طیف ۱ تا ۵).
    /// فقط برای <see cref="QuestionType.Rating"/> معنا دارد؛ برای سایر انواع ۵ است.
    /// </summary>
    public int ScaleMax { get; set; } = 5;

    /// <summary>آیا سؤال بایگانی شده و از پرسشنامه‌های جدید کنار گذاشته شده است؟</summary>
    public bool IsArchived { get; set; }

    /// <summary>شماره‌ی نسخه‌ی فعلی محتوای سؤال. از ۱ شروع می‌شود.</summary>
    public int CurrentVersionNumber { get; set; } = 1;

    /// <summary>ترجمه‌های متن سؤال.</summary>
    public List<QuestionLocalization> Localizations { get; set; } = [];

    /// <summary>گزینه‌های سؤال (فقط برای انواع گزینه‌ای).</summary>
    public List<QuestionOption> Options { get; set; } = [];

    /// <summary>برچسب‌های سؤال (برای جستجو و دسته‌بندی).</summary>
    public List<QuestionTag> Tags { get; set; } = [];

    /// <summary>تاریخچه‌ی نسخه‌های سؤال.</summary>
    public List<QuestionVersion> Versions { get; set; } = [];

    /// <summary>افزودن یا به‌روزرسانی یک ترجمه.</summary>
    public void SetLocalization(Language language, string text, string? description)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);
        if (existing is null)
        {
            Localizations.Add(new QuestionLocalization
            {
                Language = language,
                Text = text,
                Description = description
            });
        }
        else
        {
            existing.Text = text;
            existing.Description = description;
        }
    }

    /// <summary>افزودن یک گزینه جدید با کد یکتا در سطح این سؤال.</summary>
    public void AddOption(string code, string text, int displayOrder, Language language)
    {
        Options.Add(new QuestionOption
        {
            Code = code,
            DisplayOrder = displayOrder
        });

        Options[^1].SetLocalization(language, text);
    }

    /// <summary>حذف یک گزینه با شناسه.</summary>
    public bool RemoveOption(Guid optionId)
    {
        var option = Options.FirstOrDefault(o => o.Id == optionId);
        return option is not null && Options.Remove(option);
    }

    /// <summary>افزودن یک برچسب (در صورت نبودن).</summary>
    public void AddTag(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed) || Tags.Any(t => t.Name == trimmed))
        {
            return;
        }

        Tags.Add(new QuestionTag { Name = trimmed });
    }

    /// <summary>حذف یک برچسب با نام.</summary>
    public void RemoveTag(string name)
    {
        var tag = Tags.FirstOrDefault(t => t.Name == name);
        if (tag is not null)
        {
            Tags.Remove(tag);
        }
    }

    /// <summary>بایگانی کردن سؤال (کنار گذاشتن از پرسشنامه‌های جدید).</summary>
    public void Archive() => IsArchived = true;

    /// <summary>آیا سؤال در نسخه‌ی فعلی حداقل یک ترجمه‌ی متن دارد؟</summary>
    public bool HasText => Localizations.Count > 0;

    /// <summary>
    /// آیا محتوای این سؤال در نسخه‌ی فعلی قابل‌استفاده است؟
    /// سؤال گزینه‌ای باید حداقل دو گزینه داشته باشد تا معنا پیدا کند.
    /// </summary>
    public bool IsContentValid => HasText && (!Type.HasOptions() || Options.Count >= 2);
}
