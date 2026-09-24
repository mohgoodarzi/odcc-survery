using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;

namespace ODCC.Domain.Modules.Campaign.Entities;

/// <summary>
/// توزیع یک دعوت‌نامه به یک گیرنده‌ی خاص: یک ردیف به ازای هر کارمندِ عضو
/// جمعیت هدف، که وضعیت ارسال و پاسخ را دنبال می‌کند.
///
/// این موجودیت به‌صورت جداگانه از تجمع کمپین نوشته می‌شود چون می‌تواند هزاران
/// ردیف داشته باشد و نباید همواره همراه با کمپین بارگذاری شود. ارسال واقعی در
/// ماژول اعلان‌ها (Notification) انجام می‌شود؛ در این فاز وضعیت‌گذاری آن پیاده‌سازی
/// شده تا تاریخچه‌ی توزیع قابل پیگیری و آزمون باشد.
/// </summary>
public class Distribution : BaseEntity
{
    /// <summary>شناسه‌ی کمپین والد.</summary>
    public Guid CampaignId { get; set; }

    /// <summary>شناسه‌ی کارمند گیرنده.</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>ایمیل سازمانی گیرنده (تصویر لحظه‌ای برای تاریخچه).</summary>
    public string? WorkEmail { get; set; }

    /// <summary>وضعیت توزیع.</summary>
    public DistributionStatus Status { get; set; } = DistributionStatus.Pending;

    /// <summary>زمان ارسال (UTC).</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>زمان ثبت پاسخ (UTC) — توسط ماژول پاسخ‌ها تنظیم می‌شود.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>دلیل شکست ارسال (در صورت وجود).</summary>
    public string? FailureReason { get; set; }

    /// <summary>تعداد یادآورهای ارسال‌شده برای این گیرنده.</summary>
    public int ReminderCount { get; set; }

    /// <summary>ثبت ارسال موفق.</summary>
    public void MarkSent()
    {
        Status = DistributionStatus.Sent;
        SentAt = DateTime.UtcNow;
        FailureReason = null;
    }

    /// <summary>ثبت شکست ارسال.</summary>
    public void MarkFailed(string reason)
    {
        Status = DistributionStatus.Failed;
        FailureReason = reason;
    }

    /// <summary>ثبت پاسخ گیرنده (توسط ماژول پاسخ‌ها فراخوانی می‌شود).</summary>
    public void MarkResponded()
    {
        Status = DistributionStatus.Responded;
        RespondedAt = DateTime.UtcNow;
    }

    /// <summary>افزایش شمارنده‌ی یادآور.</summary>
    public void IncrementReminderCount() => ReminderCount++;
}
