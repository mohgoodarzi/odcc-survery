using ODCC.Application.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.QuestionBank.Abstractions;

/// <summary>
/// سرویس مدیریت کتابخانه‌ی سؤالات: ایجاد، ویرایش، جستجو و نسخه‌برداری.
/// </summary>
public interface IQuestionService
{
    Task<PagedResult<QuestionDto>> SearchAsync(QuestionSearchRequest request, CancellationToken ct = default);

    Task<Result<QuestionDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<QuestionDto>> CreateAsync(SaveQuestionRequest request, CancellationToken ct = default);

    Task<Result<QuestionDto>> UpdateAsync(Guid id, SaveQuestionRequest request, CancellationToken ct = default);

    /// <summary>بایگانی کردن سؤال (کنار گذاشتن از پرسشنامه‌های جدید).</summary>
    Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>تاریخچه‌ی نسخه‌های یک سؤال.</summary>
    Task<Result<IReadOnlyList<QuestionVersionDto>>> GetVersionsAsync(Guid id, CancellationToken ct = default);
}
