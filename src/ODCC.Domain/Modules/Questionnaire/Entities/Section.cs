using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Questionnaire.Entities;

/// <summary>
/// یک بخش (گروه) از پرسشنامه. سؤالات آیتم‌ها درون بخش‌ها قرار می‌گیرند
/// تا پرسشنامه‌های طولانی به صفحه‌های منطقی تقسیم شوند.
/// </summary>
public class Section : BaseEntity
{
    /// <summary>شناسه‌ی پرسشنامه والد.</summary>
    public Guid QuestionnaireId { get; set; }

    /// <summary>ترتیب نمایش بخش در پرسشنامه.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>آیا این بخش اختیاری است (قابل پرش)؟</summary>
    public bool IsOptional { get; set; }

    /// <summary>ترجمه‌های عنوان بخش.</summary>
    public List<SectionLocalization> Localizations { get; set; } = [];

    /// <summary>آیتم‌های این بخش.</summary>
    public List<QuestionnaireItem> Items { get; set; } = [];

    /// <summary>افزودن یا به‌روزرسانی ترجمه‌ی بخش.</summary>
    public void SetLocalization(Language language, string title)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);
        if (existing is null)
        {
            Localizations.Add(new SectionLocalization
            {
                Language = language,
                Title = title
            });
        }
        else
        {
            existing.Title = title;
        }
    }

    /// <summary>افزودن یک آیتم به انتهای بخش.</summary>
    public void AddItem(QuestionnaireItem item)
    {
        item.SectionId = Id;
        item.DisplayOrder = Items.Count == 0 ? 0 : Items.Max(i => i.DisplayOrder) + 1;
        Items.Add(item);
    }

    /// <summary>حذف یک آیتم با شناسه.</summary>
    public bool RemoveItem(Guid itemId)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        return item is not null && Items.Remove(item);
    }

    /// <summary>مرتب‌سازی مجدد آیتم‌های بخش.</summary>
    public void ReorderItems()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].DisplayOrder = i;
        }
    }
}
