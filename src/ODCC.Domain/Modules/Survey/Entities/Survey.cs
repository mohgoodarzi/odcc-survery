using ODCC.Domain.Common;
using ODCC.Domain.Modules.Survey.Enums;

namespace ODCC.Domain.Modules.Survey.Entities;

/// <summary>
/// یک نظرسنجی: یک «نمونه‌ی در حال اجرا» از یک پرسشنامه‌ی منتشرشده به‌همراه
/// تنظیمات و چرخه‌ی عمر مستقل.
///
/// **چسبیدن به نسخه‌ی پرسشنامه (<see cref="QuestionnaireVersion"/>):** ساختار
/// پاسخ‌گویی باید در طول عمر نظرسنجی ثابت بماند. هنگام انتشار نظرسنجی، نسخه‌ی
/// فعلیِ پرسشنامه ثبت می‌شود و تغییرات بعدی روی پرسشنامه روی این نظرسنجی اثر
/// نمی‌گذارد. پرسشنامه‌ی ارجاع‌شده قابل تغییر نیست.
///
/// **ناشناس بودن (<see cref="IsAnonymous"/>):** وقتی فعال است، پیوند میان پاسخ و
/// پاسخ‌دهنده در ماژول پاسخ‌ها ذخیره نمی‌شود (در فاز پاسخ‌ها پیاده‌سازی می‌شود).
///
/// این موجودیتِ محتوایی است (مثل پرسشنامه) و دامنه‌ی سازمانی روی آن اعمال نمی‌شود.
/// </summary>
public class Survey : BaseEntity
{
    /// <summary>کد یکتای نظرسنجی، مثلاً «SV-ENG-2026».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>وضعیت چرخه‌ی عمر.</summary>
    public SurveyStatus Status { get; set; } = SurveyStatus.Draft;

    /// <summary>شناسه‌ی پرسشنامه‌ای که این نظرسنجی از آن تغذیه می‌شود.</summary>
    public Guid QuestionnaireId { get; set; }

    /// <summary>نسخه‌ی پرسشنامه در زمان انتشار (ساختار پاسخ‌گویی ثابت می‌ماند).</summary>
    public int QuestionnaireVersion { get; set; }

    /// <summary>کد پرسشنامه (برای خوانایی گزارش‌ها).</summary>
    public string QuestionnaireCode { get; set; } = string.Empty;

    /// <summary>قالبی که این نظرسنجی از آن ساخته شده (اختیاری).</summary>
    public Guid? TemplateId { get; set; }

    // --- تنظیمات -------------------------------------------------------------

    /// <summary>آیا پاسخ‌ها ناشناس جمع‌آوری می‌شوند؟</summary>
    public bool IsAnonymous { get; set; }

    /// <summary>آیا پاسخ‌دهنده می‌تواند پس از ارسال، پاسخ خود را ویرایش کند؟</summary>
    public bool AllowEditResponse { get; set; } = true;

    /// <summary>آیا نوار پیشرفت برای پاسخ‌دهنده نمایش داده می‌شود؟</summary>
    public bool ShowProgressBar { get; set; } = true;

    /// <summary>آیا هر کاربر فقط یک بار مجاز به پاسخ‌گویی است؟</summary>
    public bool SingleResponsePerUser { get; set; } = true;

    /// <summary>زمان باز شدن پنجره‌ی پاسخ‌گویی (UTC). <c>null</c> یعنی فوری پس از انتشار.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>زمان بسته شدن پنجره‌ی پاسخ‌گویی (UTC). <c>null</c> یعنی بدون تاریخ انقضا.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>مدت زمان تخمینی تکمیل نظرسنجی به دقیقه (برای نمایش به پاسخ‌دهنده).</summary>
    public int EstimatedMinutes { get; set; } = 5;

    // --- تاریخ‌های چرخه‌ی عمر --------------------------------------------------

    /// <summary>زمان انتشار (UTC).</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>زمان فعال‌شدن واقعی پنجره‌ی پاسخ‌گویی (UTC).</summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>زمان بسته شدن پنجره‌ی پاسخ‌گویی (UTC).</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>زمان بایگانی (UTC).</summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>ترجمه‌های عنوان، توضیح و پیام‌های نظرسنجی.</summary>
    public List<SurveyLocalization> Localizations { get; set; } = [];

    /// <summary>افزودن یا به‌روزرسانی ترجمه‌ی نظرسنجی.</summary>
    public void SetLocalization(
        Language language,
        string title,
        string? description,
        string? welcomeMessage,
        string? thankYouMessage)
    {
        var existing = Localizations.FirstOrDefault(l => l.Language == language);
        if (existing is null)
        {
            Localizations.Add(new SurveyLocalization
            {
                Language = language,
                Title = title,
                Description = description,
                WelcomeMessage = welcomeMessage,
                ThankYouMessage = thankYouMessage
            });
        }
        else
        {
            existing.Update(title, description, welcomeMessage, thankYouMessage);
        }
    }

    // --- ماشین وضعیت ----------------------------------------------------------

    /// <summary>آیا نظرسنجی برای انتشار آماده است؟ (حداقل یک ترجمه لازم است.)</summary>
    public bool IsPublishable => Localizations.Count > 0;

    /// <summary>آیا نظرسنجی در حال حاضر پاسخ می‌پذیرد؟</summary>
    public bool AcceptsResponses =>
        Status == SurveyStatus.Active
        && (EndDate is null || EndDate.Value > DateTime.UtcNow);

    /// <summary>انتشار نظرسنجی: اگر تاریخ شروع در آینده باشد «زمان‌بندی‌شده»، در غیر این
    /// این صورت «فعال» می‌شود. فقط از حالت پیش‌نویس مجاز است.</summary>
    public void Publish()
    {
        Status = StartDate is { } start && start > DateTime.UtcNow
            ? SurveyStatus.Scheduled
            : SurveyStatus.Active;

        PublishedAt = DateTime.UtcNow;
    }

    /// <summary>باز کردن پنجره‌ی پاسخ‌گویی (از حالت زمان‌بندی‌شده).</summary>
    public void Start()
    {
        Status = SurveyStatus.Active;
        ActivatedAt = DateTime.UtcNow;
    }

    /// <summary>توقف موقت پاسخ‌گویی.</summary>
    public void Pause() => Status = SurveyStatus.Paused;

    /// <summary>از سرگیری پاسخ‌گویی پس از توقف.</summary>
    public void Resume()
    {
        Status = SurveyStatus.Active;
        ActivatedAt ??= DateTime.UtcNow;
    }

    /// <summary>بستن پنجره‌ی پاسخ‌گویی برای همیشه.</summary>
    public void Close()
    {
        Status = SurveyStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }

    /// <summary>بایگانی نظرسنجی.</summary>
    public void Archive()
    {
        Status = SurveyStatus.Archived;
        ArchivedAt = DateTime.UtcNow;
    }
}
