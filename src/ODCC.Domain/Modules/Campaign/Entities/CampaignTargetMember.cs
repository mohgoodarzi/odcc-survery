using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Campaign.Entities;

/// <summary>
/// یک کارمند هدفِ صریح در یک کمپین (برای <c>TargetAudienceType.Employees</c>).
/// </summary>
public class CampaignTargetMember : BaseEntity
{
    /// <summary>شناسه‌ی کمپین والد.</summary>
    public Guid CampaignId { get; set; }

    /// <summary>شناسه‌ی کارمند هدف.</summary>
    public Guid EmployeeId { get; set; }
}
