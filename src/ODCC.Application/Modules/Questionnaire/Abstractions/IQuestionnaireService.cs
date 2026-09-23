using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Questionnaire.Abstractions;

/// <summary>
/// سرویس مدیریت پرسشنامه‌ها: ساختار درختی بخش‌ها و آیتم‌ها،
/// انتشار و بایگانی.
/// </summary>
public interface IQuestionnaireService
{
    Task<PagedResult<QuestionnaireSummaryDto>> SearchAsync(QuestionnaireSearchRequest request, CancellationToken ct = default);

    Task<Result<QuestionnaireDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<QuestionnaireDto>> CreateAsync(SaveQuestionnaireRequest request, CancellationToken ct = default);

    /// <summary>ذخیره‌ی کامل ساختار پرسشنامه (بخش‌ها، آیتم‌ها و قوانین انشعاب) در یک تراکنش.</summary>
    Task<Result<QuestionnaireDto>> UpdateAsync(Guid id, SaveQuestionnaireRequest request, CancellationToken ct = default);

    /// <summary>انتشار پرسشنامه (فعال‌سازی برای استفاده در نظرسنجی‌ها).</summary>
    Task<Result<QuestionnaireDto>> PublishAsync(Guid id, CancellationToken ct = default);

    /// <summary>بایگانی پرسشنامه.</summary>
    Task<Result<QuestionnaireDto>> ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>حذف نرم پرسشنامه (فقط در حالت پیش‌نویس).</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
