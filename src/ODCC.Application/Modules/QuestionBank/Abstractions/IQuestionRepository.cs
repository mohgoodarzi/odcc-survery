using ODCC.Application.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Entities;

namespace ODCC.Application.Modules.QuestionBank.Abstractions;

/// <summary>
/// مخزن اختصاصی سؤال‌های کتابخانه.
/// </summary>
public interface IQuestionRepository : IRepository<Question>
{
    Task<Question?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>دریافت چندین سؤال با شناسه (برای پرسشنامه‌ها).</summary>
    Task<IReadOnlyList<Question>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>تعداد نسخه‌های ثبت‌شده‌ی یک سؤال.</summary>
    Task<int> CountVersionsAsync(Guid questionId, CancellationToken ct = default);

    /// <summary>آخرین شماره‌ی نسخه‌ی یک سؤال (۰ در صورت نبودن).</summary>
    Task<int> GetLatestVersionNumberAsync(Guid questionId, CancellationToken ct = default);

    /// <summary>تاریخچه‌ی نسخه‌های یک سؤال.</summary>
    Task<IReadOnlyList<QuestionVersion>> GetVersionsAsync(Guid questionId, CancellationToken ct = default);
}
