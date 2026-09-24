using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using SurveyTemplateEntity = ODCC.Domain.Modules.Survey.Entities.SurveyTemplate;

namespace ODCC.Application.Modules.Survey.Abstractions;

/// <summary>
/// مخزن اختصاصی قالب‌های نظرسنجی.
/// </summary>
public interface ISurveyTemplateRepository : IRepository<SurveyTemplateEntity>
{
    Task<SurveyTemplateEntity?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>جستجوی صفحه‌بندی‌شده با فیلتر.</summary>
    Task<IReadOnlyList<SurveyTemplateEntity>> SearchAsync(SurveyTemplateSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد کل قالب‌های مطابق با فیلتر.</summary>
    Task<int> CountAsync(SurveyTemplateSearchRequest request, CancellationToken ct = default);
}
