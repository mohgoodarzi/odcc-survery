using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Response.Dtos;
using ODCC.Domain.Modules.Response.Entities;
using ResponseSessionEntity = ODCC.Domain.Modules.Response.Entities.ResponseSession;

namespace ODCC.Application.Modules.Response.Abstractions;

/// <summary>
/// مخزن اختصاصی نشست‌های پاسخ.
/// </summary>
public interface IResponseRepository : IRepository<ResponseSessionEntity>
{
    /// <summary>جستجوی صفحه‌بندی‌شده با فیلتر.</summary>
    Task<IReadOnlyList<ResponseSessionEntity>> SearchAsync(ResponseSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد کل نشست‌های مطابق با فیلتر.</summary>
    Task<int> CountAsync(ResponseSearchRequest request, CancellationToken ct = default);

    /// <summary>دریافت یک نشست به‌همراه تمام پاسخ‌های آن.</summary>
    Task<ResponseSessionEntity?> GetByIdWithAnswersAsync(Guid id, CancellationToken ct = default);

    /// <summary>نشست فعال (در حال تکمیل) یک کاربر برای یک نظرسنجی.</summary>
    Task<ResponseSessionEntity?> FindInProgressAsync(Guid surveyId, Guid respondentUserId, CancellationToken ct = default);

    /// <summary>نشست ارسال‌شده‌ی یک کاربر برای یک نظرسنجی.</summary>
    Task<ResponseSessionEntity?> FindSubmittedAsync(Guid surveyId, Guid respondentUserId, CancellationToken ct = default);

    /// <summary>همه‌ی نشست‌های یک کاربر (برای محاسبه‌ی وضعیت پاسخ به نظرسنجی‌های باز).</summary>
    Task<IReadOnlyList<ResponseSessionEntity>> ListByRespondentAsync(Guid respondentUserId, CancellationToken ct = default);

    /// <summary>تعداد نشست‌های ارسال‌شده‌ی یک نظرسنجی (برای فاز تحلیلات).</summary>
    Task<int> CountSubmittedBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    /// <summary>
    /// همه‌ی نشست‌های ارسال‌شده‌ی یک نظرسنجی به‌همراه پاسخ‌ها و گزینه‌های انتخابی.
    /// این متد فقط برای <b>ماژول تحلیلات</b> است تا تجمع‌ها را روی پاسخ‌های خام
    /// محاسبه کند. مرز ماژول‌ها از طریق همین قرارداد رعایت می‌شود.
    /// </summary>
    /// <param name="surveyId">شناسه‌ی نظرسنجی.</param>
    /// <param name="fromUtc">فیلتر شامل‌شدن بر اساس زمان ارسال (اختیاری).</param>
    /// <param name="toUtc">فیلتر شامل‌شدن بر اساس زمان ارسال (اختیاری).</param>
    /// <param name="ct">توکن لغو.</param>
    Task<IReadOnlyList<ResponseSessionEntity>> ListSubmittedBySurveyAsync(
        Guid surveyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken ct = default);

    /// <summary>
    /// همه‌ی نشست‌های ارسال‌شده‌ی چند نظرسنجی (برای داشبورد و روند زمانی تحلیلات).
    /// فقط فیلدهای مورد نیاز تجمع را به‌صورت بدنه (projection) برمی‌گرداند تا
    /// پاسخ‌های خام و داده‌های هویتی جابه‌جا نشوند. بخش‌بندی سازمانی از طریق
    /// <see cref="SubmittedSessionSummary.RespondentEmployeeId"/> و قراردادهای
    /// ماژول سازمان حل می‌شود.
    /// </summary>
    Task<IReadOnlyList<SubmittedSessionSummary>> ListSubmittedAsync(
        IReadOnlyCollection<Guid> surveyIds,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken ct = default);

    /// <summary>
    /// همه‌ی نشست‌های یک نظرسنجی (شامل نشست‌های در حال تکمیل و ارسال‌شده) بدون
    /// پاسخ‌ها. این متد فقط برای <b>ماژول تحلیلات</b> است تا مخرج نرخ تکمیل را
    /// تشکیل دهد: نشست‌هایی که شروع شده‌اند ولی هنوز ارسال نشده‌اند، افت پاسخ
    /// کامل را نشان می‌دهند. مرز ماژول‌ها از طریق همین قرارداد رعایت می‌شود.
    /// </summary>
    /// <param name="surveyId">شناسه‌ی نظرسنجی.</param>
    /// <param name="fromUtc">فیلتر شامل‌شدن بر اساس زمان شروع (اختیاری).</param>
    /// <param name="toUtc">فیلتر شامل‌شدن بر اساس زمان شروع (اختیاری).</param>
    /// <param name="ct">توکن لغو.</param>
    Task<IReadOnlyList<ResponseSessionEntity>> ListAllBySurveyAsync(
        Guid surveyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken ct = default);
}

/// <summary>
/// خلاصه‌ی یک نشست ارسال‌شده برای تحلیلات — بدون هیچ داده‌ی هویتی مستقیم پاسخ‌گو.
/// این DTO فقط فیلدهای تجمعی و <see cref="RespondentEmployeeId"/> (برای حل
/// بخش‌بندی سازمانی از طریق قراردادهای ماژول سازمان) را حمل می‌کند.
/// </summary>
public sealed record SubmittedSessionSummary
{
    public Guid SessionId { get; init; }
    public Guid SurveyId { get; init; }
    public Guid? CampaignId { get; init; }

    /// <summary>زمان ارسال (برای روند زمانی).</summary>
    public DateTime SubmittedAt { get; init; }

    /// <summary>زمان شروع (برای محاسبه‌ی مدت پاسخ‌گویی).</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>شناسه‌ی کارمند پاسخ‌گو — فقط برای نظرسنجی‌های غیرناشناس (در ناشناس <c>null</c>).</summary>
    public Guid? RespondentEmployeeId { get; init; }

    /// <summary>کانال ورود پاسخ.</summary>
    public ODCC.Domain.Modules.Response.Enums.ResponseSource Source { get; init; }

    /// <summary>تعداد پاسخ‌های دارای مقدار.</summary>
    public int AnswerCount { get; init; }
}
