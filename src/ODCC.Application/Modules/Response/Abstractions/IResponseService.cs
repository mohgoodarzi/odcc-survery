using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Response.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Response.Abstractions;

/// <summary>
/// سرویس مدیریت پاسخ‌ها: چرخه‌ی عمر نشست (شروع، ذخیره‌ی جزئی، ارسال)،
/// اعتبارسنجی سؤال‌های اجباری، سیاست‌های ناشناس بودن و پاسخ یگانه،
/// و یکپارچه‌سازی با نظرسنجی و کمپین.
/// </summary>
public interface IResponseService
{
    // --- پاسخ‌گو ---------------------------------------------------------------

    /// <summary>
    /// بسته‌ی کامل یک نظرسنجی برای پاسخ‌گویی: تنظیمات، ساختار پرسشنامه
    /// (بخش‌ها/آیتم‌ها/گزینه‌ها) و نشست قبلی پاسخ‌گو (در صورت وجود).
    /// </summary>
    Task<Result<RespondentSurveyContextDto>> GetRespondentContextAsync(Guid surveyId, CancellationToken ct = default);

    /// <summary>
    /// شروع یا از سرگیری یک نشست پاسخ‌گویی.
    /// سیاست‌های پاسخ یگانه و ویرایش پاسخ در این‌جا اعمال می‌شوند.
    /// </summary>
    Task<Result<ResponseSessionDto>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default);

    /// <summary>نشست فعلی پاسخ‌گو برای یک نظرسنجی (در صورت وجود).</summary>
    Task<Result<ResponseSessionDto>> GetMySessionAsync(Guid surveyId, CancellationToken ct = default);

    /// <summary>ذخیره‌ی جزئی پاسخ‌ها (بدون اعتبارسنجی سؤال‌های اجباری).</summary>
    Task<Result<ResponseSessionDto>> SaveAnswersAsync(Guid sessionId, SaveAnswersRequest request, CancellationToken ct = default);

    /// <summary>
    /// ارسال نهایی پاسخ‌ها. سؤال‌های اجباری اعتبارسنجی می‌شوند و در صورت نبود
    /// پاسخ، نتیجه‌ی خطا برگردانده می‌شود.
    /// </summary>
    Task<Result<ResponseSessionDto>> SubmitAsync(Guid sessionId, SubmitResponseRequest request, CancellationToken ct = default);

    /// <summary>نظرسنجی‌هایی که کاربر جاری می‌تواند به آن‌ها پاسخ دهد.</summary>
    Task<PagedResult<RespondableSurveyDto>> GetMySurveysAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);

    // --- مدیریت / تحلیلات ------------------------------------------------------

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی نشست‌های پاسخ.</summary>
    Task<PagedResult<ResponseSessionSummaryDto>> SearchAsync(ResponseSearchRequest request, CancellationToken ct = default);

    /// <summary>دریافت یک نشست با شناسه (به‌همراه پاسخ‌ها).</summary>
    Task<Result<ResponseSessionDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>حذف نرم یک نشست (فقط مدیران).</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
