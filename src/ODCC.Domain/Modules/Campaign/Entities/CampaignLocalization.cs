using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Campaign.Entities;

/// <summary>
/// ترجمه‌ی عنوان و توضیح یک کمپین.
/// این متن در دعوت‌نامه‌ی ارسالی به گیرندگان استفاده می‌شود.
/// </summary>
public class CampaignLocalization : Localization
{
    /// <summary>عنوان کمپین (موضوع دعوت‌نامه).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>توضیح/متن دعوت‌نامه (اختیاری).</summary>
    public string? Description { get; set; }

    /// <summary>شناسه‌ی کمپین والد.</summary>
    public Guid CampaignId { get; set; }
}
