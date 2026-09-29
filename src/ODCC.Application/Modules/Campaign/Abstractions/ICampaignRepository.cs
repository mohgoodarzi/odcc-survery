using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;
using CampaignEntity = ODCC.Domain.Modules.Campaign.Entities.Campaign;

namespace ODCC.Application.Modules.Campaign.Abstractions;

/// <summary>
/// مخزن اختصاصی کمپین‌ها.
/// </summary>
public interface ICampaignRepository : IRepository<CampaignEntity>
{
    Task<CampaignEntity?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// جستجوی صفحه‌بندی‌شده با فیلتر.
    /// </summary>
    /// <param name="request">درخواست جستجو.</param>
    /// <param name="scope">
    /// دامنه‌ی سازمانی قابل‌مشاهده (fail-closed). <c>null</c> یعنی بدون محدودیت.
    /// </param>
    /// <param name="ct">توکن لغو.</param>
    Task<IReadOnlyList<CampaignEntity>> SearchAsync(
        CampaignSearchRequest request,
        ODCC.Application.Authorization.OrgScope? scope = null,
        CancellationToken ct = default);

    /// <summary>
    /// تعداد کل کمپین‌های مطابق با فیلتر.
    /// </summary>
    /// <param name="request">درخواست جستجو.</param>
    /// <param name="scope">
    /// دامنه‌ی سازمانی قابل‌مشاهده (همان semantics <see cref="SearchAsync(CampaignSearchRequest, ODCC.Application.Authorization.OrgScope?, CancellationToken)"/>).
    /// </param>
    /// <param name="ct">توکن لغو.</param>
    Task<int> CountAsync(
        CampaignSearchRequest request,
        ODCC.Application.Authorization.OrgScope? scope = null,
        CancellationToken ct = default);

    /// <summary>کمپین‌های «در حال اجرا» که حداقل یک یادآور سررسیده دارند.</summary>
    Task<IReadOnlyList<CampaignEntity>> GetDueForRemindersAsync(CancellationToken ct = default);
}
