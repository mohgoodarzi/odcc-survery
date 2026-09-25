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

    /// <summary>
    /// شناسه‌ی نظرسنجی‌های پاسخ‌پذیر (فعال و منقضی‌نشده). این متد فقط برای خواندن
    /// توسط ماژول پاسخ‌هاست و DTO برمی‌گرداند تا مرز ماژول‌ها حفظ شود.
    /// </summary>
    Task<IReadOnlyList<RespondableSurveySummaryDto>> GetRespondableAsync(CancellationToken ct = default);
}

/// <summary>
/// خلاصه‌ی یک نظرسنجی پاسخ‌پذیر (برای ماژول پاسخ‌ها). شامل فقط داده‌های
/// لازم برای نمایش در فهرست «نظرسنجی‌های من» است.
/// </summary>
public sealed record RespondableSurveySummaryDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }
    public bool AcceptsResponses { get; init; }
    public int EstimatedMinutes { get; init; }

    /// <summary>عنوان در زبان درخواست‌شده.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>توضیح در زبان درخواست‌شده.</summary>
    public string? Description { get; init; }
}
