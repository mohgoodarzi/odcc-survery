using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Domain.Common;
using QuestionnaireEntity = ODCC.Domain.Modules.Questionnaire.Entities.Questionnaire;

namespace ODCC.Application.Modules.Questionnaire.Abstractions;

/// <summary>
/// مخزن اختصاصی پرسشنامه‌ها.
/// </summary>
public interface IQuestionnaireRepository : IRepository<QuestionnaireEntity>
{
    Task<QuestionnaireEntity?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>جستجوی صفحه‌بندی‌شده با فیلتر.</summary>
    Task<IReadOnlyList<QuestionnaireEntity>> SearchAsync(QuestionnaireSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد کل پرسشنامه‌های مطابق با فیلتر.</summary>
    Task<int> CountAsync(QuestionnaireSearchRequest request, CancellationToken ct = default);
}
