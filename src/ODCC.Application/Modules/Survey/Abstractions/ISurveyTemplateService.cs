using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Survey.Abstractions;

/// <summary>
/// سرویس مدیریت قالب‌های نظرسنجی: ایجاد، فهرست‌بندی و بایگانی.
/// قالب‌ها تنظیمات ثابت یک نظرسنجی را برای استفاده‌ی مجدد نگه می‌دارند.
/// </summary>
public interface ISurveyTemplateService
{
    Task<PagedResult<SurveyTemplateSummaryDto>> SearchAsync(SurveyTemplateSearchRequest request, CancellationToken ct = default);

    Task<Result<SurveyTemplateDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<SurveyTemplateDto>> CreateAsync(SaveSurveyTemplateRequest request, CancellationToken ct = default);

    /// <summary>به‌روزرسانی قالب (تنها زمانی که فعال است).</summary>
    Task<Result<SurveyTemplateDto>> UpdateAsync(Guid id, SaveSurveyTemplateRequest request, CancellationToken ct = default);

    /// <summary>بایگانی قالب.</summary>
    Task<Result<SurveyTemplateDto>> ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>حذف نرم قالب (فقط در حالت فعال، تا زمانی که نظرسنجی‌ای به آن وابسته نباشد).</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
