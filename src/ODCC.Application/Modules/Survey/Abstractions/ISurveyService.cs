using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Survey.Abstractions;

/// <summary>
/// سرویس مدیریت نظرسنجی‌ها: چرخه‌ی عمر (انتشار/شروع/توقف/از سرگیری/بستن/بایگانی)،
/// تنظیمات و پرچم ناشناس.
/// </summary>
public interface ISurveyService
{
    Task<PagedResult<SurveySummaryDto>> SearchAsync(SurveySearchRequest request, CancellationToken ct = default);

    Task<Result<SurveyDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<SurveyDto>> CreateAsync(SaveSurveyRequest request, CancellationToken ct = default);

    /// <summary>به‌روزرسانی تنظیمات نظرسنجی (فقط در حالت پیش‌نویس).</summary>
    Task<Result<SurveyDto>> UpdateAsync(Guid id, SaveSurveyRequest request, CancellationToken ct = default);

    /// <summary>انتشار نظرسنجی: گذار به Scheduled (تاریخ شروع در آینده) یا Active.</summary>
    Task<Result<SurveyDto>> PublishAsync(Guid id, CancellationToken ct = default);

    /// <summary>باز کردن پنجره‌ی پاسخ‌گویی (از حالت زمان‌بندی‌شده).</summary>
    Task<Result<SurveyDto>> StartAsync(Guid id, CancellationToken ct = default);

    /// <summary>توقف موقت پاسخ‌گویی.</summary>
    Task<Result<SurveyDto>> PauseAsync(Guid id, CancellationToken ct = default);

    /// <summary>از سرگیری پاسخ‌گویی پس از توقف.</summary>
    Task<Result<SurveyDto>> ResumeAsync(Guid id, CancellationToken ct = default);

    /// <summary>بستن پنجره‌ی پاسخ‌گویی برای همیشه.</summary>
    Task<Result<SurveyDto>> CloseAsync(Guid id, CancellationToken ct = default);

    /// <summary>بایگانی نظرسنجی.</summary>
    Task<Result<SurveyDto>> ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>حذف نرم نظرسنجی (فقط در حالت پیش‌نویس).</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>ساخت یک نظرسنجی از روی یک قالب (کپی تنظیمات قالب).</summary>
    Task<Result<SurveyDto>> CreateFromTemplateAsync(CreateSurveyFromTemplateRequest request, CancellationToken ct = default);
}
