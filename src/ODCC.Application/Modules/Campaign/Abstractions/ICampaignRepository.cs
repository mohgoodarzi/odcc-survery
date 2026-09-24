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

    /// <summary>جستجوی صفحه‌بندی‌شده با فیلتر.</summary>
    Task<IReadOnlyList<CampaignEntity>> SearchAsync(CampaignSearchRequest request, CancellationToken ct = default);

    /// <summary>تعداد کل کمپین‌های مطابق با فیلتر.</summary>
    Task<int> CountAsync(CampaignSearchRequest request, CancellationToken ct = default);

    /// <summary>کمپین‌های «در حال اجرا» که حداقل یک یادآور سررسیده دارند.</summary>
    Task<IReadOnlyList<CampaignEntity>> GetDueForRemindersAsync(CancellationToken ct = default);
}
