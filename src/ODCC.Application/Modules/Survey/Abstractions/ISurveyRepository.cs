using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using SurveyEntity = ODCC.Domain.Modules.Survey.Entities.Survey;

namespace ODCC.Application.Modules.Survey.Abstractions;

/// <summary>
/// مخزن اختصاصی نظرسنجی‌ها.
/// </summary>
public interface ISurveyRepository : IRepository<SurveyEntity>
{
    Task<SurveyEntity?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>جستجوی صفحه‌بندی‌شده با فیلتر.</summary>
    Task<IReadOnlyList<SurveyEntity>> SearchAsync(SurveySearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد کل نظرسنجی‌های مطابق با فیلتر.</summary>
    Task<int> CountAsync(SurveySearchRequest request, CancellationToken ct = default);
}
