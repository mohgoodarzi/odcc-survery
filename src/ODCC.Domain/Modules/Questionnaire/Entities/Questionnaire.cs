using System.ComponentModel.DataAnnotations.Schema;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Questionnaire.Enums;

namespace ODCC.Domain.Modules.Questionnaire.Entities;

/// <summary>
/// یک پرسشنامه: مجموعه‌ای ساختاریافته از بخش‌ها و سؤالات که از کتابخانه‌ی سؤالات
/// تغذیه می‌شود.
///
/// پرسشنامه‌ها موجودیت‌های محتوایی هستند (قالب نظرسنجی)، نه داده‌ی متعلق به یک
/// واحد سازمانی؛ بنابراین دامنه‌ی سازمانی روی آن‌ها اعمال نمی‌شود.
///
/// **ساختار:** پرسشنامه ← بخش‌ها ← آیتم‌ها ← قوانین انشعاب.
/// هر آیتم به یک نسخه‌ی مشخص از یک سؤال کتابخانه ارجاع می‌دهد تا ساختار
/// پاسخ‌گویی در طول عمر نظرسنجی ثابت بماند.
/// </summary>
public class Questionnaire : BaseEntity
{
    /// <summary>کد یکتای پرسشنامه، مثلاً «QS-ENG-2026».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>وضعیت چرخه‌ی عمر.</summary>
    public QuestionnaireStatus Status { get; set; } = QuestionnaireStatus.Draft;

    /// <summary>شماره‌ی نسخه‌ی پرسشنامه (با هر انتشار فعال یک واحد افزایش می‌یابد).</summary>
    public int Version { get; set; } = 1;

    /// <summary>ترجمه‌های عنوان و توضیح پرسشنامه.</summary>
    public List<QuestionnaireLocalization> Localizations { get; set; } = [];

    /// <summary>بخش‌های پرسشنامه.</summary>
    public List<Section> Sections { get; set; } = [];

    /// <summary>افزودن یا به‌روزرسانی ترجمه‌ی پرسشنامه.</summary>
    public void SetLocalization(Language language, string title, string? description)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);
        if (existing is null)
        {
            Localizations.Add(new QuestionnaireLocalization
            {
                Language = language,
                Title = title,
                Description = description
            });
        }
        else
        {
            existing.Update(title, description);
        }
    }

    /// <summary>افزودن یک بخش به انتهای پرسشنامه.</summary>
    public void AddSection(Section section)
    {
        section.QuestionnaireId = Id;
        section.DisplayOrder = Sections.Count == 0 ? 0 : Sections.Max(s => s.DisplayOrder) + 1;
        Sections.Add(section);
    }

    /// <summary>حذف یک بخش با شناسه.</summary>
    public bool RemoveSection(Guid sectionId)
    {
        var section = Sections.FirstOrDefault(s => s.Id == sectionId);
        return section is not null && Sections.Remove(section);
    }

    /// <summary>مرتب‌سازی مجدد بخش‌ها بر اساس ترتیب نمایش.</summary>
    public void ReorderSections()
    {
        for (var i = 0; i < Sections.Count; i++)
        {
            Sections[i].DisplayOrder = i;
        }
    }

    /// <summary>
    /// همه‌ی آیتم‌های پرسشنامه (از همه‌ی بخش‌ها).
    /// این یک ویژگی محاسبه‌شده‌ی سمت دامنه است، نه یک ناوبری در پایگاه داده،
    /// بنابراین از نگاشت آن توسط EF Core جلوگیری می‌شود تا یک کلید خارجیِ
    /// سایه‌ای بی‌معنی روی <c>questionnaire_items</c> ایجاد نشود.
    /// </summary>
    [NotMapped]
    public IEnumerable<QuestionnaireItem> AllItems => Sections.SelectMany(s => s.Items);

    /// <summary>
    /// انتشار پرسشنامه: تنها زمانی مجاز است که حداقل یک بخش با حداقل یک آیتم
    /// و حداقل یک ترجمه‌ی عنوان داشته باشد.
    /// </summary>
    public void Publish()
    {
        Status = QuestionnaireStatus.Active;
        Version++;
    }

    /// <summary>بایگانی پرسشنامه.</summary>
    public void Archive() => Status = QuestionnaireStatus.Archived;

    /// <summary>آیا پرسشنامه برای انتشار آماده است؟</summary>
    public bool IsPublishable =>
        Localizations.Count > 0
        && Sections.Count > 0
        && Sections.Any(s => s.Items.Count > 0);
}
