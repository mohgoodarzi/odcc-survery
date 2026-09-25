using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Response.Enums;

namespace ODCC.Domain.Modules.Response.Entities;

/// <summary>
/// نشست پاسخ‌گویی یک کاربر به یک نظرسنجی: چرخه‌ی عمر «شروع → ذخیره‌ی جزئی →
/// ارسال»، سیاست‌های ناشناس بودن و پاسخ یگانه، و پاسخ‌های جزئی.
///
    /// **ناشناس بودن (<see cref="IsAnonymous"/>):** وقتی نظرسنجی ناشناس است،
    /// <b>هیچ‌کدام</b> از شناسه‌های پاسخ‌گو (کاربر، کارمند یا نام) در این جدول
    /// ذخیره نمی‌شوند؛ پیوند میان پاسخ و پاسخ‌دهنده به‌صورت عمدی قطع می‌شود.
    /// در این حالت شناسه‌ی توزیع (دعوت‌نامه) فقط به‌صورت گذرا در رویداد دامنه
    /// منتقل می‌شود تا کمپین بداند دعوت‌نامه پاسخ داده شده، ولی جایی ثبت نمی‌شود.
    ///
    /// **پاسخ یگانه (<c>SingleResponsePerUser</c> در تنظیمات نظرسنجی):** وقتی فعال
    /// است، هر کاربر فقط یک نشست ارسالشده برای هر نظرسنجی می‌تواند داشته داشت. اگر
    /// <c>AllowEditResponse</c> هم فعال باشد، کاربر می‌تواند پاسخ ارسال‌شده‌ی
    /// خود را باز کرده و ویرایش کند؛ در غیر این صورت ارسال دوم رد می‌شود.
///
/// این موجودیت محتوایی است و دامنه‌ی سازمانی روی آن اعمال نمی‌شود.
/// </summary>
public class ResponseSession : BaseEntity
{
    /// <summary>شناسه‌ی نظرسنجی.</summary>
    public Guid SurveyId { get; set; }

    /// <summary>کد نظرسنجی (تصویر لحظه‌ای برای گزارش‌ها).</summary>
    public string SurveyCode { get; set; } = string.Empty;

    /// <summary>شناسه‌ی کمپینی که این پاسخ از طریق آن رسیده (اختیاری).</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>کد کمپین (تصویر لحظه‌ای).</summary>
    public string? CampaignCode { get; set; }

    // --- هویت پاسخ‌گو -----------------------------------------------------------

    /// <summary>شناسه‌ی کاربر پاسخ‌گو. برای نظرسنجی‌های ناشناس همواره <c>null</c> است.</summary>
    public Guid? RespondentUserId { get; set; }

    /// <summary>شناسه‌ی کارمند پاسخ‌گو. برای نظرسنجی‌های ناشناس همواره <c>null</c> است.</summary>
    public Guid? RespondentEmployeeId { get; set; }

    /// <summary>نام نمایشی پاسخ‌گو (تصویر لحظه‌ای). برای نظرسنجی‌های ناشناس <c>null</c> است.</summary>
    public string? RespondentDisplayName { get; set; }

    /// <summary>آیا این پاسخ ناشناس جمع‌آوری شده؟ (تنظیمات نظرسنجی).</summary>
    public bool IsAnonymous { get; set; }

    // --- چرخه‌ی عمر --------------------------------------------------------------

    /// <summary>وضعیت نشست.</summary>
    public ResponseStatus Status { get; set; } = ResponseStatus.InProgress;

    /// <summary>کانال دسترسی پاسخ‌گو.</summary>
    public ResponseSource Source { get; set; } = ResponseSource.DirectLink;

    /// <summary>زبانی که پاسخ‌گو با آن پاسخ داده (برای نمایش آینده).</summary>
    public Language ResponseLanguage { get; set; } = Language.Fa;

    /// <summary>زمان شروع نشست (UTC).</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>زمان ارسال نهایی (UTC).</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>زمان آخرین فعالیت (ذخیره‌ی جزئی یا ارسال).</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>تعداد پاسخ‌های ثبت‌شده در این نشست (مقدار دارد).</summary>
    public int AnswerCount { get; set; }

    /// <summary>پاسخ‌های این نشست.</summary>
    public List<ResponseAnswer> Answers { get; set; } = [];

    // --- محاسبات ----------------------------------------------------------------

    /// <summary>آیا نشست ارسال شده است؟</summary>
    public bool IsSubmitted => Status == ResponseStatus.Submitted;

    /// <summary>آیا هنوز قابل تکمیل/ویرایش است؟</summary>
    public bool IsEditable => Status == ResponseStatus.InProgress;

    /// <summary>به‌روزرسانی زمان آخرین فعالیت.</summary>
    public void Touch() => LastActivityAt = DateTime.UtcNow;

    /// <summary>علامت‌گذاری به‌عنوان ارسال‌شده.</summary>
    public void Submit()
    {
        Status = ResponseStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        AnswerCount = Answers.Count(a => a.HasValue);
        Touch();
    }

    /// <summary>
    /// باز کردن مجدد یک نشست ارسال‌شده برای ویرایش. فقط وقتی نظرسنجی اجازه‌ی
    /// ویرایش پاسخ را دهد فراخوانی می‌شود.
    /// </summary>
    public void Reopen()
    {
        Status = ResponseStatus.InProgress;
        SubmittedAt = null;
        Touch();
    }

    /// <summary>دریافت پاسخ یک آیتم یا <c>null</c>.</summary>
    public ResponseAnswer? FindAnswer(Guid questionnaireItemId) =>
        Answers.FirstOrDefault(a => a.QuestionnaireItemId == questionnaireItemId);

    /// <summary>دریافت یا ساخت پاسخ یک آیتم.</summary>
    public ResponseAnswer GetOrCreateAnswer(Guid questionnaireItemId, Guid questionId, string questionCode, QuestionType questionType, int displayOrder)
    {
        var answer = FindAnswer(questionnaireItemId);
        if (answer is not null)
        {
            return answer;
        }

        answer = new ResponseAnswer
        {
            SessionId = Id,
            QuestionnaireItemId = questionnaireItemId,
            QuestionId = questionId,
            QuestionCode = questionCode,
            QuestionType = questionType,
            DisplayOrder = displayOrder
        };

        Answers.Add(answer);
        return answer;
    }

    /// <summary>حذف پاسخ‌های بدون مقدار (پاکسازی هنگام ذخیره‌ی جزئی).</summary>
    public void PruneEmptyAnswers() =>
        Answers.RemoveAll(a => !a.HasValue);
}
