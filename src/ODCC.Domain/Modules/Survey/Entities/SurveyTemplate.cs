using ODCC.Domain.Common;
using ODCC.Domain.Modules.Survey.Enums;

namespace ODCC.Domain.Modules.Survey.Entities;

/// <summary>
/// یک قالب نظرسنجی: مجموعه‌ی قابل‌استفاده‌ی مجدد از «یک پرسشنامه + تنظیمات
/// ثابت» که ساخت نظرسنجی‌های تکراری را سریع می‌کند. تنظیمات قالب هنگام ساخت
/// نظرسنجی در آن کپی می‌شود (ارتباط دائمی وجود ندارد).
///
/// قالب‌ها موجودیت‌های محتوایی هستند و دامنه‌ی سازمانی روی آن‌ها اعمال نمی‌شود.
/// </summary>
public class SurveyTemplate : BaseEntity
{
    /// <summary>کد یکتای قالب، مثلاً «TPL-ENG-QUARTERLY».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>وضعیت قالب.</summary>
    public SurveyTemplateStatus Status { get; set; } = SurveyTemplateStatus.Active;

    /// <summary>شناسه‌ی پرسشنامه‌ی پایه‌ی این قالب.</summary>
    public Guid QuestionnaireId { get; set; }

    /// <summary>کد پرسشنامه‌ی پایه (برای خوانایی فهرست‌ها — همگام با پرسشنامه).</summary>
    public string QuestionnaireCode { get; set; } = string.Empty;

    // --- تنظیمات ثابت قالب ----------------------------------------------------

    public bool IsAnonymous { get; set; }
    public bool AllowEditResponse { get; set; } = true;
    public bool ShowProgressBar { get; set; } = true;
    public bool SingleResponsePerUser { get; set; } = true;
    public int EstimatedMinutes { get; set; } = 5;

    /// <summary>ترجمه‌های عنوان و توضیح قالب.</summary>
    public List<SurveyTemplateLocalization> Localizations { get; set; } = [];

    /// <summary>افزودن یا به‌روزرسانی ترجمه‌ی قالب.</summary>
    public void SetLocalization(Language language, string title, string? description)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);
        if (existing is null)
        {
            Localizations.Add(new SurveyTemplateLocalization
            {
                Language = language,
                Title = title,
                Description = description
            });
        }
        else
        {
            existing.Title = title;
            existing.Description = description;
        }
    }

    /// <summary>آیا از این قالب می‌توان نظرسنجی ساخت؟</summary>
    public bool IsUsable => Status == SurveyTemplateStatus.Active;

    /// <summary>بایگانی قالب.</summary>
    public void Archive() => Status = SurveyTemplateStatus.Archived;
}
