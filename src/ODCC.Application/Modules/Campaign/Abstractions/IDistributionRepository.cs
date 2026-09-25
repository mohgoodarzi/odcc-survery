using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Domain.Modules.Campaign.Entities;
using ODCC.Domain.Modules.Campaign.Enums;

namespace ODCC.Application.Modules.Campaign.Abstractions;

/// <summary>
/// مخزن اختصاصی ردیف‌های توزیع.
///
/// ردیف‌های توزیع به‌صورت جداگانه از تجمع کمپین مدیریت می‌شوند چون می‌توانند
/// تعداد زیادی داشته باشند و نباید همواره همراه با کمپین بارگذاری شوند.
/// </summary>
public interface IDistributionRepository
{
    Task<Distribution?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>افزودن چند ردیف توزیع در یک عملیات.</summary>
    Task AddRangeAsync(IReadOnlyCollection<Distribution> distributions, CancellationToken ct = default);

    /// <summary>تعداد ردیف‌های توزیع یک کمپین.</summary>
    Task<int> CountByCampaignAsync(Guid campaignId, CancellationToken ct = default);

    /// <summary>تعداد ردیف‌های توزیع چند کمپین در یک پرس‌وجو (برای فهرست کمپین‌ها).</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByCampaignsAsync(IReadOnlyCollection<Guid> campaignIds, CancellationToken ct = default);

    /// <summary>تعداد ردیف‌های توزیع یک کمپین بر اساس وضعیت.</summary>
    Task<int> CountByStatusAsync(Guid campaignId, DistributionStatus status, CancellationToken ct = default);

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی ردیف‌های توزیع یک کمپین.</summary>
    Task<IReadOnlyList<Distribution>> SearchByCampaignAsync(Guid campaignId, DistributionSearchRequest request, CancellationToken ct = default);

    /// <summary>همه‌ی ردیف‌های توزیع یک کمپین (برای حل جمعیت هدف مجدد).</summary>
    Task<IReadOnlyList<Distribution>> ListByCampaignAsync(Guid campaignId, CancellationToken ct = default);

    /// <summary>ردیف‌های توزیعِ یک کمپین که هنوز پاسخ نداده‌اند (برای یادآورها: در صف یا ارسال‌شده).</summary>
    Task<IReadOnlyList<Distribution>> GetRemindableAsync(Guid campaignId, CancellationToken ct = default);

    /// <summary>
    /// دعوت‌نامه‌های باز یک کارمند (ارسال‌شده ولی هنوز پاسخ‌داده‌نشده).
    /// این متد برای ماژول پاسخ‌هاست تا نشست پاسخ‌گویی را به دعوت‌نامه‌اش پیوند بزند؛
    /// به همین دلیل DTO برمی‌گرداند، نه موجودیت داخلی.
    /// </summary>
    Task<IReadOnlyList<OpenDistributionDto>> GetOpenByEmployeeAsync(Guid employeeId, CancellationToken ct = default);

    void Update(Distribution distribution);
}
