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
}
