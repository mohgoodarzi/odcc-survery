using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Campaign.Entities;

/// <summary>
/// یک واحد سازمانی هدف در یک کمپین (برای <c>TargetAudienceType.OrgUnits</c>).
/// </summary>
public class CampaignTargetUnit : BaseEntity
{
    /// <summary>شناسه‌ی کمپین والد.</summary>
    public Guid CampaignId { get; set; }

    /// <summary>شناسه‌ی واحد سازمانی هدف.</summary>
    public Guid OrgUnitId { get; set; }

    /// <summary>آیا زیرمجموعه‌های این واحد هم شامل می‌شوند؟</summary>
    public bool IncludeDescendants { get; set; }
}
